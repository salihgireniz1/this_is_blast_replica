from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import importlib.metadata
import json
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any


INVALID_WINDOWS_CHARS = re.compile(r'[<>:"\\|?*]')
NUMBERED_ASSET = re.compile(r"^(\d+)(?:\s+(.*))?$")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract This is Blast Unity level assets into readable JSON files."
    )
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--unitypy-root", type=Path, required=True)
    return parser.parse_args()


def json_default(value: Any) -> Any:
    if isinstance(value, bytes):
        return {
            "encoding": "base64",
            "data": base64.b64encode(value).decode("ascii"),
        }
    if hasattr(value, "__dict__"):
        return vars(value)
    return str(value)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as handle:
        json.dump(
            value,
            handle,
            ensure_ascii=False,
            indent=2,
            default=json_default,
        )
        handle.write("\n")


def safe_part(part: str) -> str:
    cleaned = INVALID_WINDOWS_CHARS.sub("_", part).rstrip(". ")
    return cleaned or "_"


def safe_relative_asset_path(source_path: str) -> Path:
    source = PurePosixPath(source_path)
    expected_prefix = PurePosixPath("Assets/Levels")
    try:
        relative = source.relative_to(expected_prefix)
    except ValueError as exc:
        raise ValueError(f"Unexpected level path: {source_path}") from exc

    parts = [safe_part(part) for part in relative.parts]
    output = Path(*parts)
    return output.with_suffix(".json")


def safe_filename(name: str, path_id: int, suffix: str) -> str:
    return f"{safe_part(name)}_{path_id}{suffix}"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    args = parse_args()
    bundle = args.bundle.resolve(strict=True)
    output = args.output.resolve()
    unitypy_root = args.unitypy_root.resolve(strict=True)
    working = output.with_name(f"{output.name}.working")

    if output.exists():
        raise FileExistsError(f"Output already exists: {output}")
    if working.exists():
        raise FileExistsError(f"Working directory already exists: {working}")

    sys.path.insert(0, str(unitypy_root))
    import UnityPy  # type: ignore[import-not-found]

    environment = UnityPy.load(str(bundle))
    containers = sorted(environment.container.items(), key=lambda item: item[0].casefold())

    working.mkdir(parents=True)
    levels_root = working / "levels"
    supporting_root = working / "supporting_assets"
    index_rows: list[dict[str, Any]] = []
    exported_by_path_id: dict[int, list[str]] = defaultdict(list)
    contained_path_ids: set[int] = set()
    category_counts: Counter[str] = Counter()

    for source_path, pointer in containers:
        if pointer.type.name != "MonoBehaviour":
            raise TypeError(
                f"Expected MonoBehaviour at {source_path}, got {pointer.type.name}"
            )

        data = pointer.deref_parse_as_dict()
        relative_output = Path("levels") / safe_relative_asset_path(source_path)
        destination = working / relative_output
        write_json(destination, data)

        path_id = int(pointer.path_id)
        contained_path_ids.add(path_id)
        exported_by_path_id[path_id].append(relative_output.as_posix())

        source_relative = PurePosixPath(source_path).relative_to("Assets/Levels")
        category = source_relative.parent.as_posix()
        category_counts[category] += 1
        asset_stem = source_relative.stem
        numbered = NUMBERED_ASSET.fullmatch(asset_stem)
        index_rows.append(
            {
                "source_path": source_path,
                "output_path": relative_output.as_posix(),
                "path_id": path_id,
                "name": data.get("m_Name", ""),
                "difficulty_level": data.get("difficultyLevel", ""),
                "category": category,
                "level_number": int(numbered.group(1)) if numbered else "",
                "variant": numbered.group(2) if numbered and numbered.group(2) else "",
            }
        )

    supporting_counts: Counter[str] = Counter()
    for obj in environment.objects:
        object_type = obj.type.name
        path_id = int(obj.path_id)

        if object_type == "MonoBehaviour" and path_id not in contained_path_ids:
            data = obj.parse_as_dict()
            name = str(data.get("m_Name") or "unnamed")
            relative_output = (
                Path("supporting_assets")
                / "MonoBehaviour"
                / safe_filename(name, path_id, ".json")
            )
            write_json(working / relative_output, data)
            exported_by_path_id[path_id].append(relative_output.as_posix())
            supporting_counts[object_type] += 1

        elif object_type == "Texture2D":
            texture = obj.parse_as_object()
            name = str(getattr(texture, "m_Name", None) or "unnamed")
            relative_output = (
                Path("supporting_assets")
                / "Texture2D"
                / safe_filename(name, path_id, ".png")
            )
            destination = working / relative_output
            destination.parent.mkdir(parents=True, exist_ok=True)
            texture.image.save(destination)
            exported_by_path_id[path_id].append(relative_output.as_posix())
            supporting_counts[object_type] += 1

        elif object_type == "AssetBundle":
            relative_output = Path("supporting_assets") / "AssetBundle" / "metadata.json"
            write_json(working / relative_output, obj.parse_as_dict())
            exported_by_path_id[path_id].append(relative_output.as_posix())
            supporting_counts[object_type] += 1

    with (working / "levels_index.csv").open(
        "x", encoding="utf-8-sig", newline=""
    ) as handle:
        writer = csv.DictWriter(handle, fieldnames=list(index_rows[0].keys()))
        writer.writeheader()
        writer.writerows(index_rows)

    object_map = [
        {
            "path_id": int(obj.path_id),
            "type": obj.type.name,
            "exported_files": exported_by_path_id.get(int(obj.path_id), []),
        }
        for obj in environment.objects
    ]
    write_json(working / "object_map.json", object_map)

    object_type_counts = Counter(obj.type.name for obj in environment.objects)
    numbered_rows = [row for row in index_rows if row["level_number"] != ""]
    unique_numbers = sorted({int(row["level_number"]) for row in numbered_rows})
    summary = {
        "source_bundle": str(bundle),
        "source_sha256": sha256(bundle),
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "unitypy_version": importlib.metadata.version("UnityPy"),
        "container_count": len(containers),
        "level_json_count": len(index_rows),
        "object_count": len(environment.objects),
        "object_type_counts": dict(sorted(object_type_counts.items())),
        "supporting_asset_counts": dict(sorted(supporting_counts.items())),
        "category_counts": dict(sorted(category_counts.items())),
        "numbered_asset_count": len(numbered_rows),
        "unique_level_numbers": len(unique_numbers),
        "minimum_level_number": min(unique_numbers) if unique_numbers else None,
        "maximum_level_number": max(unique_numbers) if unique_numbers else None,
    }
    write_json(working / "summary.json", summary)

    working.rename(output)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

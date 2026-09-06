from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verify an extracted level set.")
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--unitypy-root", type=Path, required=True)
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    args = parse_args()
    bundle = args.bundle.resolve(strict=True)
    output = args.output.resolve(strict=True)
    sys.path.insert(0, str(args.unitypy_root.resolve(strict=True)))

    import UnityPy  # type: ignore[import-not-found]
    from PIL import Image

    environment = UnityPy.load(str(bundle))
    source_paths = set(environment.container)

    with (output / "levels_index.csv").open(
        "r", encoding="utf-8-sig", newline=""
    ) as handle:
        index_rows = list(csv.DictReader(handle))

    assert len(index_rows) == len(source_paths), (
        f"Index has {len(index_rows)} rows, bundle has {len(source_paths)} containers"
    )
    assert {row["source_path"] for row in index_rows} == source_paths, (
        "Index source paths do not exactly match bundle container paths"
    )
    assert len({row["output_path"] for row in index_rows}) == len(index_rows), (
        "Index contains duplicate output paths"
    )

    level_json_files = sorted((output / "levels").rglob("*.json"))
    assert len(level_json_files) == len(index_rows), (
        f"Found {len(level_json_files)} level JSON files for {len(index_rows)} rows"
    )

    for row in index_rows:
        level_path = output / row["output_path"]
        assert level_path.is_file(), f"Missing level file: {level_path}"
        with level_path.open("r", encoding="utf-8") as handle:
            data = json.load(handle)
        assert data.get("m_Name") == row["name"], (
            f"Name mismatch for {row['source_path']}"
        )
        assert "myStack" in data and "myStage" in data, (
            f"Level fields missing from {row['source_path']}"
        )

    texture_files = sorted(
        (output / "supporting_assets" / "Texture2D").glob("*.png")
    )
    texture_object_count = sum(
        obj.type.name == "Texture2D" for obj in environment.objects
    )
    assert len(texture_files) == texture_object_count, (
        f"Found {len(texture_files)} PNG files for {texture_object_count} textures"
    )
    for texture_path in texture_files:
        with Image.open(texture_path) as image:
            image.verify()

    with (output / "object_map.json").open("r", encoding="utf-8") as handle:
        object_map = json.load(handle)
    assert len(object_map) == len(environment.objects), (
        f"Object map has {len(object_map)} rows, bundle has {len(environment.objects)} objects"
    )
    expected_type_counts = Counter(obj.type.name for obj in environment.objects)
    actual_type_counts = Counter(item["type"] for item in object_map)
    assert actual_type_counts == expected_type_counts, "Object type counts differ"

    with (output / "summary.json").open("r", encoding="utf-8") as handle:
        summary = json.load(handle)
    assert summary["source_sha256"] == sha256(bundle), "Bundle SHA-256 differs"
    assert summary["level_json_count"] == len(level_json_files), (
        "Summary level count differs"
    )
    assert summary["object_count"] == len(object_map), "Summary object count differs"

    campaign_rows = [
        row for row in index_rows if row["category"] == "Control Cohort VO"
    ]
    campaign_numbers = sorted(int(row["level_number"]) for row in campaign_rows)
    assert campaign_numbers == list(range(1, 2001)), (
        "Control Cohort VO is not a complete 1..2000 campaign"
    )

    print(
        json.dumps(
            {
                "bundle_containers": len(source_paths),
                "verified_level_json_files": len(level_json_files),
                "verified_campaign_levels": len(campaign_numbers),
                "verified_textures": len(texture_files),
                "verified_object_map_entries": len(object_map),
                "invalid_json_files": 0,
                "missing_files": 0,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

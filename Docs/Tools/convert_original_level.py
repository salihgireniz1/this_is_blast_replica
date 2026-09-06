"""Convert a level extracted from the original game into this project's JSON, and prove it.

The original stores a level as a ScriptableObject: `myStack` is the board (row-major, `width`
across), `myStage` the shooter deck (column-major, depth 0 is the selectable row), a cell's
`Value` is a palette index and `SecretItem` marks a hidden shooter. Ammo is not authored:
the game derives it, so here each colour's cubes are split evenly over that colour's
shooters, front-most shooters taking the remainder (exact fit, like `trimSurplusAmmo`).

The case fixes 10x10 and five named colours, so the board is cropped to its front `--rows`
rows and every palette index is renamed through `--map`. Then the level is played: a DFS
over the player's choices under GameRules (leftmost matching front, five slots, fail when
the row is full and targetless, no merge) must find a win, and that tap sequence is replayed
under random firing interleavings to show the win does not hinge on shot order.

    python convert_original_level.py --source <extracted.json> --map 2=Y,3=B,6=O,7=R,9=G
        --output Level_01.json [--rows 10] [--flip]
"""
import argparse, json, random, sys
from collections import Counter
from functools import lru_cache

SLOTS = 5
CASE_COLORS = set("YRBGO")


def read_original(path, rows, flip, color_map):
    level = json.load(open(path, encoding="utf-8"))
    stack, stage = level["myStack"], level["myStage"]
    width, height, cells = stack["width"], stack["height"], stack["customCellDrawingList"]
    board = [[color_map[cells[r * width + c]["Value"]] for c in range(width)] for r in range(height)]
    if flip:
        board.reverse()
    board = board[:rows]
    dw, dh, deck = stage["width"], stage["height"], stage["customCellDrawingList"]
    columns = []
    for c in range(dw):
        column = [deck[c * dh + r] for r in range(dh)]
        column = [cell for cell in column if cell["Enabled"] and cell["Value"] != 0]
        columns.append([{"color": color_map[cell["Value"]], "hidden": bool(cell["SecretItem"])} for cell in column])
    return board, columns


def arm(board, columns):
    """Exact-fit ammo: a colour's cubes split over its shooters, front-most get the remainder."""
    cubes = Counter(color for row in board for color in row)
    shooters = Counter(s["color"] for column in columns for s in column)
    for color in cubes:
        if shooters[color] == 0:
            sys.exit(f"colour {color} has cubes but no shooter")
    handed = Counter()
    for depth in range(max(len(c) for c in columns)):
        for column in columns:
            if depth < len(column):
                s = column[depth]
                base, extra = divmod(cubes[s["color"]], shooters[s["color"]])
                s["ammo"] = base + (1 if handed[s["color"]] < extra else 0)
                handed[s["color"]] += 1
    return cubes


def target_of(board, front, color):
    """GameRules.TryFindTarget: the leftmost column whose front cube wears the colour."""
    rows, cols = len(board), len(board[0])
    return next((c for c in range(cols) if front[c] < rows and board[front[c]][c] == color), -1)


def planner(board, columns):
    """A DFS player: from any state, the column to tap next on a winning line, or None.

    Firing resolves to a fixed point in slot order before each choice; the result is cached
    per state so the same planner can be asked again after every real tap.
    """
    cols, rows = len(board[0]), len(board)
    queue = tuple(tuple((s["color"], s["ammo"]) for s in column) for column in columns)

    def fire(front, slots):
        front, slots = list(front), list(slots)
        progress = True
        while progress:
            progress = False
            for i, s in enumerate(slots):
                if s is None:
                    continue
                target = target_of(board, front, s[0])
                if target < 0:
                    continue
                front[target] += 1
                slots[i] = None if s[1] == 1 else (s[0], s[1] - 1)
                progress = True
        return tuple(front), tuple(slots)

    @lru_cache(maxsize=None)
    def search(front, heads, slots):
        front, slots = fire(front, slots)
        if all(f >= rows for f in front):
            return ()
        if None not in slots:
            return None
        for q in range(len(queue)):
            if heads[q] < len(queue[q]):
                seated = list(slots)
                seated[seated.index(None)] = queue[q][heads[q]]
                heads2 = heads[:q] + (heads[q] + 1,) + heads[q + 1:]
                rest = search(front, heads2, tuple(seated))
                if rest is not None:
                    return (q,) + rest
        return None

    return search


def solve(board, columns):
    """The winning tap sequence from the start under slot-order firing, or None."""
    return planner(board, columns)((0,) * len(board[0]), (0,) * len(columns), (None,) * SLOTS)


def replay(board, columns, trials, seed=1):
    """An adaptive player against a random slot order per firing pass; returns the win ratio.

    Before every tap the planner is asked again from the real state, so this is the player
    who watches the board, not one who memorised a sequence.
    """
    cols, rows = len(board[0]), len(board)
    plan = planner(board, columns)
    rng, wins = random.Random(seed), 0
    for _ in range(trials):
        front, slots, heads = [0] * cols, [None] * SLOTS, [0] * len(columns)
        while not all(f >= rows for f in front):
            frozen = tuple(None if s is None else (s[0], s[1]) for s in slots)
            line = plan(tuple(front), tuple(heads), frozen)
            if not line:
                break
            q = line[0]
            s = columns[q][heads[q]]
            heads[q] += 1
            slots[slots.index(None)] = [s["color"], s["ammo"]]
            progress = True
            while progress:
                progress = False
                order = list(range(SLOTS))
                rng.shuffle(order)
                for i in order:
                    s = slots[i]
                    if s is None:
                        continue
                    target = target_of(board, front, s[0])
                    if target < 0:
                        continue
                    front[target] += 1
                    s[1] -= 1
                    progress = True
                    if s[1] == 0:
                        slots[i] = None
        wins += all(f >= rows for f in front)
    return wins / trials


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--source", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--map", required=True, help="palette index to case letter, e.g. 2=Y,3=B")
    p.add_argument("--rows", type=int, default=10)
    p.add_argument("--flip", action="store_true", help="the file's last row is the front")
    p.add_argument("--trials", type=int, default=500)
    args = p.parse_args()
    color_map = {int(k): v for k, v in (pair.split("=") for pair in args.map.split(","))}

    board, columns = read_original(args.source, args.rows, args.flip, color_map)
    cubes = arm(board, columns)
    hidden = sum(s["hidden"] for column in columns for s in column)
    print(f"board {len(board[0])}x{len(board)} cubes={dict(cubes)} columns={len(columns)} "
          f"shooters={sum(map(len, columns))} hidden={hidden}")
    if set(cubes) != CASE_COLORS or hidden == 0 or len(columns) > 5:
        sys.exit("does not meet the case: five colours, a hidden shooter, at most five columns")

    taps = solve(board, columns)
    if taps is None:
        sys.exit("unsolvable under GameRules without merge")
    ratio = replay(board, columns, args.trials)
    print(f"solvable: {len(taps)} taps {list(taps)}; adaptive player under random firing order wins {ratio:.0%}")

    level = {
        "boardLayers": [{"rows": ["".join(row) for row in board]}],
        "slotCount": SLOTS,
        "shooterColumns": [
            {"shooters": [{"color": s["color"], "ammo": s["ammo"], "hidden": s["hidden"]} for s in column]}
            for column in columns
        ],
    }
    with open(args.output, "w", encoding="utf-8", newline="\n") as f:
        json.dump(level, f, indent=2)
        f.write("\n")
    for row in board:
        print("".join(row))


if __name__ == "__main__":
    main()

"""Level_04 generator: the mid-size entry in the benchmark set - 400 cubes and 20 shooters,
between Level_01's 100 and Level_02's 600, so Docs/PERFORMANCE.md has a measurement point in
the middle of the range. 10x40 board, 5 queue columns x 4 shooters at 20 ammo each == 400,
one colour per column, so every colour is always at a front and the level is winnable by
construction. Exists to be measured, not played."""
import json

COLS, ROWS, LAYERS, SLOTS = 10, 40, 1, 5
COLORS = "YRBGO"
QCOLS, QDEPTH = 5, 4
AMMO = COLS * ROWS * LAYERS // (QCOLS * QDEPTH)

# The board is banded by colour so each colour owns the same number of cubes as its column
# owns ammo; bands run front to back, which is also the order the shooters clear them.
rows = []
for r in range(ROWS):
    rows.append(COLORS[(r * len(COLORS)) // ROWS] * COLS)

queue = []
for q in range(QCOLS):
    color = COLORS[q]
    queue.append({"shooters": [{"color": color, "ammo": AMMO, "hidden": d == QDEPTH - 1}
                               for d in range(QDEPTH)]})

level = {"boardLayers": [{"rows": rows} for _ in range(LAYERS)],
         "slotCount": SLOTS, "shooterColumns": queue}
print(json.dumps(level, indent=2))

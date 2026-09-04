"""Level_03 generator: the stress level behind Docs/PERFORMANCE.md, not a level anyone plays
for fun. 10x30x3 = 900 cubes, one colour per stack, 5 queue columns x 20 shooters = 100
shooters at 9 ammo each (ammo == cubes per colour). No scramble: every queue column is one
colour and there is one column per colour, so every colour is always at a front and the
level is winnable by construction. Hidden shooters sit at depth 1 and beyond, as the brief
wants.

Five columns, not ten: the queue is centred on x=0 at 2 units apart, the same spacing as
the five slots, so a sixth column starts leaving the frame. An earlier cut used ten and
half of its shooters spawned off-screen where nobody could tap them."""
import json, random

COLS, ROWS, LAYERS, SLOTS = 10, 30, 3, 5
COLORS = "YRBGO"
QCOLS, QDEPTH = 5, 20
AMMO = COLS * ROWS * LAYERS // (QCOLS * QDEPTH)
HIDDEN_EVERY = 7

rng = random.Random(3)
stacks = list(COLORS) * (COLS * ROWS // len(COLORS))
rng.shuffle(stacks)
ground = ["".join(stacks[r * COLS:(r + 1) * COLS]) for r in range(ROWS)]
queue = []
for q in range(QCOLS):
    color = COLORS[q % len(COLORS)]
    queue.append({"shooters": [{"color": color, "ammo": AMMO, "hidden": d > 0 and (q + d) % HIDDEN_EVERY == 0}
                               for d in range(QDEPTH)]})
level = {"boardLayers": [{"rows": ground} for _ in range(LAYERS)], "slotCount": SLOTS, "shooterColumns": queue}
print(json.dumps(level, indent=2))

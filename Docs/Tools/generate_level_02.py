"""Level_02 generator: 10x20x3, five colours, one colour per stack, ammo == cubes per colour.
Start from one queue column per colour (always winnable: every colour is at a front), then
scramble by swapping shooters across columns, keeping a swap only while a colour-aware player
still wins under random firing interleavings. Hard means the naive player (always the
leftmost front) loses. The sim mirrors GameLoop: leftmost matching exposed cube, five slots,
fail when every slot is occupied and targetless."""
import json, random, sys

COLS, ROWS, LAYERS, SLOTS, QCOLS = 10, 20, 3, 5, 5
COLORS = "YRBGO"
PER_COLOR = COLS * ROWS * LAYERS // len(COLORS)
AMMO_SIZES = (5, 10, 15, 20)

class Sim:
    def __init__(self, board, rng):
        self.board = board; self.rng = rng
        self.front = [0]*COLS; self.living = [LAYERS]*COLS
        self.slots = [None]*SLOTS; self.left = COLS*ROWS*LAYERS
    def exposed(self, c):
        return None if self.front[c] >= ROWS else self.board[self.living[c]-1][self.front[c]][c]
    def target(self, color):
        for c in range(COLS):
            if self.exposed(c) == color: return c
        return -1
    def remove(self, c):
        self.living[c] -= 1
        if self.living[c] == 0: self.front[c] += 1; self.living[c] = LAYERS
        self.left -= 1
    def fire_all(self):
        progress = True
        while progress:
            progress = False
            order = list(range(SLOTS)); self.rng.shuffle(order)
            for i in order:
                s = self.slots[i]
                if s is None: continue
                t = self.target(s[0])
                if t < 0: continue
                self.remove(t); s[1] -= 1; progress = True
                if s[1] == 0: self.slots[i] = None
    def stuck(self):
        return self.left > 0 and all(s is not None and self.target(s[0]) < 0 for s in self.slots)
    def seat(self, color, ammo):
        self.slots[self.slots.index(None)] = [color, ammo]
    def exposed_count(self, color):
        return sum(1 for c in range(COLS) if self.exposed(c) == color)
    def seated(self):
        return {s[0] for s in self.slots if s}

def make_board(rng):
    """One colour per stack: the three cubes standing on a cell all match. A stack of mixed
    colours needs three different shooters to clear one cell, which Salih found unplayable."""
    stacks = list(COLORS) * (COLS * ROWS // len(COLORS)); rng.shuffle(stacks)
    ground = [[stacks[r*COLS + c] for c in range(COLS)] for r in range(ROWS)]
    return [[row[:] for row in ground] for _ in range(LAYERS)]

def color_columns(rng):
    cols = []
    for c in COLORS:
        left, col = PER_COLOR, []
        while left:
            a = min(left, rng.choice(AMMO_SIZES)); col.append({"color": c, "ammo": a, "hidden": False}); left -= a
        cols.append(col)
    return cols

def play(board, queue, rng, smart):
    sim = Sim(board, rng); qfront = [0]*QCOLS
    while True:
        sim.fire_all()
        if sim.left == 0: return True
        if sim.stuck() or None not in sim.slots: return False
        fronts = [q for q in range(QCOLS) if qfront[q] < len(queue[q])]
        if not fronts: return False
        if smart:
            seated = sim.seated()
            def score(q):
                col = queue[q][qfront[q]]["color"]; ex = sim.exposed_count(col)
                nxt = queue[q][qfront[q]+1]["color"] if qfront[q]+1 < len(queue[q]) else None
                return (1000 if ex and col not in seated else 0) + (500 if ex else 0) \
                     + (100 if nxt and sim.exposed_count(nxt) and nxt not in seated else 0) + ex
            q = max(fronts, key=score)
        else:
            q = fronts[0]
        s = queue[q][qfront[q]]; qfront[q] += 1
        sim.seat(s["color"], s["ammo"])

def robust(board, queue, smart, trials):
    return all(play(board, queue, random.Random(t), smart) for t in range(trials))

def main():
    for seed in range(1, 100):
        rng = random.Random(seed)
        board = make_board(rng); queue = color_columns(rng)
        kept = 0
        for _ in range(600):
            a, b = rng.randrange(QCOLS), rng.randrange(QCOLS)
            if a == b: continue
            i, j = rng.randrange(len(queue[a])), rng.randrange(len(queue[b]))
            queue[a][i], queue[b][j] = queue[b][j], queue[a][i]
            if robust(board, queue, True, 8): kept += 1
            else: queue[a][i], queue[b][j] = queue[b][j], queue[a][i]
        if not robust(board, queue, True, 40): continue
        naive = robust(board, queue, False, 10)
        hidden = 0
        for col in queue:
            for s in col[1:]:
                if rng.random() < 0.15: s["hidden"] = True; hidden += 1
        mixed = sum(len({s["color"] for s in col}) for col in queue)
        print(f"seed {seed}: {kept} swaps kept, colours per column {[len({s['color'] for s in c}) for c in queue]}, "
              f"naive wins={naive}, {hidden} hidden, {sum(len(c) for c in queue)} shooters, "
              f"columns {[len(c) for c in queue]}", file=sys.stderr)
        if naive or hidden < 4: continue
        level = {"boardLayers": [{"rows": ["".join(r) for r in layer]} for layer in board],
                 "slotCount": SLOTS, "shooterColumns": [{"shooters": c} for c in queue]}
        print(json.dumps(level, indent=2)); return
    print("no seed found", file=sys.stderr)

main()

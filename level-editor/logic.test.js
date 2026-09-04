// logic.test.js - the level editor's pure logic against the game's own level files.
// Layer: level editor (outside Assets/; Unity never sees it).
// Responsibility: every mistake logic.js could make that the browser would not show -
//   a lenient parse that displays a broken file, a serialisation the Unity parser or git
//   would not recognise, a validation rule that fires on a shipped level or stays silent
//   on a broken one, an autofill that strands ammo, a simulation that always wins.
// NOT its responsibility: the DOM, the File System Access flow, or anything in index.html.
//   Those are verified by hand in Edge; this file runs with `node --test level-editor/`.
//
// The fixtures are the real files in Assets/00_GAME/Levels, read from disk, so the tests
// fail the day the game's format and the editor's disagree.

"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const Level = require("./logic.js");

/** Where the game's level files live, relative to this file. */
const LEVELS_DIR = path.join(__dirname, "..", "Assets", "00_GAME", "Levels");

/** Reads a shipped level as text with line endings normalised to LF (the working tree is CRLF on Windows). */
function readLevel(name) {
  return fs.readFileSync(path.join(LEVELS_DIR, name), "utf8").replace(/\r\n/g, "\n");
}

// --- parse ---------------------------------------------------------------------------

test("parse: a file without boardLayers is refused, naming the key", () => {
  assert.throws(() => Level.parse('{"slotCount": 5}'), /boardLayers/);
});

test("parse: a ragged ground row is refused", () => {
  const text = JSON.stringify({ boardLayers: [{ rows: ["YYY", "YY"] }], slotCount: 5, shooterColumns: [] });
  assert.throws(() => Level.parse(text), /rows\[1\]/);
});

test("parse: an unknown letter is refused, and so is a lowercase one", () => {
  const withX = JSON.stringify({ boardLayers: [{ rows: ["YYX"] }], slotCount: 5, shooterColumns: [] });
  const lower = JSON.stringify({ boardLayers: [{ rows: ["YYr"] }], slotCount: 5, shooterColumns: [] });
  assert.throws(() => Level.parse(withX), /'X'/);
  assert.throws(() => Level.parse(lower), /'r'/);
});

test("parse: a multi-layer file reports its layer count and returns the ground layer verbatim", () => {
  const text = readLevel("Level_02.json");
  const raw = JSON.parse(text);
  const { level, layerCount } = Level.parse(text);
  assert.equal(layerCount, 3);
  assert.deepEqual(level.rows, raw.boardLayers[0].rows);
});

// --- serialize -----------------------------------------------------------------------

test("serialize: a generated level round-trips byte for byte", () => {
  const text = readLevel("Level_04.json");
  assert.equal(Level.serialize(Level.parse(text).level), text);
});

test("serialize: the hand-written sample level survives a parse-serialize-parse round trip", () => {
  const text = readLevel("Level_01.json");
  const once = Level.parse(text).level;
  const twice = Level.parse(Level.serialize(once)).level;
  assert.deepEqual(twice, once);
});

test("serialize: a multi-layer source writes one layer and leaves the queue untouched", () => {
  const text = readLevel("Level_02.json");
  const raw = JSON.parse(text);
  const written = JSON.parse(Level.serialize(Level.parse(text).level));
  assert.equal(written.boardLayers.length, 1);
  assert.deepEqual(written.boardLayers[0].rows, raw.boardLayers[0].rows);
  assert.deepEqual(written.shooterColumns, raw.shooterColumns);
  assert.equal(written.slotCount, raw.slotCount);
});

// --- validate: the parser's rules, then LevelFileTests' ------------------------------

/**
 * The smallest level that passes every rule: all five colours once, ammo equal to cubes,
 * one hidden shooter behind a front, five slots. Each validate test breaks exactly one
 * thing in a copy of it.
 */
function validLevel() {
  return {
    rows: ["YRBGO"],
    slotCount: 5,
    columns: [
      [{ color: "Y", ammo: 1, hidden: false }, { color: "R", ammo: 1, hidden: true }],
      [{ color: "B", ammo: 1, hidden: false }],
      [{ color: "G", ammo: 1, hidden: false }],
      [{ color: "O", ammo: 1, hidden: false }],
    ],
  };
}

/** The error codes validate raises for a level, in order. */
function errorCodes(level) {
  return Level.validate(level).filter((issue) => issue.severity === "error").map((issue) => issue.code);
}

test("validate: the shipped levels and the minimal fixture raise no errors", () => {
  assert.deepEqual(errorCodes(Level.parse(readLevel("Level_01.json")).level), []);
  assert.deepEqual(errorCodes(Level.parse(readLevel("Level_04.json")).level), []);
  assert.deepEqual(errorCodes(validLevel()), []);
});

test("validate: an empty board is an error, not a crash", () => {
  const level = validLevel();
  level.rows = [];
  assert.ok(errorCodes(level).includes("E_EMPTY_BOARD"));
});

test("validate: no shooter columns at all is an error", () => {
  const level = validLevel();
  level.columns = [];
  assert.ok(errorCodes(level).includes("E_NO_COLUMNS"));
});

test("validate: a column with no shooters is an error that names the column", () => {
  const level = validLevel();
  level.columns[2] = [];
  const issue = Level.validate(level).find((i) => i.code === "E_EMPTY_COLUMN");
  assert.ok(issue, "E_EMPTY_COLUMN was not raised");
  assert.match(issue.message, /column 3/);
});

test("validate: a shooter with no ammo is an error that names its place", () => {
  const level = validLevel();
  level.columns[1][0].ammo = 0;
  const issue = Level.validate(level).find((i) => i.code === "E_AMMO");
  assert.ok(issue, "E_AMMO was not raised");
  assert.match(issue.message, /column 2/);
});

test("validate: zero slots is an error", () => {
  const level = validLevel();
  level.slotCount = 0;
  assert.ok(errorCodes(level).includes("E_SLOTS"));
});

// --- stats ---------------------------------------------------------------------------

test("stats: counts cubes and ammo per colour with every colour present, zeros included", () => {
  const level = {
    rows: ["YYR", "BBG"],
    slotCount: 5,
    columns: [[{ color: "Y", ammo: 5, hidden: false }, { color: "O", ammo: 3, hidden: true }]],
  };
  assert.deepEqual(Level.stats(level), {
    cubes: { Y: 2, R: 1, B: 2, G: 1, O: 0 },
    ammo: { Y: 5, R: 0, B: 0, G: 0, O: 3 },
  });
});

// --- validate: LevelFileTests' rules and the advisory warnings -----------------------

/** The warning codes validate raises for a level, in order. */
function warningCodes(level) {
  return Level.validate(level).filter((issue) => issue.severity === "warning").map((issue) => issue.code);
}

test("validate: a board missing one of the five colours is a warning naming it, never an error", () => {
  const level = validLevel();
  level.rows = ["YRBG"];
  level.columns.pop(); // drop the orange shooter too, so only the board is at fault
  const issue = Level.validate(level).find((i) => i.code === "W_MISSING_COLOUR");
  assert.ok(issue, "W_MISSING_COLOUR was not raised");
  assert.equal(issue.severity, "warning");
  assert.match(issue.message, /Orange/);
  assert.deepEqual(errorCodes(level), []); // a three-colour level saves
});

test("validate: a colour with less ammo than cubes is an error naming both numbers", () => {
  const level = validLevel();
  level.rows = ["YYRBGO"]; // two yellow cubes, one yellow shot
  const issue = Level.validate(level).find((i) => i.code === "E_UNDER_AMMO");
  assert.ok(issue, "E_UNDER_AMMO was not raised");
  assert.match(issue.message, /Yellow/);
  assert.match(issue.message, /2 cubes/);
  assert.match(issue.message, /1 /);
});

test("validate: no hidden shooter anywhere is a warning, never an error", () => {
  const level = validLevel();
  level.columns[0][1].hidden = false;
  assert.ok(warningCodes(level).includes("W_NO_HIDDEN"));
  assert.deepEqual(errorCodes(level), []); // a level with nothing hidden saves
});

test("validate: a sixth shooter column is an error", () => {
  const level = validLevel();
  while (level.columns.length < 6) level.columns.push([{ color: "Y", ammo: 1, hidden: false }]);
  assert.ok(errorCodes(level).includes("E_TOO_MANY_COLUMNS"));
});

test("validate: one shot too many is a warning, one too few is an error - never both", () => {
  const over = validLevel();
  over.columns[0][0].ammo = 2; // yellow: 1 cube, 2 shots
  assert.ok(warningCodes(over).includes("W_OVER_AMMO"));
  assert.ok(!errorCodes(over).includes("E_UNDER_AMMO"));

  const under = validLevel();
  under.rows = ["YYRBGO"]; // yellow: 2 cubes, 1 shot
  assert.ok(errorCodes(under).includes("E_UNDER_AMMO"));
  assert.ok(!warningCodes(under).includes("W_OVER_AMMO"));
});

test("validate: a shooter whose colour has no cube is named once, not also as over-ammo", () => {
  const level = validLevel();
  level.rows = ["YRBG"]; // orange gone from the board; its shooter stays
  const issues = Level.validate(level);
  const orphan = issues.find((i) => i.code === "W_ORPHAN_COLOUR");
  assert.ok(orphan, "W_ORPHAN_COLOUR was not raised");
  assert.match(orphan.message, /Orange/);
  assert.ok(!issues.some((i) => i.code === "W_OVER_AMMO" && /Orange/.test(i.message)));
});

test("validate: a hidden shooter at the front is a warning; behind the front it is not", () => {
  const front = validLevel();
  front.columns[0][0].hidden = true;
  assert.ok(warningCodes(front).includes("W_HIDDEN_FRONT"));
  assert.ok(!warningCodes(validLevel()).includes("W_HIDDEN_FRONT"));
});

test("validate: a board that is not 10x10 and a slot count that is not 5 warn; the brief's numbers do not", () => {
  assert.ok(warningCodes(validLevel()).includes("W_SIZE"));
  assert.ok(!warningCodes(validLevel()).includes("W_SLOTS"));

  const brief = validLevel();
  brief.rows = Array.from({ length: 10 }, () => "YRBGOYRBGO");
  assert.ok(!warningCodes(brief).includes("W_SIZE"));

  const fourSlots = validLevel();
  fourSlots.slotCount = 4;
  assert.ok(warningCodes(fourSlots).includes("W_SLOTS"));
});

// --- board helpers and file names ----------------------------------------------------

test("resizeBoard: growing keeps every cell and fills the new ones with the given colour", () => {
  assert.deepEqual(Level.resizeBoard(["YR", "BG"], 3, 3, "O"), ["YRO", "BGO", "OOO"]);
});

test("resizeBoard: shrinking drops the back rows and the rightmost columns, never the front", () => {
  assert.deepEqual(Level.resizeBoard(["YRB", "GOY", "RBG"], 2, 2), ["YR", "GO"]);
});

test("paintCell: returns new rows with one cell changed and leaves the input alone", () => {
  const before = ["YY", "YY"];
  const after = Level.paintCell(before, 1, 0, "R");
  assert.deepEqual(after, ["YY", "RY"]);
  assert.deepEqual(before, ["YY", "YY"]);
});

test("nextFreeName: fills the first gap, counts from 01, and ignores case", () => {
  assert.equal(Level.nextFreeName([]), "Level_01.json");
  assert.equal(Level.nextFreeName(["Level_01.json", "Level_02.json"]), "Level_03.json");
  assert.equal(Level.nextFreeName(["Level_01.json", "Level_03.json"]), "Level_02.json");
  assert.equal(Level.nextFreeName(["level_01.JSON"]), "Level_02.json");
});

// --- autofill ------------------------------------------------------------------------

/** Ammo per colour summed over a queue, and the count of hidden shooters. */
function queueTotals(columns) {
  const ammo = {};
  let hidden = 0;
  for (const shooters of columns) {
    for (const shooter of shooters) {
      ammo[shooter.color] = (ammo[shooter.color] || 0) + shooter.ammo;
      if (shooter.hidden) hidden += 1;
    }
  }
  return { ammo, hidden };
}

test("autofill: 45 cubes become shooters of 20, 20 and 5 - the remainder last, never dropped", () => {
  const rows = Array.from({ length: 5 }, () => "YYYYYYYYY"); // 45 yellow
  const columns = Level.autofill(rows, 1);
  assert.deepEqual(columns[0].map((s) => s.ammo), [20, 20, 5]);
});

test("autofill: every colour gets exactly as many shots as it has cubes, and no shooter is empty", () => {
  const { level } = Level.parse(readLevel("Level_04.json"));
  const columns = Level.autofill(level.rows, 5);
  const { cubes } = Level.stats(level);
  assert.deepEqual(queueTotals(columns).ammo, cubes);
  assert.ok(columns.every((shooters) => shooters.every((s) => s.ammo >= 1)));
});

test("autofill: the colour the front row needs first is at the front of column 1", () => {
  const columns = Level.autofill(["OOO", "RRR", "YYY"], 3); // orange in front, yellow at the back
  assert.equal(columns[0][0].color, "O");
  assert.notEqual(columns[0][0].color, "Y");
});

test("autofill: exactly one shooter is hidden and it sits behind a front; a one-deep queue gets none", () => {
  const deep = Level.autofill(Level.parse(readLevel("Level_04.json")).level.rows, 5);
  assert.equal(queueTotals(deep).hidden, 1);
  assert.ok(deep.every((shooters) => !shooters[0].hidden), "a front shooter was hidden");

  const shallow = Level.autofill(["YRBGO"], 5); // five single-shot shooters, five columns
  assert.equal(queueTotals(shallow).hidden, 0);
});

test("autofill: uses the columns asked for, but never leaves one empty", () => {
  const rows = Level.parse(readLevel("Level_04.json")).level.rows;
  assert.equal(Level.autofill(rows, 3).length, 3);
  assert.equal(Level.autofill(["YYRR"], 5).length, 2);
  assert.ok(Level.autofill(["YYRR"], 5).every((shooters) => shooters.length > 0));
});

// --- simulate ------------------------------------------------------------------------

/**
 * A level every rule accepts that the greedy player still loses: one slot, one column,
 * and the only front shooter is a colour the front row does not expose.
 */
function stuckLevel() {
  return {
    rows: ["RRRRR", "YBGOY"],
    slotCount: 1,
    columns: [[
      { color: "Y", ammo: 2, hidden: false },
      { color: "R", ammo: 5, hidden: true },
      { color: "B", ammo: 1, hidden: false },
      { color: "G", ammo: 1, hidden: false },
      { color: "O", ammo: 1, hidden: false },
    ]],
  };
}

test("simulate: the shipped sample level is won with nothing left", () => {
  const { level } = Level.parse(readLevel("Level_01.json"));
  assert.deepEqual(Level.simulate(level), { won: true, cubesLeft: 0 });
  assert.ok(!warningCodes(level).includes("W_SIM_STUCK"));
});

test("simulate: a legal level the greedy player cannot finish is reported stuck, and validate warns", () => {
  const result = Level.simulate(stuckLevel());
  assert.equal(result.won, false);
  assert.ok(result.cubesLeft > 0);
  assert.deepEqual(errorCodes(stuckLevel()), []);
  assert.ok(warningCodes(stuckLevel()).includes("W_SIM_STUCK"));
});

test("simulate: seats the front that can fire, not the leftmost one", () => {
  const level = {
    rows: ["RR"],
    slotCount: 1,
    columns: [[{ color: "Y", ammo: 1, hidden: false }], [{ color: "R", ammo: 2, hidden: false }]],
  };
  assert.equal(Level.simulate(level).won, true);
});

test("autofill: when every chunk would sit at a front, one is split so a hidden shooter exists", () => {
  const { level } = Level.parse(readLevel("Level_01.json")); // five colours, twenty cubes each
  const columns = Level.autofill(level.rows, 5);
  const totals = queueTotals(columns);
  assert.equal(totals.hidden, 1);
  assert.ok(columns.every((shooters) => !shooters[0].hidden), "a front shooter was hidden");
  assert.deepEqual(totals.ammo, Level.stats(level).cubes); // the split changes no total
});

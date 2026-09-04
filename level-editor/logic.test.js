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

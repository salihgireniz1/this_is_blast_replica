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

// logic.js - the level editor's pure logic: the file contract, the rules, the helpers.
// Layer: level editor (outside Assets/; Unity never sees it).
// Responsibility: everything about a level that is true without a screen - reading the
//   game's JSON into an editable object, writing it back in the exact shape the Unity
//   parser and the generators produce, and (in later chunks) validating it against the
//   same rules LevelParser and LevelFileTests enforce, filling a queue from a board, and
//   simulating a greedy player.
// NOT its responsibility: the DOM, files, folders or permissions. index.html owns those.
//   Nothing in this file may touch `document` or `window`; that is what lets
//   logic.test.js run it under Node against the real level files.
//
// A classic script, not a module: Chromium refuses <script type="module"> from file://,
// and the page is opened by double-click. The CommonJS guard at the bottom is what Node
// uses; the browser just sees the `Level` global.
//
// The file contract, mirrored from Assets/00_GAME/Scripts/Infrastructure/LevelDefinition.cs:
//   { "boardLayers": [ { "rows": ["RRRRRBBBBB", ...] } ],   ground layer first
//     "slotCount": 5,
//     "shooterColumns": [ { "shooters": [ { "color": "R", "ammo": 10, "hidden": false } ] } ] }
// rows[0] is the FRONT row (nearest the shooters); shooters[0] is depth 0, the tappable
// front. Width is rows[0].length; there are no size keys. The editor's own shape drops the
// wrapper objects (they exist only because JsonUtility cannot read jagged arrays):
//   level = { rows: string[], slotCount: number, columns: Shooter[][] }
//   Shooter = { color: "Y"|"R"|"B"|"G"|"O", ammo: number, hidden: boolean }

"use strict";

const Level = (() => {
  // --- Constants ---------------------------------------------------------------------

  /** The five colour letters the game knows, in BlastColor order. Case-sensitive. */
  const COLOURS = "YRBGO";

  /**
   * Parses the game's JSON into the editor's level shape. Throws only for what the editor
   * could not display; everything else is left for validate to flag.
   * @param {string} text The file's contents.
   * @returns {{ level: { rows: string[], slotCount: number, columns: object[][] }, layerCount: number }}
   *   The ground layer as an editable level, plus how many layers the file held (upper layers are dropped).
   */
  function parse(text) {
    let raw;
    try {
      raw = JSON.parse(text);
    } catch (error) {
      throw new Error(`Not a JSON file: ${error.message}`);
    }
    if (raw === null || typeof raw !== "object") {
      throw new Error("The level is not a JSON object.");
    }

    const layers = raw.boardLayers;
    if (!Array.isArray(layers) || layers.length === 0) {
      throw new Error("The level has no boardLayers.");
    }
    const groundRows = layers[0] && layers[0].rows;
    if (!Array.isArray(groundRows) || groundRows.length === 0) {
      throw new Error("boardLayers[0] has no rows.");
    }

    // Width is the first row's length; every other row must agree, or there is no grid to draw.
    const width = typeof groundRows[0] === "string" ? groundRows[0].length : -1;
    const rows = groundRows.map((row, index) => {
      if (typeof row !== "string" || row.length !== width) {
        const got = typeof row === "string" ? row.length : "no";
        throw new Error(`boardLayers[0].rows[${index}] has ${got} letters where rows[0] has ${width}; every row must be the same width.`);
      }
      for (let column = 0; column < row.length; column++) {
        if (!COLOURS.includes(row[column])) {
          throw new Error(`boardLayers[0].rows[${index}][${column}] holds '${row[column]}'; the colour letters are Y, R, B, G, O.`);
        }
      }
      return row;
    });

    // A missing queue is a level the editor can show (as empty columns); validate flags it.
    const rawColumns = Array.isArray(raw.shooterColumns) ? raw.shooterColumns : [];
    const columns = rawColumns.map((column, columnIndex) => {
      const shooters = column && Array.isArray(column.shooters) ? column.shooters : [];
      return shooters.map((shooter, depth) => {
        const where = `shooterColumns[${columnIndex}].shooters[${depth}]`;
        const color = shooter && shooter.color;
        if (typeof color !== "string" || color.length !== 1 || !COLOURS.includes(color)) {
          throw new Error(`${where} has colour '${color}'; the colour letters are Y, R, B, G, O.`);
        }
        const ammo = shooter.ammo;
        if (!Number.isInteger(ammo)) {
          throw new Error(`${where} has ammo '${ammo}'; ammo must be a whole number.`);
        }
        return { color, ammo, hidden: Boolean(shooter.hidden) };
      });
    });

    const slotCount = Number.isInteger(raw.slotCount) ? raw.slotCount : 0;

    return { level: { rows, slotCount, columns }, layerCount: layers.length };
  }

  /**
   * Writes a level in the exact shape the Unity parser reads and the Python generators emit.
   * Key order and indentation match `json.dumps(level, indent=2)` so a generated file
   * round-trips byte for byte and every later git diff is one value per line.
   * @param {{ rows: string[], slotCount: number, columns: object[][] }} level The editor's level.
   * @returns {string} The file contents, LF line endings, trailing newline.
   */
  function serialize(level) {
    const document = {
      boardLayers: [{ rows: level.rows }],
      slotCount: level.slotCount,
      shooterColumns: level.columns.map((shooters) => ({
        shooters: shooters.map((shooter) => ({
          color: shooter.color,
          ammo: shooter.ammo,
          hidden: shooter.hidden,
        })),
      })),
    };
    return JSON.stringify(document, null, 2) + "\n";
  }

  /**
   * Counts the cubes on the board and the ammo in the queue, per colour. Every colour key
   * is always present (zero when absent) so a table can render without guards.
   * @param {{ rows: string[], columns: object[][] }} level The editor's level.
   * @returns {{ cubes: object, ammo: object }} Letter -> count, for all five letters.
   */
  function stats(level) {
    const perColour = () => Object.fromEntries([...COLOURS].map((letter) => [letter, 0]));
    const cubes = perColour();
    const ammo = perColour();
    for (const row of level.rows) {
      for (const letter of row) {
        if (letter in cubes) cubes[letter] += 1;
      }
    }
    for (const shooters of level.columns) {
      for (const shooter of shooters) {
        if (shooter.color in ammo) ammo[shooter.color] += shooter.ammo;
      }
    }
    return { cubes, ammo };
  }

  /**
   * Checks a level against the same rules the game enforces: first what LevelParser
   * refuses (an error here is a FormatException there), then what LevelFileTests asserts
   * per shipped file. Errors must block saving - the file would break Unity or the test
   * suite; warnings are advice. Column and position numbers in messages are 1-based, the
   * way the designer counts them on screen.
   * @param {{ rows: string[], slotCount: number, columns: object[][] }} level The editor's level.
   * @returns {{ severity: "error"|"warning", code: string, message: string }[]} Issues in rule order.
   */
  function validate(level) {
    const issues = [];
    const error = (code, message) => issues.push({ severity: "error", code, message });

    // LevelParser: a board must have at least one row and one column.
    if (level.rows.length === 0 || level.rows[0].length === 0) {
      error("E_EMPTY_BOARD", "The board has no cells.");
    }

    // LevelParser: "The level has no shooterColumns."
    if (level.columns.length === 0) {
      error("E_NO_COLUMNS", "There are no shooter columns; add at least one.");
    }

    level.columns.forEach((shooters, columnIndex) => {
      const column = columnIndex + 1;

      // LevelParser: "shooterColumns[n] has no shooters."
      if (shooters.length === 0) {
        error("E_EMPTY_COLUMN", `Shooter column ${column} has no shooters.`);
      }

      // LevelParser: zero ammo would seat a shooter that SlotRow reads as an empty slot.
      shooters.forEach((shooter, depth) => {
        if (!Number.isInteger(shooter.ammo) || shooter.ammo < 1) {
          error("E_AMMO", `The shooter at column ${column}, position ${depth + 1} has ammo ${shooter.ammo}; a shooter needs at least one shot.`);
        }
      });
    });

    // LevelParser: "slotCount is n; a level needs at least one slot."
    if (!Number.isInteger(level.slotCount) || level.slotCount < 1) {
      error("E_SLOTS", `Slot count is ${level.slotCount}; a level needs at least one slot.`);
    }

    return issues;
  }

  return { COLOURS, parse, serialize, stats, validate };
})();

if (typeof module !== "undefined") {
  module.exports = Level;
}

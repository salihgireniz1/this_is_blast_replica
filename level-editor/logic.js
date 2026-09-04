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

  return { COLOURS, parse, serialize };
})();

if (typeof module !== "undefined") {
  module.exports = Level;
}

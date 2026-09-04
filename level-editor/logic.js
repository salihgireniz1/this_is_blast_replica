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

  /** The colour each letter stands for, for messages a designer reads. */
  const COLOUR_NAMES = { Y: "Sarı", R: "Kırmızı", B: "Mavi", G: "Yeşil", O: "Turuncu" };

  /**
   * How many shooter columns fit across the dock. The queue is centred on x = 0 at the
   * slots' spacing, so a sixth column spawns off-screen where nobody can tap it
   * (LevelFileTests.MaxQueueColumns).
   */
  const MAX_QUEUE_COLUMNS = 5;

  /** The case brief fixes the board at 10x10 (slide 3, Not1). Other sizes parse; they warn. */
  const BRIEF_BOARD_SIZE = 10;

  /** The case brief fixes the slot row at five (slide 3, Not2). Other counts parse; they warn. */
  const BRIEF_SLOT_COUNT = 5;

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

    // Every cube on a cell shares its colour (the rule the generators follow, and what the
    // game plays best), so a layer is a count and the board shown is the ground layer. A
    // file whose upper layers differ cannot be shown faithfully; the page refuses to
    // overwrite such a file in place.
    const uniform = layers.every((layer) => layer && Array.isArray(layer.rows)
      && layer.rows.length === rows.length && layer.rows.every((row, index) => row === rows[index]));

    return { level: { rows, layers: layers.length, slotCount, columns }, uniform };
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
      boardLayers: Array.from({ length: level.layers ?? 1 }, () => ({ rows: level.rows })),
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
    const layers = level.layers ?? 1; // a cell holds one cube per layer, all the same colour
    for (const row of level.rows) {
      for (const letter of row) {
        if (letter in cubes) cubes[letter] += layers;
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
      error("E_EMPTY_BOARD", "Tahtada hücre yok.");
    }

    // LevelParser: "The level has no boardLayers."
    const layers = level.layers ?? 1;
    if (!Number.isInteger(layers) || layers < 1) {
      error("E_LAYERS", `Katman sayısı ${layers}; en az bir katman gerekir.`);
    }

    // LevelParser: "The level has no shooterColumns."
    if (level.columns.length === 0) {
      error("E_NO_COLUMNS", "Hiç shooter sütunu yok; en az bir tane ekle.");
    }

    level.columns.forEach((shooters, columnIndex) => {
      const column = columnIndex + 1;

      // LevelParser: "shooterColumns[n] has no shooters."
      if (shooters.length === 0) {
        error("E_EMPTY_COLUMN", `Shooter sütunu ${column} boş.`);
      }

      // LevelParser: zero ammo would seat a shooter that SlotRow reads as an empty slot.
      shooters.forEach((shooter, depth) => {
        if (!Number.isInteger(shooter.ammo) || shooter.ammo < 1) {
          error("E_AMMO", `Sütun ${column}, sıra ${depth + 1}'deki shooter'ın mermisi ${shooter.ammo}; bir shooter'ın en az bir mermisi olmalı.`);
        }
      });
    });

    // LevelParser: "slotCount is n; a level needs at least one slot."
    const slotsValid = Number.isInteger(level.slotCount) && level.slotCount >= 1;
    if (!slotsValid) {
      error("E_SLOTS", `Slot sayısı ${level.slotCount}; en az bir slot gerekir.`);
    }

    const { cubes, ammo } = stats(level);
    const name = (letter) => `${COLOUR_NAMES[letter]} (${letter})`;

    for (const letter of COLOURS) {
      // LevelFileTests: ammo >= cubes per colour. Under-ammo only shows at the very end of
      // a playthrough, when the last shooter of that colour has left and cubes still stand.
      if (cubes[letter] > 0 && ammo[letter] < cubes[letter]) {
        error("E_UNDER_AMMO", `${name(letter)}: ${cubes[letter]} küp var ama sadece ${ammo[letter]} mermi; level kazanılamaz.`);
      }
    }

    // LevelFileTests: the dock is five columns wide.
    if (level.columns.length > MAX_QUEUE_COLUMNS) {
      error("E_TOO_MANY_COLUMNS", `${level.columns.length} shooter sütunu var, dock'a en fazla ${MAX_QUEUE_COLUMNS} sığar; fazlası ekran dışında doğar, kimse dokunamaz.`);
    }

    // --- Warnings: allowed, but the designer should know. ---
    const warning = (code, message) => issues.push({ severity: "warning", code, message });

    const width = level.rows.length > 0 ? level.rows[0].length : 0;
    if (width !== BRIEF_BOARD_SIZE || level.rows.length !== BRIEF_BOARD_SIZE) {
      warning("W_SIZE", `Tahta ${width}x${level.rows.length}; case ${BRIEF_BOARD_SIZE}x${BRIEF_BOARD_SIZE} istiyor.`);
    }

    if (slotsValid && level.slotCount !== BRIEF_SLOT_COUNT) {
      warning("W_SLOTS", `Slot sayısı ${level.slotCount}; case ${BRIEF_SLOT_COUNT} istiyor.`);
    }

    if (layers > 1) {
      warning("W_LAYERS", `Her hücrede ${layers} katman küp; case'in örnek leveli tek katman.`);
    }

    // All five colours and a hidden shooter are the brief's rules for the sample level only
    // (LevelFileTests scopes both to Level_01); anywhere else they are design choices.
    const missing = [...COLOURS].filter((letter) => cubes[letter] === 0);
    if (missing.length > 0) {
      warning("W_MISSING_COLOUR", `Tahtada yok: ${missing.map(name).join(", ")}. Case'in örnek levelinde beş renk de olmalı; diğer leveller atlayabilir.`);
    }
    if (!level.columns.some((shooters) => shooters.some((shooter) => shooter.hidden))) {
      warning("W_NO_HIDDEN", "Hiç hidden shooter yok. Case'in örnek levelinde bir tane olmalı; diğer levellerde şart değil.");
    }

    for (const letter of COLOURS) {
      // A shooter whose colour never appears can never fire: it seats and sits forever.
      if (cubes[letter] === 0 && ammo[letter] > 0) {
        warning("W_ORPHAN_COLOUR", `${name(letter)} shooter'ların vuracak küpü yok; slota oturan bir daha çıkmaz.`);
      } else if (ammo[letter] > cubes[letter]) {
        // Legal (the test only asks for >=), but the last shooter of this colour keeps its
        // leftover shots and its slot until the level ends - the failure the brief describes.
        warning("W_OVER_AMMO", `${name(letter)}: küpten ${ammo[letter] - cubes[letter]} fazla mermi; mermisi kalan ama hedefi olmayan shooter slotta kalır.`);
      }
    }

    // A hidden front is revealed the instant the level starts; the feature is wasted on it.
    level.columns.forEach((shooters, columnIndex) => {
      if (shooters.length > 0 && shooters[0].hidden) {
        warning("W_HIDDEN_FRONT", `Sütun ${columnIndex + 1}'in öndeki shooter'ı hidden; level başlar başlamaz açığa çıkar.`);
      }
    });

    // Ammo >= cubes is necessary, not sufficient: the order can still deadlock. Only a
    // legal level is worth simulating - a broken one's result would mean nothing.
    if (!issues.some((issue) => issue.severity === "error")) {
      const played = simulate(level);
      if (!played.won) {
        warning("W_SIM_STUCK", `Açgözlü simülasyon ${played.cubesLeft} küp kala takıldı; dikkatli bir oyuncu yine kazanabilir, sırayı kontrol et.`);
      }
    }

    return issues;
  }

  /** How shipped levels are named: Level_ plus at least two digits. */
  const LEVEL_NAME_PREFIX = "Level_";
  const LEVEL_NAME_DIGITS = 2;

  /**
   * Returns the board at a new size. Every existing cell is kept; new cells take `fill`;
   * surplus rows go from the BACK so rows[0] stays the front, surplus columns from the right.
   * @param {string[]} rows The board, front row first.
   * @param {number} width Columns wanted.
   * @param {number} height Rows wanted.
   * @param {string} [fill] The letter new cells take; the first colour when omitted.
   * @returns {string[]} A new board; the input is not touched.
   */
  function resizeBoard(rows, width, height, fill = COLOURS[0]) {
    const result = [];
    for (let row = 0; row < height; row++) {
      const source = row < rows.length ? rows[row] : "";
      result.push((source + fill.repeat(width)).slice(0, width));
    }
    return result;
  }

  /**
   * Returns the board with one cell recoloured. Rows are strings, so the change is one
   * slice-concat and the input rows are never mutated - the page re-renders from the result.
   * @param {string[]} rows The board, front row first.
   * @param {number} row Which row.
   * @param {number} column Which column.
   * @param {string} letter The colour to paint.
   * @returns {string[]} A new board.
   */
  function paintCell(rows, row, column, letter) {
    return rows.map((cells, index) =>
      index === row ? cells.slice(0, column) + letter + cells.slice(column + 1) : cells);
  }

  /**
   * The first Level_NN.json not in the folder, counting from 01. Case-insensitive, because
   * the file system the folder lives on is.
   * @param {string[]} names The file names already in the folder.
   * @returns {string} A free name.
   */
  function nextFreeName(names) {
    const taken = new Set(names.map((name) => name.toLowerCase()));
    for (let number = 1; ; number++) {
      const name = `${LEVEL_NAME_PREFIX}${String(number).padStart(LEVEL_NAME_DIGITS, "0")}.json`;
      if (!taken.has(name.toLowerCase())) return name;
    }
  }

  /** The most shots one autofilled shooter carries; the remainder goes to the last one. */
  const AUTOFILL_CHUNK = 20;

  /**
   * Builds a queue for a board. Every colour gets exactly as many shots as it has cubes,
   * in chunks of AUTOFILL_CHUNK with the remainder last, so no shooter is ever left holding
   * shots with nothing to hit. Colours the front rows need first are dealt first, and
   * shooters are dealt round-robin across the columns, so the earliest-needed colours end
   * up at the fronts. One shooter behind a front is hidden, when the queue is deep enough.
   * @param {string[]} rows The board, front row first.
   * @param {number} columnCount Columns wanted (clamped to 1..MAX_QUEUE_COLUMNS).
   * @param {number} [layers] Cubes per cell, all of the cell's colour; one when omitted.
   * @returns {object[][]} A new queue, columns of shooters front-first.
   */
  function autofill(rows, columnCount, layers = 1) {
    const wanted = Math.max(1, Math.min(MAX_QUEUE_COLUMNS, columnCount || MAX_QUEUE_COLUMNS));

    const cubes = {};
    const firstRow = {};
    rows.forEach((row, rowIndex) => {
      for (const letter of row) {
        if (!(letter in cubes)) {
          cubes[letter] = 0;
          firstRow[letter] = rowIndex;
        }
        cubes[letter] += layers;
      }
    });

    // Needed first, then biggest, then the game's own colour order - fully deterministic.
    const order = Object.keys(cubes).sort((a, b) =>
      firstRow[a] - firstRow[b] || cubes[b] - cubes[a] || COLOURS.indexOf(a) - COLOURS.indexOf(b));

    const dealt = [];
    for (const letter of order) {
      let left = cubes[letter];
      while (left > 0) {
        const ammo = Math.min(AUTOFILL_CHUNK, left);
        dealt.push({ color: letter, ammo, hidden: false });
        left -= ammo;
      }
    }

    // Never more columns than shooters, or a column would be empty and the parser refuses it.
    const columnsUsed = Math.min(wanted, dealt.length);

    // When every shooter would be a front (five colours of twenty, say), nothing could be
    // hidden without wasting it on a front. Split the last chunk so one shooter sits behind:
    // the totals do not change, and the case's hidden requirement is met every time.
    if (dealt.length > 0 && dealt.length <= columnsUsed) {
      const last = dealt[dealt.length - 1];
      if (last.ammo >= 2) {
        const half = Math.floor(last.ammo / 2);
        last.ammo -= half;
        dealt.push({ color: last.color, ammo: half, hidden: false });
      }
    }

    const columns = Array.from({ length: columnsUsed }, () => []);
    dealt.forEach((shooter, index) => columns[index % columns.length].push(shooter));

    // The shooter at column 1, position 2: revealed the moment the first front is taken,
    // so the feature shows early, and never a front, where hiding would be pointless.
    if (dealt.length > columns.length) {
      dealt[columns.length].hidden = true;
    }

    return columns;
  }

  /** Seat scores for the greedy player, ported from Docs/Tools/generate_level_02.py's smart player. */
  const SCORE_EXPOSED_AND_NEW = 1000;
  const SCORE_EXPOSED = 500;
  const SCORE_NEXT_USEFUL = 100;

  /**
   * Plays the level as a greedy but colour-aware player would, ported from the generator's
   * `Sim` + `play(smart=True)` with the random firing order made deterministic: fire every
   * seated shooter that has a target (leftmost front cube of its colour, as GameRules does),
   * then seat the queue front that scores best - exposed and not already seated first, then
   * exposed, then one whose successor would be useful. Advisory only: Level_02 is designed so
   * the naive player loses, and a careful human may win what this player cannot.
   * @param {{ rows: string[], slotCount: number, columns: object[][] }} level The editor's level.
   * @returns {{ won: boolean, cubesLeft: number }} Whether the board emptied, and what remained.
   */
  function simulate(level) {
    const width = level.rows.length > 0 ? level.rows[0].length : 0;
    const height = level.rows.length;
    const layers = Math.max(1, level.layers | 0);
    const front = new Array(width).fill(0);
    const living = new Array(width).fill(layers); // cubes still standing on the front cell
    let left = width * height * layers;
    const slots = new Array(Math.max(0, level.slotCount | 0)).fill(null);
    const queueFront = level.columns.map(() => 0);

    // Every cube on a cell shares its colour, so the front cell's colour holds until its
    // last cube goes; only then does the row behind become the front.
    const exposed = (column) => (front[column] >= height ? null : level.rows[front[column]][column]);
    const remove = (column) => {
      living[column] -= 1;
      if (living[column] === 0) {
        front[column] += 1;
        living[column] = layers;
      }
      left -= 1;
    };
    const target = (color) => {
      for (let column = 0; column < width; column++) {
        if (exposed(column) === color) return column;
      }
      return -1;
    };
    const exposedCount = (color) => {
      let count = 0;
      for (let column = 0; column < width; column++) {
        if (exposed(column) === color) count += 1;
      }
      return count;
    };

    const fireAll = () => {
      let progress = true;
      while (progress) {
        progress = false;
        for (let slot = 0; slot < slots.length; slot++) {
          const seated = slots[slot];
          if (seated === null) continue;
          const hit = target(seated.color);
          if (hit < 0) continue;
          remove(hit);
          seated.ammo -= 1;
          progress = true;
          if (seated.ammo === 0) slots[slot] = null;
        }
      }
    };

    const stuck = () => left > 0 && slots.every((seated) => seated !== null && target(seated.color) < 0);

    for (;;) {
      fireAll();
      if (left === 0) return { won: true, cubesLeft: 0 };
      const freeSlot = slots.indexOf(null);
      if (stuck() || freeSlot < 0) return { won: false, cubesLeft: left };

      const fronts = queueFront.map((depth, column) => column).filter((column) => queueFront[column] < level.columns[column].length);
      if (fronts.length === 0) return { won: false, cubesLeft: left };

      const seatedColours = new Set(slots.filter(Boolean).map((seated) => seated.color));
      const score = (column) => {
        const shooters = level.columns[column];
        const shooter = shooters[queueFront[column]];
        const count = exposedCount(shooter.color);
        const next = queueFront[column] + 1 < shooters.length ? shooters[queueFront[column] + 1].color : null;
        return (count > 0 && !seatedColours.has(shooter.color) ? SCORE_EXPOSED_AND_NEW : 0)
          + (count > 0 ? SCORE_EXPOSED : 0)
          + (next !== null && exposedCount(next) > 0 && !seatedColours.has(next) ? SCORE_NEXT_USEFUL : 0)
          + count;
      };

      // First maximum wins, so ties go to the lowest column - the same tie-break as Python's max.
      let best = fronts[0];
      let bestScore = score(best);
      for (const column of fronts.slice(1)) {
        const candidate = score(column);
        if (candidate > bestScore) {
          best = column;
          bestScore = candidate;
        }
      }

      const chosen = level.columns[best][queueFront[best]];
      queueFront[best] += 1;
      slots[freeSlot] = { color: chosen.color, ammo: chosen.ammo };
    }
  }

  /** Where this page lives inside the repo, and where the game's levels live relative to it. */
  const PAGE_PATH = "/level-editor/index.html";
  const LEVELS_PATH = "/Assets/00_GAME/Levels";

  /**
   * The Levels folder's path on disk, worked out from the page's own file:// URL. No browser
   * lets a page open a folder by path, but the page can say which one to pick, and this
   * pastes straight into the picker's folder field. Empty when the page is not a file in
   * the repo (served over http, moved elsewhere).
   * @param {string} href The page's URL.
   * @returns {string} A Windows-style path, or "".
   */
  function levelsFolderFor(href) {
    if (!href.startsWith("file:///")) return "";
    const here = decodeURIComponent(href.slice("file:///".length));
    if (!here.endsWith(PAGE_PATH)) return "";
    const levels = here.slice(0, -PAGE_PATH.length) + LEVELS_PATH;
    return levels.replace(/\//g, "\\");
  }

  return {
    COLOURS, COLOUR_NAMES, MAX_QUEUE_COLUMNS, AUTOFILL_CHUNK,
    parse, serialize, stats, validate, resizeBoard, paintCell, nextFreeName, autofill, simulate,
    levelsFolderFor,
  };
})();

if (typeof module !== "undefined") {
  module.exports = Level;
}

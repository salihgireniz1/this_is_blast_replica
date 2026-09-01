// BoardModel - the stage: which colour stands on each cell of the target board.
// Layer: Domain (engine-free).
// Responsibility: storing one colour per cell and turning a Cell into the one slot that
//   belongs to it, refusing any address that is not on the board.
// NOT its responsibility: the dock, or the queues the cannons are drawn from. The shipped
//   original sizes the target board and the player's side as two separate grids with
//   different fields, so widening this array to hold both would fuse two things that are not
//   the same shape. Each of those gets its own type when a phase needs one.
// NOT its responsibility: emptiness. Every cell holds a colour, and a cell never written to
//   reads Yellow because Yellow is the zero value of BlastColor. A board with holes in it
//   needs a contents type that can say "nothing here", which is phase 7's Cube - and the plan
//   is already that swapping this array for Cube[] stays inside Get and Set.
// NOT its responsibility: where a cell sits in the world, or what may legally move where.
//   Positions belong to Presentation, rules to the use cases above this layer.
//
// How the forward shift is modelled: nothing moves. Plan A4 has the cubes behind a dead one
// step forward a slot, but shuffling an array to say so would copy the whole column on every
// shot for a result no caller can tell apart. Instead the colours stay where they were
// authored and a per-column front index walks backwards through them. Presentation animates
// the cubes forward; the model only counts.
//
// What that costs, and it is worth knowing before reading Get: a cell behind the front still
// answers with the colour it was authored with. This type does not track aliveness per cell,
// so "is this cube still standing" is FrontRow's question, never Get's.
//
// Why the front is one index per column rather than one per column and layer: a stack empties
// from the top down - confirmed against the shipped game - so a column is always contiguous
// from the ground up and one number describes it. Almost every shipped level is a single layer
// anyway. Whether a multi-layer column shifts as a whole position or layer by layer is not
// established; the two are identical while Layers is 1, so the question is deliberately left
// open rather than guessed at.

using System;

namespace Blast.Domain
{
    /// <summary>The target board, holding one <see cref="BlastColor"/> per <see cref="Cell"/>.</summary>
    public sealed class BoardModel
    {
        #region Fields

        /// <summary>Every cell's colour, flattened layer by layer then row by row.</summary>
        readonly BlastColor[] _cells;

        /// <summary>Each column's frontmost row still holding cubes; <see cref="Rows"/> when spent.</summary>
        readonly int[] _frontRow;

        /// <summary>How many layers still stand on each column's front row, counted from the ground.</summary>
        readonly int[] _livingLayers;

        #endregion

        #region Properties

        /// <summary>How many columns the board is wide.</summary>
        public int Columns { get; }

        /// <summary>How many rows the board is deep.</summary>
        public int Rows { get; }

        /// <summary>How many layers the board is stacked.</summary>
        public int Layers { get; }

        #endregion

        #region Public Methods

        /// <summary>Builds an empty board of the given size.</summary>
        /// <param name="columns">How many columns wide.</param>
        /// <param name="rows">How many rows deep.</param>
        /// <param name="layers">How many layers stacked.</param>
        public BoardModel(int columns, int rows, int layers)
        {
            Columns = columns;
            Rows = rows;
            Layers = layers;

            _cells = new BlastColor[columns * rows * layers];
            _frontRow = new int[columns];
            _livingLayers = new int[columns];

            // Every column starts full - the board is authored as a filled rectangle, never with
            // holes - so each front sits at row zero with its whole stack standing.
            Array.Fill(_livingLayers, layers);
        }

        /// <summary>Reads the colour standing on a cell.</summary>
        /// <param name="cell">The address to read.</param>
        /// <returns>The colour on that cell.</returns>
        public BlastColor Get(Cell cell) => _cells[IndexOf(cell)];

        /// <summary>Writes the colour standing on a cell.</summary>
        /// <param name="cell">The address to write.</param>
        /// <param name="color">The colour to put there.</param>
        public void Set(Cell cell, BlastColor color) => _cells[IndexOf(cell)] = color;

        /// <summary>Reads the frontmost row of a column that still holds cubes.</summary>
        /// <param name="column">The column to look down.</param>
        /// <returns>That row, or <see cref="Rows"/> once the column is spent.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The column is not on this board.</exception>
        public int FrontRow(int column)
        {
            ValidateColumn(column);

            return _frontRow[column];
        }

        /// <summary>Takes the next cube off the front of a column, topmost layer first.</summary>
        /// <param name="column">The column to take from.</param>
        /// <returns>The address the cube was standing on.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The column is not on this board.</exception>
        /// <exception cref="InvalidOperationException">The column has no cubes left.</exception>
        public Cell Remove(int column)
        {
            ValidateColumn(column);

            if (_frontRow[column] >= Rows)
            {
                throw new InvalidOperationException(
                    $"Column {column} has no cubes left to remove.");
            }

            // Pre-decrement, so the count doubles as the index of the cube it hands out: a full
            // stack of four gives layer 3 first and layer 0 last. That is the shipped game's
            // order and it is what keeps the stack contiguous from the ground up.
            var layer = --_livingLayers[column];
            var cell = new Cell(column, _frontRow[column], layer);

            if (_livingLayers[column] == 0)
            {
                _frontRow[column]++;
                _livingLayers[column] = Layers;
            }

            return cell;
        }

        #endregion

        #region Private Methods

        /// <summary>Refuses a column that is not on this board.</summary>
        /// <param name="column">The column to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The column is not on this board.</exception>
        void ValidateColumn(int column)
        {
            // Same uint cast as IndexOf, and needed for the same reason: the per-column arrays
            // would answer for a column one past the right edge of a wider board without it.
            if ((uint)column >= (uint)Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column),
                    $"Column {column} is not on a board {Columns} columns wide.");
            }
        }

        /// <summary>Turns an address into the one slot that belongs to it.</summary>
        /// <param name="cell">The address to resolve.</param>
        /// <returns>The index of that cell in the flat array.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The address is not on this board.</exception>
        int IndexOf(Cell cell)
        {
            // The array cannot catch this on its own. An off-board column still lands on a
            // valid index most of the time - column 2 of a 2-wide board resolves to the same
            // slot as (0, 1, 0) - so without this check Get answers with a neighbour's colour
            // and nothing ever throws.
            //
            // Cast to uint so one comparison per axis covers both ends: a negative index
            // wraps to something enormous and fails the same test as an index past the edge.
            if ((uint)cell.Column >= (uint)Columns ||
                (uint)cell.Row >= (uint)Rows ||
                (uint)cell.Layer >= (uint)Layers)
            {
                throw new ArgumentOutOfRangeException(nameof(cell),
                    $"({cell.Column}, {cell.Row}, {cell.Layer}) is not on a " +
                    $"{Columns} x {Rows} x {Layers} board.");
            }

            return cell.Layer * Rows * Columns + cell.Row * Columns + cell.Column;
        }

        #endregion
    }
}

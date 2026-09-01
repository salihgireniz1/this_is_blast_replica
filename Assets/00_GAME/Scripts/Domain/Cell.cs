// Cell - the address of one cell on the board.
// Layer: Domain (engine-free).
// Responsibility: naming a cell as a column/row/layer triple, so no two of them can be
//   swapped by accident on their way through the layers, and comparing two cells without
//   allocating. Plan A1 stacks one to three cubes per column and row, which is what makes the
//   third axis real rather than decorative.
// NOT its responsibility: what is standing on that cell. The colour a cube shows lives in
//   BoardModel, keyed by this address; a Cell is where, never what.
// NOT its responsibility: where that cell sits in the world either. The step sizes and
//   origins in plan A2 turn a Cell into a position, and that conversion belongs to
//   Presentation.

using System;

namespace Blast.Domain
{
    /// <summary>One cell on the board, addressed by column, row and layer.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        #region Fields

        /// <summary>Column index, zero at the left edge of the board.</summary>
        public readonly int Column;

        /// <summary>Row index, zero at the row nearest the cannons.</summary>
        public readonly int Row;

        /// <summary>Layer index, zero at the layer resting on the ground.</summary>
        public readonly int Layer;

        #endregion

        #region Public Methods

        /// <summary>Addresses the cell at a column, a row and a layer.</summary>
        /// <param name="column">Column index, zero at the left edge.</param>
        /// <param name="row">Row index, zero nearest the cannons.</param>
        /// <param name="layer">Layer index, zero on the ground.</param>
        public Cell(int column, int row, int layer)
        {
            Column = column;
            Row = row;
            Layer = layer;
        }

        /// <summary>Tells whether both cells address the same column, row and layer.</summary>
        /// <param name="left">The cell on the left of the operator.</param>
        /// <param name="right">The cell on the right of the operator.</param>
        /// <returns>True when the two address the same cell.</returns>
        public static bool operator ==(Cell left, Cell right) => left.Equals(right);

        /// <summary>Tells whether the two cells address different cells.</summary>
        /// <param name="left">The cell on the left of the operator.</param>
        /// <param name="right">The cell on the right of the operator.</param>
        /// <returns>True when the two address different cells.</returns>
        public static bool operator !=(Cell left, Cell right) => !left.Equals(right);

        /// <summary>Compares against another cell without boxing either operand.</summary>
        /// <param name="other">The cell to compare with.</param>
        /// <returns>True when the column, the row and the layer all match.</returns>
        public bool Equals(Cell other) =>
            Column == other.Column && Row == other.Row && Layer == other.Layer;

        /// <summary>Compares against an arbitrary object, boxing as the runtime demands.</summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>True when it is a cell addressing the same column, row and layer.</returns>
        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        /// <summary>Hashes the three axes in order, so a transposed cell lands somewhere else.</summary>
        /// <returns>A hash that tells the three axes apart.</returns>
        public override int GetHashCode() => HashCode.Combine(Column, Row, Layer);

        #endregion
    }
}

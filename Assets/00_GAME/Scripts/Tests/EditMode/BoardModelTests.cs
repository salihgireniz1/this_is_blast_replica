// BoardModelTests - guards the things about the board that fail silently.
// Layer: Test (EditMode).
// Responsibility: that every cell owns its own slot, that an address outside the board is
//   refused, and that a column's front advances exactly as its cubes are removed.
// NOT its responsibility: that Get returns what Set just stored on the same cell. That round
//   trip is one array write and one array read at the same index, so it passes under any index
//   formula, a transposed one included - it would be a test that cannot fail. Nor that a fresh
//   column reports a front of zero, for the same reason: it holds under any implementation.
//
// Why the dimensions are 2 x 3 x 4: all three differ. Transposing any pair of axes in the
// index formula then moves at least one cell, so the aliasing test sees it. On a cubic board a
// swapped column and row map to the same slot and the mistake stays invisible. Rows and Layers
// differing also separates "a column empties after Rows removals" from "after Rows x Layers".
//
// Why the out-of-range column is Columns and not some wild number: on this board its flat
// index is 2, a perfectly valid slot - it addresses (0, 1, 0). Without a bounds check of its
// own, Get answers with a neighbour's colour and never throws. A wildly out-of-range column
// would run off the end of the array and throw on its own, which would let a missing check pass.

using System;
using System.Collections.Generic;
using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies the addressing and the front-of-column contract of <see cref="BoardModel"/>.
    /// </summary>
    public sealed class BoardModelTests
    {
        #region Fields

        /// <summary>Columns on the board these tests build.</summary>
        const int Columns = 2;

        /// <summary>Rows on the board these tests build.</summary>
        const int Rows = 3;

        /// <summary>Layers on the board these tests build.</summary>
        const int Layers = 4;

        #endregion

        #region Public Methods

        /// <summary>Fails when two different cells resolve to the same slot in the array.</summary>
        [Test]
        public void EveryCell_HasItsOwnSlot()
        {
            var board = new BoardModel(Columns, Rows, Layers);

            foreach (var written in AllCells())
            {
                board.Set(written, BlastColor.Red);

                foreach (var read in AllCells())
                {
                    // A cell never written to still reads Yellow, which is the zero value of
                    // BlastColor and so what a fresh array holds. BlastColorTests is what pins
                    // that number down.
                    var expected = read == written ? BlastColor.Red : BlastColor.Yellow;

                    Assert.AreEqual(expected, board.Get(read),
                        $"Writing {Name(written)} was visible at {Name(read)}: two cells share one slot.");
                }

                board.Set(written, BlastColor.Yellow);
            }
        }

        /// <summary>Fails when a column past the edge reads a neighbour instead of throwing.</summary>
        [Test]
        public void AnAddressOutsideTheBoard_IsRefused()
        {
            var board = new BoardModel(Columns, Rows, Layers);
            var outside = new Cell(Columns, 0, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => board.Get(outside),
                "Get answered for a column past the right edge instead of refusing it.");
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Set(outside, BlastColor.Red),
                "Set accepted a column past the right edge instead of refusing it.");
        }

        /// <summary>Fails when one column's front index is read or written through another's.</summary>
        [Test]
        public void RemovingFromOneColumn_LeavesTheOthersAlone()
        {
            var board = new BoardModel(Columns, Rows, Layers);

            EmptyTheFrontPositionOf(board, column: 0);

            Assert.AreEqual(1, board.FrontRow(0),
                "Clearing a front position did not move the front of the column it was in.");
            Assert.AreEqual(0, board.FrontRow(1),
                "Removing from column 0 moved the front of column 1: the two share one index.");
        }

        /// <summary>Fails when a stack empties from the ground up instead of the top down.</summary>
        [Test]
        public void AStack_IsRemovedFromTheTopDown()
        {
            // Confirmed against the shipped game: the top cube of a stack goes first. That is
            // what keeps a stack contiguous from the ground up, so no cube is ever left floating
            // and the game needs no gravity at all. Emptying bottom-up would break it silently -
            // every count still works out, only the wrong cube dies.
            var board = new BoardModel(Columns, Rows, Layers);

            for (var layer = Layers - 1; layer >= 0; layer--)
            {
                Assert.AreEqual(layer, board.Remove(0).Layer,
                    $"Expected layer {layer} to go next; the stack is not emptying from the top.");
            }
        }

        /// <summary>Fails when a column calls itself empty before every layer of every row is gone.</summary>
        [Test]
        public void AColumn_EmptiesOnlyAfterEveryLayerOfEveryRow()
        {
            // The mistake this catches is counting rows and forgetting layers, which empties the
            // column a whole stack early. Rows and Layers differ on this board, so that lands on
            // a different number and shows.
            var board = new BoardModel(Columns, Rows, Layers);
            var cubes = Rows * Layers;

            for (var i = 0; i < cubes - 1; i++)
            {
                board.Remove(0);
            }

            Assert.AreNotEqual(Rows, board.FrontRow(0),
                $"The column called itself empty after {cubes - 1} of its {cubes} cubes.");

            board.Remove(0);

            Assert.AreEqual(Rows, board.FrontRow(0),
                $"The column did not call itself empty after all {cubes} of its cubes were gone.");
        }

        /// <summary>Fails when removing from a spent column corrupts it instead of throwing.</summary>
        [Test]
        public void RemovingFromAnEmptyColumn_IsRefused()
        {
            var board = new BoardModel(Columns, Rows, Layers);

            for (var i = 0; i < Rows * Layers; i++)
            {
                board.Remove(0);
            }

            Assert.Throws<InvalidOperationException>(() => board.Remove(0),
                "An empty column handed out another cube instead of refusing.");
        }

        #endregion

        #region Private Methods

        /// <summary>Removes every layer standing on a column's front position.</summary>
        /// <param name="board">The board to remove from.</param>
        /// <param name="column">The column whose front position is cleared.</param>
        static void EmptyTheFrontPositionOf(BoardModel board, int column)
        {
            for (var i = 0; i < Layers; i++)
            {
                board.Remove(column);
            }
        }

        /// <summary>Enumerates every address on a board of the dimensions these tests use.</summary>
        /// <returns>Each address exactly once.</returns>
        static IEnumerable<Cell> AllCells()
        {
            for (var layer = 0; layer < Layers; layer++)
            for (var row = 0; row < Rows; row++)
            for (var column = 0; column < Columns; column++)
                yield return new Cell(column, row, layer);
        }

        /// <summary>Spells an address out for an assert message, which Cell itself does not do.</summary>
        /// <param name="cell">The address to spell out.</param>
        /// <returns>The three axes in column, row, layer order.</returns>
        static string Name(Cell cell) => $"({cell.Column}, {cell.Row}, {cell.Layer})";

        #endregion
    }
}

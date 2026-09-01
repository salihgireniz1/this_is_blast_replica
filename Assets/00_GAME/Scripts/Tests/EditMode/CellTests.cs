// CellTests - guards the things about Cell that fail silently.
// Layer: Test (EditMode).
// Responsibility: that no two of the three axes are treated as interchangeable, and that
//   comparing two cells costs nothing.
// NOT its responsibility: that equal values compare equal. A struct does that on its own, so
//   asserting it would pass whether or not this type is written correctly.
//
// Why the allocation test: a struct without IEquatable falls back to ValueType.Equals, which
// boxes both operands and compares them by reflection. Every comparison then allocates - and
// Cell is a dictionary key and a hot-loop comparison in a project whose headline budget is
// 0 B of GC per frame. Nothing about that is visible in the source; only this test shows it.
//
// Two tests, not one per axis pair. A column/layer and a row/layer pair were written first and
// both stayed green while Layer was missing from the hash entirely - Cell(3,0,4) against
// Cell(4,0,3) already differs in its column, so nothing about the layer is ever exercised. Each
// test below fails on a distinct mistake instead: transposing a pair catches a symmetric hash,
// and the stacked pair catches a dropped field. Nothing else earns a run.

using Blast.Domain;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies the equality contract of <see cref="Cell"/>.
    /// </summary>
    public sealed class CellTests
    {
        #region Fields

        /// <summary>Sink for comparison results, so no comparison can be optimised away.</summary>
        static bool _sink;

        #endregion

        #region Public Methods

        /// <summary>Fails when comparing two cells boxes them onto the heap.</summary>
        [Test]
        public void Comparing_DoesNotAllocate()
        {
            var left = new Cell(3, 4, 1);
            var right = new Cell(3, 4, 1);

            // Statement body, not an expression body: an assignment expression yields a value, so
            // NUnit binds ActualValueDelegate<bool> instead of the TestDelegate this constraint
            // needs, and the test throws before it ever measures an allocation.
            Assert.That(() => { _sink = left.Equals(right); }, Is.Not.AllocatingGCMemory());
        }

        /// <summary>Fails when the column and the row are treated as interchangeable.</summary>
        [Test]
        public void Equality_TellsAColumnFromARow()
        {
            AssertDistinct(new Cell(3, 4, 0), new Cell(4, 3, 0), "column and row");
        }

        /// <summary>Fails when a cell one layer up is mistaken for the cell below it.</summary>
        [Test]
        public void Equality_TellsAStackedCellFromTheOneBeneathIt()
        {
            // The stack is what makes this a 3D board at all: plan A1 puts one to three cubes
            // on the same column and row. Dropping Layer from the hash is invisible until two
            // stacked cubes collide in a dictionary and one of them cannot be found.
            AssertDistinct(new Cell(3, 4, 0), new Cell(3, 4, 1), "a cell and the one above it");
        }

        #endregion

        #region Private Methods

        /// <summary>Asserts two addresses compare and hash as different cells.</summary>
        /// <param name="left">The first address.</param>
        /// <param name="right">The second address, differing from the first.</param>
        /// <param name="axes">What the pair is meant to distinguish, for the failure message.</param>
        static void AssertDistinct(Cell left, Cell right, string axes)
        {
            // The hash assertion is the one that matters. Equals can compare every field
            // correctly while the hash folds them symmetrically - Column ^ Row ^ Layer and
            // Column + Row + Layer both collide on a transposed pair - which never returns a
            // wrong answer, only degrades every dictionary keyed by a cell, silently.
            Assert.AreNotEqual(left, right, $"A transposed cell compared equal: {axes}.");
            Assert.AreNotEqual(left.GetHashCode(), right.GetHashCode(),
                $"A transposed cell hashes the same, so the hash ignores {axes}.");
        }

        #endregion
    }
}

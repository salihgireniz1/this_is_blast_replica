// FlightPathTests - where along a bullet's flight it enters a cube's footprint.
// Layer: Tests (EditMode).
// Responsibility: pinning the geometry the director uses to find the cubes a bullet
//   brushes on its way to the target: the entry fraction along the segment, a miss when the
//   line passes beside the cube or stops short of it, and zero when the flight starts inside.
// NOT its responsibility: which cubes the director asks about, or what a brushed cube does.

using Blast.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>A flight segment against one cube's square footprint on the board plane.</summary>
    public sealed class FlightPathTests
    {
        #region Fields

        /// <summary>The footprint's side; every case uses a unit cube so fractions read by eye.</summary>
        const float CellSize = 1f;

        /// <summary>How close a computed fraction must be to the one worked out by hand.</summary>
        const float Tolerance = 1e-4f;

        #endregion

        #region Public Methods

        /// <summary>A straight shot up the column enters the cube at its near face.</summary>
        [Test]
        public void EnterFraction_StraightThroughCell_IsDistanceToNearFaceOverLength()
        {
            // From z=0 to z=4, the cube centred on z=2 spans 1.5..2.5: the near face is at 1.5.
            float fraction = FlightPath.EnterFraction(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.EqualTo(1.5f / 4f).Within(Tolerance));
        }

        /// <summary>A diagonal flight enters through whichever face it reaches first.</summary>
        [Test]
        public void EnterFraction_DiagonalThroughCell_IsFractionAtFirstFace()
        {
            // Along the diagonal both faces are reached at the same point: x=z=1.5, so 1.5/4.
            float fraction = FlightPath.EnterFraction(new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 4f), new Vector3(2f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.EqualTo(1.5f / 4f).Within(Tolerance));
        }

        /// <summary>A flight in the next column over never touches this cube.</summary>
        [Test]
        public void EnterFraction_PassingBesideCell_IsNegative()
        {
            float fraction = FlightPath.EnterFraction(new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 4f), new Vector3(0f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.Negative);
        }

        /// <summary>A flight that lands before the cube never enters it.</summary>
        [Test]
        public void EnterFraction_StoppingShortOfCell_IsNegative()
        {
            float fraction = FlightPath.EnterFraction(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.Negative);
        }

        /// <summary>A flight that starts inside the footprint is inside from the first moment.</summary>
        [Test]
        public void EnterFraction_StartingInsideCell_IsZero()
        {
            float fraction = FlightPath.EnterFraction(new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.EqualTo(0f).Within(Tolerance));
        }

        /// <summary>Height is not part of the footprint: a bullet above the board plane still counts as inside the column.</summary>
        [Test]
        public void EnterFraction_IgnoresHeight()
        {
            float fraction = FlightPath.EnterFraction(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 4f), new Vector3(0f, 0f, 2f), CellSize);

            Assert.That(fraction, Is.EqualTo(1.5f / 4f).Within(Tolerance));
        }

        #endregion
    }
}

// ShooterQueueTests - the shooter side's queue discipline, and the Hidden Shooter rule.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in ShooterQueue's source - a queue emptied
//   from the wrong end, one column's front read through another's, a hidden colour leaking
//   before the front row, a visible colour concealed behind it, a spent column handing out
//   one more shooter, and a peek reaching backwards into shooters already taken.
// NOT its responsibility: what a slot does with a taken shooter, or the firing rule. Those
//   are separate behaviours with their own suites.

using System;
using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="ShooterQueue"/>'s order, isolation and reveal rules.</summary>
    public sealed class ShooterQueueTests
    {
        #region Private Methods

        /// <summary>Builds a shooter whose colour is in plain sight.</summary>
        /// <param name="color">The colour it fires.</param>
        static Shooter Visible(BlastColor color) => new Shooter(color, ammo: 10, isHidden: false);

        /// <summary>Builds a shooter whose colour is concealed until it reaches the front.</summary>
        /// <param name="color">The colour it fires.</param>
        static Shooter Hidden(BlastColor color) => new Shooter(color, ammo: 10, isHidden: true);

        #endregion

        #region Public Methods

        /// <summary>
        /// Taking twice hands out the authored order, front first. A queue drained from the
        /// wrong end keeps every count intact and only swaps who comes out when - nothing else
        /// in the suite would notice.
        /// </summary>
        [Test]
        public void TakingTwice_HandsOutTheAuthoredOrder()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red), Visible(BlastColor.Blue) },
            });

            Assert.AreEqual(BlastColor.Red, queue.TakeFront(0).Color,
                "The first take was not the authored front; the queue drains from the wrong end.");
            Assert.AreEqual(BlastColor.Blue, queue.TakeFront(0).Color,
                "The second take was not the shooter behind the front.");
        }

        /// <summary>
        /// Taking from one column leaves the others where they were. A front index shared or
        /// transposed between columns still answers every single-column test correctly.
        /// </summary>
        [Test]
        public void TakingFromOneColumn_LeavesTheOthersAlone()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red), Visible(BlastColor.Blue) },
                new[] { Visible(BlastColor.Green), Visible(BlastColor.Orange) },
            });

            queue.TakeFront(0);

            Assert.AreEqual(BlastColor.Green, queue.Peek(1, 0).Color,
                "Taking from column 0 moved column 1's front.");
        }

        /// <summary>
        /// A hidden shooter keeps its colour to itself until it stands on the front row, and
        /// gives it up the moment it arrives there. Both halves matter: reading the authored
        /// flag alone reveals nothing ever, reading the row alone reveals too early.
        /// </summary>
        [Test]
        public void AHiddenShooter_RevealsOnlyOnTheFrontRow()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red), Hidden(BlastColor.Blue) },
            });

            Assert.IsFalse(queue.IsRevealed(0, 1),
                "A hidden shooter behind the front is already showing its colour.");

            queue.TakeFront(0);

            Assert.IsTrue(queue.IsRevealed(0, 0),
                "A hidden shooter that reached the front is still concealed.");
        }

        /// <summary>
        /// A shooter authored visible shows its colour at any depth. An implementation that
        /// reveals only the front row passes every hidden-shooter test while blanking the two
        /// queue rows the case requires to be readable.
        /// </summary>
        [Test]
        public void AVisibleShooter_IsRevealedBehindTheFront()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red), Visible(BlastColor.Blue) },
            });

            Assert.IsTrue(queue.IsRevealed(0, 1),
                "A visible shooter behind the front is concealed; the queue rows went blank.");
        }

        /// <summary>
        /// A spent column refuses to hand out one more shooter. Without its own check the
        /// front index runs past the array and the failure surfaces as a wrong answer or a
        /// raw out-of-range somewhere far from the mistake.
        /// </summary>
        [Test]
        public void TakingFromASpentColumn_IsRefused()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red) },
            });

            queue.TakeFront(0);

            Assert.Throws<InvalidOperationException>(() => queue.TakeFront(0),
                "A spent column handed out one more shooter.");
        }

        /// <summary>
        /// Peeking behind the front is refused. A negative depth resolves to a shooter already
        /// taken - a perfectly valid array slot - so without its own check Peek answers with a
        /// ghost and nothing ever throws.
        /// </summary>
        [Test]
        public void PeekingBehindTheFront_IsRefused()
        {
            var queue = new ShooterQueue(new[]
            {
                new[] { Visible(BlastColor.Red), Visible(BlastColor.Blue) },
            });

            queue.TakeFront(0);

            Assert.Throws<ArgumentOutOfRangeException>(() => queue.Peek(0, -1),
                "Peek reached backwards into a shooter that was already taken.");
        }

        #endregion
    }
}

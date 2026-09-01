// GameLoopTests - the use case that glues selection, firing and the verdicts together.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in GameLoop's source - a selection that
//   leaves the queue and the slots disagreeing, a guard missing so a spent column or a
//   full row corrupts state, a shot that kills a cube without paying ammo (or pays
//   without killing), verdicts that fire a move early or a move late, and input accepted
//   after the game is already decided.
// NOT its responsibility: the individual rules (GameRulesTests) or the models' own
//   bookkeeping. These tests only assert what the ORCHESTRATION guarantees: that every
//   domain mutation happens together with its partners or not at all.

using Blast.Application;
using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="GameLoop"/>'s selection, firing and verdicts.</summary>
    public sealed class GameLoopTests
    {
        #region Private Methods

        /// <summary>Builds a one-row board whose fronts are the given colours.</summary>
        /// <param name="fronts">One colour per column.</param>
        static BoardModel BoardWithFronts(params BlastColor[] fronts)
        {
            var board = new BoardModel(fronts.Length, rows: 1, layers: 1);

            for (int column = 0; column < fronts.Length; column++)
            {
                board.Set(new Cell(column, 0, 0), fronts[column]);
            }

            return board;
        }

        /// <summary>Builds a queue holding a single column with the given shooters.</summary>
        /// <param name="shooters">The column's shooters, front first.</param>
        static ShooterQueue QueueOf(params Shooter[] shooters) => new ShooterQueue(new[] { shooters });

        #endregion

        #region Public Methods

        /// <summary>
        /// A selection moves the front shooter from the queue into the first slot - both
        /// sides of the handoff, atomically. A loop that takes without seating loses the
        /// shooter; one that seats without taking duplicates it.
        /// </summary>
        [Test]
        public void Select_MovesTheFrontShooterIntoASlot()
        {
            var queue = QueueOf(new Shooter(BlastColor.Red, ammo: 3, isHidden: false));
            var slots = new SlotRow(slots: 2);
            var loop = new GameLoop(BoardWithFronts(BlastColor.Red), queue, slots);

            Assert.IsTrue(loop.TrySelect(0, out int slot), "The selection was refused.");
            Assert.AreEqual(0, slot, "The shooter did not take the first slot.");
            Assert.AreEqual(0, queue.Remaining(0), "The shooter is still in the queue too.");
            Assert.AreEqual(3, slots.AmmoAt(0), "The seated shooter lost its ammo on the way.");
        }

        /// <summary>
        /// A selection is refused - and the domain left untouched - when the column is
        /// spent or every slot is taken. Without the guards TakeFront and Occupy throw
        /// from half-committed state.
        /// </summary>
        [Test]
        public void Select_RefusesAnEmptyColumnAndAFullRow()
        {
            var queue = new ShooterQueue(new[]
            {
                new Shooter[0],
                new[] { new Shooter(BlastColor.Red, 3, false), new Shooter(BlastColor.Red, 3, false) },
            });
            var slots = new SlotRow(slots: 1);
            var loop = new GameLoop(BoardWithFronts(BlastColor.Red), queue, slots);

            Assert.IsFalse(loop.TrySelect(0, out _), "An empty column was selectable.");

            loop.TrySelect(1, out _);

            Assert.IsFalse(loop.TrySelect(1, out _), "A full slot row accepted another selection.");
            Assert.AreEqual(1, queue.Remaining(1), "The refused selection still took the shooter.");
        }

        /// <summary>
        /// A shot pays one ammo for exactly one cube, and reports which board column was
        /// hit so the view can kill the right cube. Killing without paying (or paying
        /// without killing) desynchronises the counter from the board forever.
        /// </summary>
        [Test]
        public void Shoot_PaysOneAmmoForOneCube()
        {
            var board = BoardWithFronts(BlastColor.Blue, BlastColor.Red);
            var slots = new SlotRow(slots: 2);
            var loop = new GameLoop(board, QueueOf(new Shooter(BlastColor.Red, 3, false)), slots);
            loop.TrySelect(0, out int slot);

            Assert.IsTrue(loop.TryShoot(slot, out int hitColumn), "The shot was refused.");
            Assert.AreEqual(1, hitColumn, "The shot did not hit the matching front.");
            Assert.AreEqual(2, slots.AmmoAt(slot), "The kill was not paid with exactly one ammo.");
            Assert.IsFalse(GameRules.TryFindTarget(board, BlastColor.Red, out _),
                "The cube the shot reported dead is still standing.");
        }

        /// <summary>
        /// A shooter with no matching front holds its fire and its ammo. Spending on a
        /// miss drains the shooter while its cubes are still buried - unwinnable later.
        /// </summary>
        [Test]
        public void Shoot_HoldsFireWithoutATarget()
        {
            var loop = new GameLoop(
                BoardWithFronts(BlastColor.Blue),
                QueueOf(new Shooter(BlastColor.Red, 3, false)),
                new SlotRow(2));
            loop.TrySelect(0, out int slot);

            Assert.IsFalse(loop.TryShoot(slot, out _), "A shot was fired at nothing.");
        }

        /// <summary>
        /// The win lands exactly on the last cube's death, and a decided game accepts no
        /// further input - a select or a shot after the verdict corrupts the ending.
        /// </summary>
        [Test]
        public void TheLastCube_WinsAndFreezesTheGame()
        {
            var loop = new GameLoop(
                BoardWithFronts(BlastColor.Red),
                QueueOf(new Shooter(BlastColor.Red, 1, false), new Shooter(BlastColor.Red, 1, false)),
                new SlotRow(2));
            loop.TrySelect(0, out int slot);

            Assert.AreEqual(GameVerdict.Playing, loop.Verdict, "The game decided itself early.");

            loop.TryShoot(slot, out _);

            Assert.AreEqual(GameVerdict.Won, loop.Verdict, "The last cube died and no win landed.");
            Assert.IsFalse(loop.TrySelect(0, out _), "A decided game accepted a selection.");
        }

        /// <summary>
        /// Emptying the board from a full slot row is a WIN, even though "every slot
        /// occupied, nobody has a target" is then also literally true. This is the test
        /// behind TryShoot asking IsWon before IsFailed - swap them and the player clears
        /// the level into a LOST screen.
        /// </summary>
        [Test]
        public void TheLastCube_WinsEvenWithTheSlotRowStuckFull()
        {
            var loop = new GameLoop(
                BoardWithFronts(BlastColor.Red),
                QueueOf(new Shooter(BlastColor.Red, ammo: 2, isHidden: false)),
                new SlotRow(slots: 1));
            loop.TrySelect(0, out int slot);

            // Ammo 2 for 1 cube: after the kill the shooter still holds a shot, so the
            // single slot stays occupied and the row reads full and targetless.
            loop.TryShoot(slot, out _);

            Assert.AreEqual(GameVerdict.Won, loop.Verdict,
                "The board is empty but the full slot row turned the ending into a loss.");
        }

        /// <summary>
        /// Seating the last shooter into a row where nobody has a target loses the game on
        /// that move - not on some later poll. The player must see the fail the moment it
        /// becomes true.
        /// </summary>
        [Test]
        public void FillingTheRowWithTargetlessShooters_Loses()
        {
            var loop = new GameLoop(
                BoardWithFronts(BlastColor.Blue),
                QueueOf(new Shooter(BlastColor.Red, 3, false), new Shooter(BlastColor.Green, 3, false)),
                new SlotRow(2));

            loop.TrySelect(0, out _);

            Assert.AreEqual(GameVerdict.Playing, loop.Verdict, "The fail fired with a slot still free.");

            loop.TrySelect(0, out _);

            Assert.AreEqual(GameVerdict.Lost, loop.Verdict,
                "Every slot is stuck and the game still calls itself playable.");
        }

        #endregion
    }
}

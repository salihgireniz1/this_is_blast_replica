// GameRulesTests - the firing rule and the two verdicts.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in GameRules' source - a target found in
//   the wrong column, a colour matched against a buried cube instead of the front, a spent
//   column shot at, a win declared with cubes still standing, and a fail declared while a
//   slot is free or a slotted shooter still has work.
// NOT its responsibility: BoardModel's own front bookkeeping (BoardModelTests) or SlotRow's
//   occupancy (SlotRowTests). These tests author boards through the public API only.

using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="GameRules"/>' targeting and verdict logic.</summary>
    public sealed class GameRulesTests
    {
        #region Private Methods

        /// <summary>Builds a one-row, one-layer board whose fronts are the given colours.</summary>
        /// <param name="fronts">One colour per column, left to right.</param>
        static BoardModel BoardWithFronts(params BlastColor[] fronts)
        {
            var board = new BoardModel(fronts.Length, rows: 1, layers: 1);

            for (int column = 0; column < fronts.Length; column++)
            {
                board.Set(new Cell(column, 0, 0), fronts[column]);
            }

            return board;
        }

        /// <summary>Builds a shooter of the given colour with ammo to spare.</summary>
        /// <param name="color">The colour it fires.</param>
        static Shooter ShooterOf(BlastColor color) => new Shooter(color, ammo: 10, isHidden: false);

        #endregion

        #region Public Methods

        /// <summary>
        /// The target is the leftmost column whose front matches. Any other pick still
        /// destroys a correct cube, so nothing else in the suite would ever notice a scan
        /// running from the wrong end.
        /// </summary>
        [Test]
        public void FindTarget_PicksTheLeftmostMatchingFront()
        {
            var board = BoardWithFronts(BlastColor.Blue, BlastColor.Red, BlastColor.Red);

            Assert.IsTrue(GameRules.TryFindTarget(board, BlastColor.Red, out int column),
                "No target was found although two fronts match.");
            Assert.AreEqual(1, column, "The target is not the leftmost matching front.");
        }

        /// <summary>
        /// Only the front cube of a column is shootable. A scan that looks deeper finds the
        /// buried Red and reports a target the shooter cannot legally hit.
        /// </summary>
        [Test]
        public void FindTarget_SeesOnlyTheFrontCube()
        {
            var board = new BoardModel(columns: 1, rows: 2, layers: 1);
            board.Set(new Cell(0, 0, 0), BlastColor.Blue);
            board.Set(new Cell(0, 1, 0), BlastColor.Red);

            Assert.IsFalse(GameRules.TryFindTarget(board, BlastColor.Red, out _),
                "A cube buried behind the front was offered as a target.");
        }

        /// <summary>
        /// A spent column is skipped. Its authored colours are still in the array, so a scan
        /// that forgets the front check finds a ghost and shooters fire at an empty column.
        /// </summary>
        [Test]
        public void FindTarget_SkipsASpentColumn()
        {
            var board = BoardWithFronts(BlastColor.Red, BlastColor.Red);
            board.Remove(0);

            GameRules.TryFindTarget(board, BlastColor.Red, out int column);

            Assert.AreEqual(1, column,
                "A spent column's authored colour was offered as a target.");
        }

        /// <summary>
        /// The win comes exactly when the last cube dies - not a cube early, not a cube late.
        /// </summary>
        [Test]
        public void IsWon_OnlyWhenTheLastCubeIsGone()
        {
            var board = BoardWithFronts(BlastColor.Red, BlastColor.Blue);
            board.Remove(0);

            Assert.IsFalse(GameRules.IsWon(board), "The win fired with a cube still standing.");

            board.Remove(1);

            Assert.IsTrue(GameRules.IsWon(board), "Every cube is gone and the win never fired.");
        }

        /// <summary>
        /// No fail while a slot is free: the player can always send another shooter. A fail
        /// check that skips the IsFull half punishes a board state that is still playable.
        /// </summary>
        [Test]
        public void IsFailed_SaysNoWhileASlotIsFree()
        {
            var board = BoardWithFronts(BlastColor.Blue);
            var slots = new SlotRow(slots: 2);
            slots.Occupy(ShooterOf(BlastColor.Red));

            Assert.IsFalse(GameRules.IsFailed(board, slots),
                "A fail was declared although a slot is still free.");
        }

        /// <summary>
        /// No fail while any slotted shooter still has a front cube of its colour: it will
        /// fire, empty its ammo and free a slot. Forgetting this half fails boards that are
        /// about to unblock themselves.
        /// </summary>
        [Test]
        public void IsFailed_SaysNoWhileASlottedShooterStillHasATarget()
        {
            var board = BoardWithFronts(BlastColor.Blue);
            var slots = new SlotRow(slots: 2);
            slots.Occupy(ShooterOf(BlastColor.Red));
            slots.Occupy(ShooterOf(BlastColor.Blue));

            Assert.IsFalse(GameRules.IsFailed(board, slots),
                "A fail was declared although a slotted shooter still has a target.");
        }

        /// <summary>
        /// The fail comes when both halves hold: every slot occupied, no occupant with a
        /// target. This is the positive that catches the whole check wired inside out.
        /// </summary>
        [Test]
        public void IsFailed_SaysYesWhenFullAndTargetless()
        {
            var board = BoardWithFronts(BlastColor.Blue);
            var slots = new SlotRow(slots: 2);
            slots.Occupy(ShooterOf(BlastColor.Red));
            slots.Occupy(ShooterOf(BlastColor.Green));

            Assert.IsTrue(GameRules.IsFailed(board, slots),
                "Every slot is stuck and no shooter has a target, yet no fail was declared.");
        }

        #endregion
    }
}

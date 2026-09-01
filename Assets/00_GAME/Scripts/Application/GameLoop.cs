// GameLoop - the use case: what one player action or one shot changes, atomically.
// Layer: Application.
// Responsibility: gluing the domain models together per move. TrySelect moves a queue
//   front into a slot (both or neither), TryShoot trades exactly one ammo for exactly one
//   cube, and both re-evaluate the verdict on every mutation, because a verdict noticed a
//   move late is a verdict the player saw being wrong.
// NOT its responsibility: time. Nothing here waits, animates or paces - Presentation's
//   director calls these methods on its own rhythm and dresses the results. That split is
//   why this file needs no engine, no UniTask and no mocks to test.
// NOT its responsibility: the rules themselves. Targeting, win and fail live in
//   GameRules; this type only decides when to ask.

using Blast.Domain;

namespace Blast.Application
{
    /// <summary>Drives one level: selections in, shots out, verdict maintained.</summary>
    public sealed class GameLoop
    {
        #region Fields

        /// <summary>The target board shots land on.</summary>
        readonly BoardModel _board;

        /// <summary>The columns of waiting shooters selections draw from.</summary>
        readonly ShooterQueue _queue;

        /// <summary>The slot row seated shooters fire from.</summary>
        readonly SlotRow _slots;

        #endregion

        #region Properties

        /// <summary>The level's state. Once decided, every entry point refuses.</summary>
        public GameVerdict Verdict { get; private set; } = GameVerdict.Playing;

        #endregion

        #region Public Methods

        /// <summary>Wires the loop over one level's models.</summary>
        /// <param name="board">The target board.</param>
        /// <param name="queue">The shooter queue.</param>
        /// <param name="slots">The slot row.</param>
        public GameLoop(BoardModel board, ShooterQueue queue, SlotRow slots)
        {
            _board = board;
            _queue = queue;
            _slots = slots;
        }

        /// <summary>Moves a column's front shooter into the first empty slot.</summary>
        /// <param name="column">The queue column the player tapped.</param>
        /// <param name="slot">The slot the shooter took; -1 when refused.</param>
        /// <returns>True when the shooter was seated.</returns>
        public bool TrySelect(int column, out int slot)
        {
            slot = -1;

            // The guards double as the input rules: a decided game, a spent column and a
            // full row are all "nothing happens", never an exception - the player is
            // allowed to tap anything at any time.
            if (Verdict != GameVerdict.Playing)
            {
                return false;
            }

            if (_queue.Remaining(column) == 0 || _slots.IsFull)
            {
                return false;
            }

            Shooter shooter = _queue.TakeFront(column);
            slot = _slots.Occupy(shooter);

            // Seating can only ever CAUSE a fail (a slot filled), never a win (no cube
            // died), so only the fail needs re-reading here.
            if (GameRules.IsFailed(_board, _slots))
            {
                Verdict = GameVerdict.Lost;
            }

            return true;
        }

        /// <summary>Fires one shot from a slot: one ammo for one cube.</summary>
        /// <param name="slot">The slot whose shooter is due to fire.</param>
        /// <param name="hitColumn">The board column whose front cube died; -1 when held.</param>
        /// <returns>True when a cube died; false when the shooter holds fire.</returns>
        public bool TryShoot(int slot, out int hitColumn)
        {
            hitColumn = -1;

            if (Verdict != GameVerdict.Playing || !_slots.IsOccupied(slot))
            {
                return false;
            }

            Shooter shooter = _slots.ShooterAt(slot);

            if (!GameRules.TryFindTarget(_board, shooter.Color, out hitColumn))
            {
                return false;
            }

            // The pair that must never split: the cube dies and the ammo is paid in the
            // same call, before any other selection or shot can observe the state.
            _board.Remove(hitColumn);
            _slots.Spend(slot);

            // Win first: an emptied board with a full slot row satisfies the fail's
            // letter too, and this ordering is what keeps that ending a win.
            if (GameRules.IsWon(_board))
            {
                Verdict = GameVerdict.Won;
            }
            else if (GameRules.IsFailed(_board, _slots))
            {
                Verdict = GameVerdict.Lost;
            }

            return true;
        }

        #endregion
    }
}

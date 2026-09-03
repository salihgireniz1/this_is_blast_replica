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

using System;
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

        /// <summary>Per board column: what targeting may do with it, derived from the two
        /// counts below. Locked while any shot at it is still hitting its cube (no target at
        /// all), Settling while any is collapsing or sliding (a last-resort target), Free when
        /// every shot has played out.</summary>
        readonly ColumnState[] _columns;

        /// <summary>Per board column: shots whose bullet is flying or whose cube is being hit.
        /// A count, not a flag: shots at one column overlap on screen, and the first one
        /// settling must not free the column under the second.</summary>
        readonly int[] _lockedShots;

        /// <summary>Per board column: shots whose cube is collapsing or whose row is sliding.</summary>
        readonly int[] _settlingShots;

        #endregion

        #region Properties

        /// <summary>The level's state. Once decided, every entry point refuses.</summary>
        public GameVerdict Verdict { get; private set; } = GameVerdict.Playing;

        /// <summary>Raised once, the moment the level is decided, with the verdict. Nothing
        /// polls Verdict for the ending: the UI learns it here.</summary>
        public event Action<GameVerdict> Decided;

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
            _columns = new ColumnState[board.Columns];
            _lockedShots = new int[board.Columns];
            _settlingShots = new int[board.Columns];
        }

        /// <summary>Counts a shot whose cube is being hit: the column is no target at all until
        /// that shot, and every other one at the column, has moved on to settling.</summary>
        /// <param name="column">The board column that just lost its front.</param>
        public void LockColumn(int column)
        {
            _lockedShots[column]++;
            RefreshState(column);
        }

        /// <summary>Moves one shot from hitting to settling: the column may be shot again once
        /// no shot at it is still hitting, but only when no free column matches.</summary>
        /// <param name="column">The board column whose dead cube has started to collapse.</param>
        public void MarkSettling(int column)
        {
            _lockedShots[column]--;
            _settlingShots[column]++;
            RefreshState(column);
        }

        /// <summary>Retires one settling shot: the column is free again once none is left.</summary>
        /// <param name="column">The board column whose survivors have landed.</param>
        public void MarkSettled(int column)
        {
            _settlingShots[column]--;
            RefreshState(column);
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
                Decide(GameVerdict.Lost);
            }

            return true;
        }

        /// <summary>Fires one shot from a slot: one ammo for one cube.</summary>
        /// <param name="slot">The slot whose shooter is due to fire.</param>
        /// <param name="hitColumn">The board column whose front cube died; -1 when the shooter holds fire.</param>
        /// <returns>True when a cube died; false when the shooter holds fire.</returns>
        public bool TryShoot(int slot, out int hitColumn)
        {
            hitColumn = -1;

            if (Verdict != GameVerdict.Playing || !_slots.IsOccupied(slot))
            {
                return false;
            }

            Shooter shooter = _slots.ShooterAt(slot);

            if (!GameRules.TryFindTarget(_board, shooter.Color, _columns, out hitColumn))
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
                Decide(GameVerdict.Won);
            }
            else if (GameRules.IsFailed(_board, _slots))
            {
                Decide(GameVerdict.Lost);
            }

            return true;
        }

        #endregion

        #region Private Methods

        /// <summary>Derives what targeting may do with a column from its shot counts: any
        /// shot still hitting locks it, otherwise any shot settling ranks it last.</summary>
        /// <param name="column">The board column whose counts just changed.</param>
        void RefreshState(int column)
        {
            if (_lockedShots[column] > 0)
            {
                _columns[column] = ColumnState.Locked;
            }
            else if (_settlingShots[column] > 0)
            {
                _columns[column] = ColumnState.Settling;
            }
            else
            {
                _columns[column] = ColumnState.Free;
            }
        }


        /// <summary>Ends the level: records the verdict and announces it. The only writer of
        /// Verdict past Playing, so the announcement cannot be forgotten at one site.</summary>
        /// <param name="verdict">How the level ended.</param>
        void Decide(GameVerdict verdict)
        {
            Verdict = verdict;
            Decided?.Invoke(verdict);
        }

        #endregion
    }
}

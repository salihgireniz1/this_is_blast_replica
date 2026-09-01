// GameRules - the rules that read the two models together: targeting, win, fail.
// Layer: Domain (engine-free).
// Responsibility: answering three questions no single model can - which front cube a
//   colour may shoot (leftmost match), whether the level is won (no cube standing), and
//   whether it is failed (every slot occupied and no occupant with a target).
// NOT its responsibility: acting on the answers. Removing the cube, spending the ammo and
//   showing the overlays is the game loop's job in the Application layer; these functions
//   only read, never mutate, which is what makes them safe to call at any moment.
// NOT its responsibility: ordering the verdicts. An emptied board with a full slot row
//   satisfies IsFailed's letter too, so the caller asks IsWon first - the win outranks it.
//
// Static on purpose: every rule is a pure function over models handed in, there is no
// state to hold and nothing to substitute in a test. An interface here would be a seam
// nothing ever swaps.

namespace Blast.Domain
{
    /// <summary>The pure reading rules of the game: targeting, win and fail.</summary>
    public static class GameRules
    {
        #region Public Methods

        /// <summary>Finds the leftmost column whose front cube wears the given colour.</summary>
        /// <param name="board">The board to scan.</param>
        /// <param name="color">The colour the shooter fires.</param>
        /// <param name="column">The column to shoot at; -1 when there is none.</param>
        /// <returns>True when a front cube of that colour stands somewhere.</returns>
        public static bool TryFindTarget(BoardModel board, BlastColor color, out int column)
        {
            for (int candidate = 0; candidate < board.Columns; candidate++)
            {
                // TryFrontColor is what skips spent columns: their authored colours are
                // still in the array, but a column with no front has nothing to shoot.
                bool columnStands = board.TryFrontColor(candidate, out BlastColor front);
                if (columnStands && front == color)
                {
                    column = candidate;
                    return true;
                }
            }

            column = -1;
            return false;
        }

        /// <summary>Whether every cube on the board is gone.</summary>
        /// <param name="board">The board to check.</param>
        /// <returns>True when no column holds a cube any more.</returns>
        public static bool IsWon(BoardModel board)
        {
            for (int column = 0; column < board.Columns; column++)
            {
                bool columnStillStands = board.FrontRow(column) < board.Rows;
                if (columnStillStands)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether the level is stuck: every slot occupied, no occupant with a target.</summary>
        /// <param name="board">The board the occupants would shoot at.</param>
        /// <param name="slots">The slot row the shooters stand in.</param>
        /// <returns>True when nothing can ever change again. Ask <see cref="IsWon"/> first.</returns>
        public static bool IsFailed(BoardModel board, SlotRow slots)
        {
            // A free slot means the player can still send a shooter down - not stuck.
            if (!slots.IsFull)
            {
                return false;
            }

            for (int slot = 0; slot < slots.Slots; slot++)
            {
                // An occupant with a target will fire, drain its ammo and free its slot,
                // so the row is only stuck when every single one is waiting on a colour
                // that no longer reaches the front.
                Shooter standing = slots.ShooterAt(slot);
                if (TryFindTarget(board, standing.Color, out _))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}

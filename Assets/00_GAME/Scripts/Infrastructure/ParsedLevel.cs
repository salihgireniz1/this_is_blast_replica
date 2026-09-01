// ParsedLevel - one parsed level, ready to play: the three domain models a level is.
// Layer: Infrastructure.
// Responsibility: carrying the board, the shooter queue and the slot row out of the
//   parser as one unit, so a caller cannot end up holding the board of one level and the
//   queue of another.
// NOT its responsibility: building or validating them - that is LevelParser's job, and
//   the constructor trusts it.

using Blast.Domain;

namespace Blast.Infrastructure
{
    /// <summary>The domain models of one level, built together by <see cref="LevelParser"/>.</summary>
    public sealed class ParsedLevel
    {
        #region Properties

        /// <summary>The target board the shooters fire at.</summary>
        public BoardModel Board { get; }

        /// <summary>The columns of waiting shooters.</summary>
        public ShooterQueue Shooters { get; }

        /// <summary>The row of slots the shooters fire from.</summary>
        public SlotRow Slots { get; }

        #endregion

        #region Public Methods

        /// <summary>Bundles one level's models.</summary>
        /// <param name="board">The target board.</param>
        /// <param name="shooters">The shooter queue.</param>
        /// <param name="slots">The slot row.</param>
        public ParsedLevel(BoardModel board, ShooterQueue shooters, SlotRow slots)
        {
            Board = board;
            Shooters = shooters;
            Slots = slots;
        }

        #endregion
    }
}

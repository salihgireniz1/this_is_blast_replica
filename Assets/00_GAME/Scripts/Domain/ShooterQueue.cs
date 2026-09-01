// ShooterQueue - the player's side: columns of shooters waiting their turn.
// Layer: Domain (engine-free).
// Responsibility: handing out each column's front shooter in authored order, saying what
//   stands at a depth behind the front, and answering the Hidden Shooter question - a
//   concealed colour is shown only once its shooter reaches the front row.
// NOT its responsibility: the slots a taken shooter runs to, or the firing rule. The case
//   sizes the queue and the slot row independently (columns are level-driven, slots are
//   always five), so they are separate types.
// NOT its responsibility: which row may be selected. The API is the rule: only TakeFront
//   exists, so a caller cannot take from anywhere but the front.
//
// Same shape as BoardModel, for the same reason: nothing moves. Shooters stay where the
// level authored them and a per-column front index walks forward; Presentation animates
// the step-up. Columns are jagged because the case lets every column carry a different
// number of shooters.

using System;

namespace Blast.Domain
{
    /// <summary>The columns of waiting shooters, drained strictly from the front.</summary>
    public sealed class ShooterQueue
    {
        #region Fields

        /// <summary>Each column's shooters in authored order, front first.</summary>
        readonly Shooter[][] _columns;

        /// <summary>Each column's current front, as an index into its authored array.</summary>
        readonly int[] _front;

        #endregion

        #region Properties

        /// <summary>How many columns of shooters the level authored.</summary>
        public int Columns => _columns.Length;

        #endregion

        #region Public Methods

        /// <summary>Builds a queue from the authored columns.</summary>
        /// <param name="columns">Each column's shooters, front first.</param>
        public ShooterQueue(Shooter[][] columns)
        {
            _columns = columns;
            _front = new int[columns.Length];
        }

        /// <summary>How many shooters a column still holds.</summary>
        /// <param name="column">The column to count.</param>
        /// <exception cref="ArgumentOutOfRangeException">The column does not exist.</exception>
        public int Remaining(int column)
        {
            ValidateColumn(column);

            return _columns[column].Length - _front[column];
        }

        /// <summary>Reads the shooter standing at a depth behind a column's front.</summary>
        /// <param name="column">The column to look down.</param>
        /// <param name="depth">How far behind the front; zero is the front itself.</param>
        /// <returns>The shooter standing there.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The column or depth does not exist.</exception>
        public Shooter Peek(int column, int depth)
        {
            ValidateDepth(column, depth);

            return _columns[column][_front[column] + depth];
        }

        /// <summary>Whether the shooter at a depth is currently showing its colour.</summary>
        /// <param name="column">The column to look down.</param>
        /// <param name="depth">How far behind the front; zero is the front itself.</param>
        /// <returns>True when the colour may be shown.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The column or depth does not exist.</exception>
        public bool IsRevealed(int column, int depth)
        {
            // The Hidden Shooter rule in one line: an authored-visible colour shows anywhere,
            // a concealed one only once it stands on the front row.
            return !Peek(column, depth).IsHidden || depth == 0;
        }

        /// <summary>Takes the front shooter off a column, stepping everyone behind it up.</summary>
        /// <param name="column">The column to take from.</param>
        /// <returns>The shooter that was standing at the front.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The column does not exist.</exception>
        /// <exception cref="InvalidOperationException">The column has no shooters left.</exception>
        public Shooter TakeFront(int column)
        {
            ValidateColumn(column);

            if (_front[column] >= _columns[column].Length)
            {
                throw new InvalidOperationException(
                    $"Column {column} has no shooters left to take.");
            }

            return _columns[column][_front[column]++];
        }

        #endregion

        #region Private Methods

        /// <summary>Refuses a column that does not exist.</summary>
        /// <param name="column">The column to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The column does not exist.</exception>
        void ValidateColumn(int column)
        {
            // Same uint cast as BoardModel, same reason: one comparison covers both ends.
            if ((uint)column >= (uint)_columns.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(column),
                    $"Column {column} does not exist in a queue {_columns.Length} columns wide.");
            }
        }

        /// <summary>Refuses a depth that is not between the front and the column's end.</summary>
        /// <param name="column">The column the depth is measured in.</param>
        /// <param name="depth">The depth to check.</param>
        /// <exception cref="ArgumentOutOfRangeException">The column or depth does not exist.</exception>
        void ValidateDepth(int column, int depth)
        {
            ValidateColumn(column);

            // The array cannot catch this on its own. A negative depth resolves to a shooter
            // already taken - a perfectly valid slot - so without this check Peek answers
            // with a ghost and nothing ever throws.
            if ((uint)depth >= (uint)(_columns[column].Length - _front[column]))
            {
                throw new ArgumentOutOfRangeException(nameof(depth),
                    $"Depth {depth} is not between column {column}'s front and its end.");
            }
        }

        #endregion
    }
}

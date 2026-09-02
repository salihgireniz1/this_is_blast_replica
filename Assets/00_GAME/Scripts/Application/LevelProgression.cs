// LevelProgression - which level the player is on, and which comes after a verdict.
// Layer: Application.
// Responsibility: the index into the level catalogue: read from the store on creation,
//   advanced and wrapped by a win, held by a loss, written back the moment it changes so
//   a player who quits on the WIN screen still resumes on the next level.
// NOT its responsibility: the levels themselves. It knows how many there are, never what
//   they contain; Bootstrap turns Current into a file. Nor the store's medium (ISaveStore).

namespace Blast.Application
{
    /// <summary>The player's position in the level catalogue, persisted across sessions.</summary>
    public sealed class LevelProgression
    {
        #region Fields

        /// <summary>The store key the index lives under.</summary>
        const string IndexKey = "LevelIndex";

        /// <summary>How many levels the catalogue holds; the index wraps at this.</summary>
        readonly int _levelCount;

        /// <summary>Where the index is remembered between sessions.</summary>
        readonly ISaveStore _store;

        #endregion

        #region Properties

        /// <summary>The index of the level to play now. Always inside the catalogue.</summary>
        public int Current { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>Reads the remembered index, folded into the current catalogue's range.</summary>
        /// <param name="levelCount">How many levels exist; must be at least one.</param>
        /// <param name="store">Where the index is remembered.</param>
        public LevelProgression(int levelCount, ISaveStore store)
        {
            _levelCount = levelCount;
            _store = store;

            // The modulo guards a save written when the catalogue was longer: a removed
            // level file must not turn every stored index into an out-of-range boot.
            Current = _store.Load(IndexKey, 0) % levelCount;
        }

        /// <summary>Moves on after a win, stays after a loss, and remembers the result.</summary>
        /// <param name="verdict">How the current level ended.</param>
        public void Record(GameVerdict verdict)
        {
            if (verdict != GameVerdict.Won)
            {
                return;
            }

            Current = (Current + 1) % _levelCount;
            _store.Save(IndexKey, Current);
        }

        #endregion
    }
}

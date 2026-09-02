// LevelProgressionTests - which level comes next, and that the answer survives a restart.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in LevelProgression's source - a win that
//   does not move on, a last level that runs off the end instead of wrapping, a loss that
//   skips ahead, an advance that was never written to the store, and a stored index from
//   a longer catalogue that would throw on the next boot.
// NOT its responsibility: the store itself. The in-memory stand-in here is the whole
//   contract; Es3SaveStore is one call each way and gets no test.

using System.Collections.Generic;
using Blast.Application;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="LevelProgression"/> over an in-memory store.</summary>
    public sealed class LevelProgressionTests
    {
        #region Public Methods

        /// <summary>A win moves to the next level; the last level wraps to the first.</summary>
        [Test]
        public void AWin_AdvancesAndWrapsAround()
        {
            var progression = new LevelProgression(levelCount: 2, new MemoryStore());

            Assert.AreEqual(0, progression.Current, "A fresh store does not start at the first level.");

            progression.Record(GameVerdict.Won);

            Assert.AreEqual(1, progression.Current, "A win did not move on to the next level.");

            progression.Record(GameVerdict.Won);

            Assert.AreEqual(0, progression.Current, "The last level did not wrap around to the first.");
        }

        /// <summary>A loss keeps the same level for the retry.</summary>
        [Test]
        public void ALoss_StaysOnTheSameLevel()
        {
            var progression = new LevelProgression(levelCount: 2, new MemoryStore());

            progression.Record(GameVerdict.Lost);

            Assert.AreEqual(0, progression.Current, "A loss moved the player off the level they lost.");
        }

        /// <summary>The level reached is where the next run starts: the advance was stored.</summary>
        [Test]
        public void TheReachedLevel_IsWhereTheNextRunStarts()
        {
            var store = new MemoryStore();
            new LevelProgression(levelCount: 3, store).Record(GameVerdict.Won);

            var nextRun = new LevelProgression(levelCount: 3, store);

            Assert.AreEqual(1, nextRun.Current, "The advance was not stored; the next run starts over.");
        }

        /// <summary>
        /// A stored index from a longer catalogue stays inside the current one. Removing a
        /// level file must not turn every player's save into an index out of range on boot.
        /// </summary>
        [Test]
        public void AStoredIndexPastTheEnd_IsBroughtBackInside()
        {
            var store = new MemoryStore();
            var longer = new LevelProgression(levelCount: 5, store);
            longer.Record(GameVerdict.Won);
            longer.Record(GameVerdict.Won);
            longer.Record(GameVerdict.Won);

            var shorter = new LevelProgression(levelCount: 2, store);

            Assert.That(shorter.Current, Is.InRange(0, 1), "A stored index past the end was not brought back inside the catalogue.");
        }

        #endregion

        #region Nested Types

        /// <summary>A store that remembers within one test and forgets after it.</summary>
        sealed class MemoryStore : ISaveStore
        {
            /// <summary>Everything saved so far, by key.</summary>
            readonly Dictionary<string, object> _data = new Dictionary<string, object>();

            /// <summary>Reads a value, or the fallback when nothing was saved under the key.</summary>
            /// <param name="key">The key to read.</param>
            /// <param name="fallback">What to return when the key is absent.</param>
            public T Load<T>(string key, T fallback) => _data.TryGetValue(key, out object value) ? (T)value : fallback;

            /// <summary>Writes a value under a key, replacing any previous one.</summary>
            /// <param name="key">The key to write.</param>
            /// <param name="value">The value to remember.</param>
            public void Save<T>(string key, T value) => _data[key] = value;
        }

        #endregion
    }
}

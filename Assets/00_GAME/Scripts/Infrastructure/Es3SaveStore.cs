// Es3SaveStore - ISaveStore answered by Easy Save 3's local file.
// Layer: Infrastructure.
// Responsibility: the two calls that map the contract onto ES3: KeyExists-or-fallback on
//   read, Save on write. ES3 picks the file (its default settings asset) and the format.
// NOT its responsibility: what is saved or when. LevelProgression decides that; this type
//   would not change if a second value joined the file. Swapping the backend means another
//   class implementing ISaveStore and one line in GameLifetimeScope, nothing here.

using Blast.Application;

namespace Blast.Infrastructure
{
    /// <summary>The local Easy Save 3 file behind <see cref="ISaveStore"/>.</summary>
    public sealed class Es3SaveStore : ISaveStore
    {
        #region Public Methods

        /// <summary>Reads a value from the ES3 file, or the fallback when the key was never saved.</summary>
        /// <typeparam name="T">The value's type, as it was saved.</typeparam>
        /// <param name="key">The key to read.</param>
        /// <param name="fallback">What to return when the key is absent.</param>
        public T Load<T>(string key, T fallback) => ES3.Load(key, fallback);

        /// <summary>Writes a value into the ES3 file.</summary>
        /// <typeparam name="T">The value's type.</typeparam>
        /// <param name="key">The key to write.</param>
        /// <param name="value">The value to remember.</param>
        public void Save<T>(string key, T value) => ES3.Save(key, value);

        #endregion
    }
}

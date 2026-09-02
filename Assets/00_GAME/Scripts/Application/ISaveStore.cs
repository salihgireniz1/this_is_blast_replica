// ISaveStore - where remembered values go, by key.
// Layer: Application.
// Responsibility: the one contract every persistence backend implements: read a value under
//   a key or fall back, write a value under a key. Typed so a caller never parses.
// NOT its responsibility: where the bytes live. Easy Save 3 answers it today
//   (Es3SaveStore, Infrastructure); a PlayerPrefs or JSON-file store would be the same two
//   methods. Synchronous on purpose: every backend in scope is local. A remote one would
//   need an async contract, and that is the day this interface changes - not before.

namespace Blast.Application
{
    /// <summary>A typed key-value store for values that outlive a play session.</summary>
    public interface ISaveStore
    {
        /// <summary>Reads the value under a key, or the fallback when nothing was saved there.</summary>
        /// <typeparam name="T">The value's type, as it was saved.</typeparam>
        /// <param name="key">The key to read.</param>
        /// <param name="fallback">What to return when the key is absent.</param>
        T Load<T>(string key, T fallback);

        /// <summary>Writes a value under a key, replacing any previous one.</summary>
        /// <typeparam name="T">The value's type.</typeparam>
        /// <param name="key">The key to write.</param>
        /// <param name="value">The value to remember.</param>
        void Save<T>(string key, T value);
    }
}

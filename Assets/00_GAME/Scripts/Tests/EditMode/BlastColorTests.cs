// BlastColorTests - nails the numeric layout of BlastColor down.
// Layer: Test (EditMode).
// Responsibility: that no existing colour ever changes its number.
// NOT its responsibility: that the enum is byte-wide, or that Surprise sits at the top.
//   Both are read straight off the declaration, so a test would only restate the source.
//
// Why this exists: levels and save files store a colour as its raw number, so inserting a
// new colour in the middle - Purple = 2, Blue pushed to 3 - silently repaints every piece of
// authored content. Nothing throws, nothing logs, the level just plays wrong. Appending a
// colour at the next free number is a choice and leaves this test green; renumbering an
// existing one is a mistake and turns it red.

using Blast.Domain;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Verifies the stored numeric values of <see cref="BlastColor"/>.</summary>
    public sealed class BlastColorTests
    {
        #region Public Methods

        /// <summary>Fails when an existing colour is renumbered, as a mid-enum insert does.</summary>
        [Test]
        public void EveryColour_KeepsTheNumberAuthoredContentStores()
        {
            Assert.AreEqual(0, (byte)BlastColor.Yellow, "Yellow moved.");
            Assert.AreEqual(1, (byte)BlastColor.Red, "Red moved.");
            Assert.AreEqual(2, (byte)BlastColor.Blue, "Blue moved.");
            Assert.AreEqual(3, (byte)BlastColor.Green, "Green moved.");
            Assert.AreEqual(4, (byte)BlastColor.Orange, "Orange moved.");
            Assert.AreEqual(byte.MaxValue, (byte)BlastColor.Surprise, "Surprise moved.");
        }

        #endregion
    }
}

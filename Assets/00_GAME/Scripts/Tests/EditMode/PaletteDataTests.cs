// PaletteDataTests - checks how PaletteData resolves a BlastColor into a Color.
// Layer: Test (EditMode).
// Responsibility: the lookup contract - order independence, and a loud failure when the
//   asset is missing an entry.
// NOT its responsibility: the values in the shipped Palette.asset. That asset is data a
//   designer edits; a separate test guards it for completeness, never for exact hexes.

using System.Collections.Generic;
using System.Reflection;
using Blast.Domain;
using Blast.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies the colour lookup of <see cref="PaletteData"/>.
    /// </summary>
    public sealed class PaletteDataTests
    {
        #region Public Methods

        /// <summary>Fails when the lookup goes by list position instead of by the paired enum.</summary>
        [Test]
        public void ColorOf_ReturnsTheTintPairedWithThatColor()
        {
            // Deliberately not in enum order: a designer reordering rows in the inspector must
            // not change what any colour resolves to.
            var palette = Palette(
                new PaletteEntry { Color = BlastColor.Green, Tint = Color.green },
                new PaletteEntry { Color = BlastColor.Blue,  Tint = Color.blue });

            Assert.AreEqual(Color.blue, palette.ColorOf(BlastColor.Blue));
        }

        /// <summary>Fails when a colour missing from the asset resolves to something silently.</summary>
        [Test]
        public void ColorOf_ThrowsWhenTheColorIsMissing()
        {
            var palette = Palette(new PaletteEntry { Color = BlastColor.Blue, Tint = Color.blue });

            // A hole in the data is an authoring mistake. Returning magenta would hide it until
            // someone noticed the wrong cube on screen.
            Assert.Throws<KeyNotFoundException>(() => palette.ColorOf(BlastColor.Red));
        }

        #endregion

        #region Private Methods

        /// <summary>Builds an in-memory palette by writing the serialised field directly.</summary>
        /// <param name="entries">The colour/tint pairs the palette should hold.</param>
        /// <returns>A palette instance that lives only for the test.</returns>
        /// <remarks>
        /// Reflection rather than a test-only setter: the field stays private in production code,
        /// and no API exists purely because a test needed it.
        /// </remarks>
        static PaletteData Palette(params PaletteEntry[] entries)
        {
            var palette = ScriptableObject.CreateInstance<PaletteData>();

            typeof(PaletteData)
                .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(palette, entries);

            return palette;
        }

        #endregion
    }
}

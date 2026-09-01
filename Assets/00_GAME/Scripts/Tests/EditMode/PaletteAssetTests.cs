// PaletteAssetTests - guards the palette assets a designer edits, not the code that reads them.
// Layer: Test (EditMode).
// Responsibility: that every authored palette holds exactly one row per BlastColor - no
//   missing colour, no leftover duplicate row - and that its tints agree with its materials.
// NOT its responsibility: the hex values, nor how many palettes the project keeps. Both are
//   design decisions; asserting them would turn a retint or a second theme into a failing test.
//   What it does assert is that the two places a colour is written down agree with each other.
//
// The shader check rides along on the material loop rather than living in its own suite: if the
// TCP2 shader GUID ever stops resolving, Unity silently swaps in the error shader and every cube
// turns magenta at build time, not in the editor.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Blast.Domain;
using Blast.Infrastructure;
using NUnit.Framework;
using UnityEditor;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies that every authored palette asset covers the enum completely.
    /// </summary>
    public sealed class PaletteAssetTests
    {
        #region Fields

        /// <summary>The shader property holding a cube material's albedo.</summary>
        static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        /// <summary>Shader every palette material must still be bound to.</summary>
        const string HybridShaderName = "Toony Colors Pro 2/Hybrid Shader 2";

        #endregion

        #region Public Methods

        /// <summary>Fails when a colour was added to the enum but never authored in a palette.</summary>
        [Test]
        public void EveryPalette_ResolvesEveryBlastColor()
        {
            foreach (var palette in AllPalettes())
            foreach (BlastColor color in Enum.GetValues(typeof(BlastColor)))
            {
                Assert.DoesNotThrow(() => palette.ColorOf(color), $"{palette.name}: {color} is missing.");
            }
        }

        /// <summary>Fails when a duplicated inspector row leaves a palette longer than the enum.</summary>
        [Test]
        public void NoPalette_HasRowsBeyondTheEnum()
        {
            // Unity's "+" copies the last row. Together with the test above this pins the count
            // from both sides, so a duplicate cannot hide behind a complete lookup.
            foreach (var palette in AllPalettes())
            {
                var rows = new SerializedObject(palette).FindProperty("_entries").arraySize;

                Assert.AreEqual(Enum.GetValues(typeof(BlastColor)).Length, rows, palette.name);
            }
        }

        /// <summary>Fails when a colour has no material, or its material stopped rendering.</summary>
        [Test]
        public void EveryPalette_HasARenderableMaterialForEveryBlastColor()
        {
            foreach (var palette in AllPalettes())
            foreach (BlastColor color in Enum.GetValues(typeof(BlastColor)))
            {
                var material = palette.MaterialOf(color);

                Assert.IsNotNull(material, $"{palette.name}: {color} has no material.");
                Assert.AreEqual(HybridShaderName, material.shader.name,
                    $"{palette.name}, {color}: {material.name} fell back to another shader.");
            }
        }

        /// <summary>Fails when a tint and the material it belongs to drift apart.</summary>
        [Test]
        public void EveryPalette_TintsMatchTheirMaterials()
        {
            foreach (var palette in AllPalettes())
            foreach (BlastColor color in Enum.GetValues(typeof(BlastColor)))
            {
                // GetColor returns the value as authored, not as uploaded: the sRGB to linear
                // conversion happens on its way to the shader, so both sides of this comparison
                // are plain hex-picker values. Color's own == is approximate, which is exactly
                // the precision a hex picker offers anyway.
                var authored = palette.MaterialOf(color).GetColor(BaseColorProperty);

                Assert.IsTrue(authored == palette.ColorOf(color),
                    $"{palette.name}, {color}: material {authored} vs palette {palette.ColorOf(color)}.");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>Every palette asset in the project, however many the designer keeps.</summary>
        /// <returns>The authored palettes.</returns>
        static IEnumerable<PaletteData> AllPalettes()
        {
            return AssetDatabase.FindAssets($"t:{nameof(PaletteData)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PaletteData>);
        }

        #endregion
    }
}

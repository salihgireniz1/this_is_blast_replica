// PaletteData - the authored colour table, edited as an asset rather than in code.
// Layer: Infrastructure.
// Responsibility: pairing every BlastColor with the Color and the Material a designer picked
//   for it, and answering ColorOf() / MaterialOf() for whoever builds a cube or a cannon.
// NOT its responsibility: deciding which colours the game has (that is BlastColor in
//   Domain), and nothing about shading yet - ramp threshold, specular roughness and the
//   materials join this entry when the material chunk needs them.

using System;
using System.Collections.Generic;
using Blast.Domain;
using UnityEngine;

namespace Blast.Infrastructure
{
    /// <summary>One authored colour: which BlastColor it is, and what it looks like.</summary>
    [Serializable]
    public struct PaletteEntry
    {
        /// <summary>The colour identity this entry supplies a value for.</summary>
        public BlastColor Color;

        /// <summary>The value a designer picked, entered as a hex in the colour picker.</summary>
        public UnityEngine.Color Tint;

        /// <summary>
        /// What anything of this colour renders with - a target cube and a cannon share the row,
        /// because the core rule is that the player matches the two by eye and separate tints
        /// would make that unreadable. Kept next to the tint rather than derived from it: a
        /// material carries ramp and specular settings a Color cannot express, and the tint is
        /// still needed where nothing is being rendered - UI, particles, the level editor's grid
        /// of buttons.
        /// </summary>
        public Material Material;
    }

    /// <summary>
    /// The palette asset. Reorder, retint or extend it in the inspector; no code recompiles.
    /// </summary>
    [CreateAssetMenu(fileName = "Palette", menuName = "Blast/Palette Data")]
    public sealed class PaletteData : ScriptableObject
    {
        #region Fields

        /// <summary>The authored pairs. Order is presentation only - lookup goes by Color.</summary>
        [SerializeField] PaletteEntry[] _entries;

        #endregion

        #region Public Methods

        /// <summary>Resolves a colour identity into the authored value.</summary>
        /// <param name="color">The identity to look up.</param>
        /// <returns>The tint paired with that identity.</returns>
        /// <exception cref="KeyNotFoundException">The asset has no entry for that colour.</exception>
        public UnityEngine.Color ColorOf(BlastColor color)
        {
            return EntryOf(color).Tint;
        }

        /// <summary>Resolves a colour identity into the material anything of it renders with.</summary>
        /// <param name="color">The identity to look up.</param>
        /// <returns>The material paired with that identity.</returns>
        /// <exception cref="KeyNotFoundException">The asset has no entry for that colour.</exception>
        public Material MaterialOf(BlastColor color)
        {
            return EntryOf(color).Material;
        }

        #endregion

        #region Private Methods

        /// <summary>Finds the authored row for a colour.</summary>
        /// <param name="color">The identity to look up.</param>
        /// <returns>The row paired with that identity.</returns>
        /// <exception cref="KeyNotFoundException">The asset has no entry for that colour.</exception>
        PaletteEntry EntryOf(BlastColor color)
        {
            // Linear over at most a handful of entries, and only ever on spawn. An index built
            // in OnEnable would be faster on paper and slower to reason about in the inspector.
            foreach (var entry in _entries)
            {
                if (entry.Color == color)
                {
                    return entry;
                }
            }

            throw new KeyNotFoundException($"{name} has no entry for {color}.");
        }

        #endregion
    }
}

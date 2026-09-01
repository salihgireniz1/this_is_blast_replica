// PaletteColorMaterials - the adapter that lets Presentation dress from the palette.
// Layer: Bootstrap (the one layer allowed to see both sides).
// Responsibility: answering Presentation's IColorMaterials with PaletteData's rows. The
//   hidden material is the palette's Surprise row - the one colour identity only a
//   concealed shooter ever wears, which is exactly what Surprise exists for.
// NOT its responsibility: owning any material. It holds no state but the asset reference
//   and adds no behaviour; it only translates between a seam and an asset.

using Blast.Domain;
using Blast.Infrastructure;
using Blast.Presentation;
using UnityEngine;

namespace Blast.Bootstrap
{
    /// <summary>Adapts <see cref="PaletteData"/> onto Presentation's <see cref="IColorMaterials"/>.</summary>
    public sealed class PaletteColorMaterials : IColorMaterials
    {
        #region Fields

        /// <summary>The authored colour table this adapter reads.</summary>
        readonly PaletteData _palette;

        #endregion

        #region Properties

        /// <inheritdoc />
        public Material HiddenMaterial => _palette.MaterialOf(BlastColor.Surprise);

        #endregion

        #region Public Methods

        /// <summary>Wraps the palette asset.</summary>
        /// <param name="palette">The authored colour table.</param>
        public PaletteColorMaterials(PaletteData palette)
        {
            _palette = palette;
        }

        /// <inheritdoc />
        public Material MaterialOf(BlastColor color) => _palette.MaterialOf(color);

        #endregion
    }
}

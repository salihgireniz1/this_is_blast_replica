// IColorMaterials - the one thing Presentation needs from the colour table: materials.
// Layer: Presentation (the consumer defines the seam; Bootstrap supplies the implementation).
// Responsibility: resolving a BlastColor into the material anything of that colour renders
//   with - a cube on the board and a shooter in the queue share it, because the player
//   matches the two by eye.
// NOT its responsibility: where the materials come from. PaletteData lives in
//   Infrastructure, which Presentation is not allowed to see; Bootstrap adapts it onto
//   this interface. That direction - the consumer owning the interface - is what keeps the
//   dependency arrows pointing down.

using Blast.Domain;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Resolves colour identities into render materials for the views.</summary>
    public interface IColorMaterials
    {
        /// <summary>The material anything of this colour renders with.</summary>
        /// <param name="color">The colour identity to resolve.</param>
        Material MaterialOf(BlastColor color);

        /// <summary>The material a concealed shooter wears until its colour is revealed.</summary>
        Material HiddenMaterial { get; }
    }
}

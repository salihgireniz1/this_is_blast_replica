// CubeView - one cube on the board, as the player sees it.
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing the material its colour resolves to.
// NOT its responsibility: knowing its colour's meaning, its cell, or when it dies. The
//   spawner places it and the game loop will tell it to leave; this type never reads the
//   domain.

using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>The visual of a single board cube.</summary>
    public sealed class CubeView : MonoBehaviour
    {
        #region Fields

        /// <summary>The renderer that wears the colour material. Assigned in the prefab.</summary>
        [SerializeField] MeshRenderer _renderer;

        #endregion

        #region Public Methods

        /// <summary>Dresses the cube in its colour's material.</summary>
        /// <param name="material">The material to wear.</param>
        public void Wear(Material material)
        {
            // sharedMaterial on purpose: assigning .material clones the material per cube,
            // which is 100 hidden instances on a full board for no visual difference.
            _renderer.sharedMaterial = material;
        }

        #endregion
    }
}

// ShooterView - one shooter in the queue or in a slot, as the player sees it.
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing its colour's material - or the hidden one while concealed - and
//   showing its remaining ammo on the counter.
// NOT its responsibility: deciding whether it is revealed (ShooterQueue's rule), how much
//   ammo remains (SlotRow's count), or when it runs, fires and leaves (the game loop's
//   calls, in a later chunk). It renders state; it never computes it.

using TMPro;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>The visual of a single shooter.</summary>
    public sealed class ShooterView : MonoBehaviour
    {
        #region Fields

        /// <summary>Every renderer that wears the colour material. Assigned in the prefab.</summary>
        [SerializeField] Renderer[] _coloredParts;

        /// <summary>The ammo counter above the head. Assigned in the prefab.</summary>
        [SerializeField] TMP_Text _ammoText;

        /// <summary>What the counter shows while the colour is concealed.</summary>
        const string ConcealedLabel = "?";

        #endregion

        #region Public Methods

        /// <summary>Dresses the shooter as revealed: its colour and its ammo count.</summary>
        /// <param name="material">The colour material to wear.</param>
        /// <param name="ammo">The shots to show on the counter.</param>
        public void ShowRevealed(Material material, int ammo)
        {
            Wear(material);
            _ammoText.SetText("{0}", ammo);
        }

        /// <summary>Dresses the shooter as concealed: the hidden material and no number.</summary>
        /// <param name="hiddenMaterial">The material concealed shooters wear.</param>
        public void ShowConcealed(Material hiddenMaterial)
        {
            Wear(hiddenMaterial);
            _ammoText.SetText(ConcealedLabel);
        }

        #endregion

        #region Private Methods

        /// <summary>Puts one material on every coloured part.</summary>
        /// <param name="material">The material to wear.</param>
        void Wear(Material material)
        {
            foreach (var part in _coloredParts)
            {
                // sharedMaterial for the same reason as CubeView: no per-instance clones.
                part.sharedMaterial = material;
            }
        }

        #endregion
    }
}

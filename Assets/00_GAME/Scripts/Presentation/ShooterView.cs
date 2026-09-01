// ShooterView - one shooter in the queue or in a slot, as the player sees it.
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing its colour's material - or the hidden one while concealed -
//   showing its remaining ammo on the counter, and driving the animator's three
//   parameters (isIdle / isRun booleans, Shoot trigger) as it is told to run, stand or fire.
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

        /// <summary>The WalkingCube animator. Assigned in the prefab.</summary>
        [SerializeField] Animator _animator;

        /// <summary>Animator parameter: standing still.</summary>
        static readonly int IsIdle = Animator.StringToHash("isIdle");

        /// <summary>Animator parameter: running.</summary>
        static readonly int IsRun = Animator.StringToHash("isRun");

        /// <summary>Animator trigger: one shot.</summary>
        static readonly int Shoot = Animator.StringToHash("Shoot");

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

        /// <summary>Updates the counter as shots are spent.</summary>
        /// <param name="ammo">The shots remaining.</param>
        public void SetAmmo(int ammo)
        {
            _ammoText.SetText("{0}", ammo);
        }

        /// <summary>Runs or stands: the two booleans are always each other's opposite.</summary>
        /// <param name="running">True to run, false to stand idle.</param>
        public void SetRunning(bool running)
        {
            _animator.SetBool(IsRun, running);
            _animator.SetBool(IsIdle, !running);
        }

        /// <summary>Plays one shot. Drops isIdle so the idle transition cannot cut it short.</summary>
        public void PlayShoot()
        {
            _animator.SetBool(IsIdle, false);
            _animator.SetTrigger(Shoot);
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

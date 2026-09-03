// ShooterView - one shooter in the queue or in a slot, as the player sees it.
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing its colour's material - or the hidden one while concealed -
//   showing its remaining ammo on the counter, driving the animator's three parameters
//   (isIdle / isRun booleans, Shoot trigger) as it is told to run, stand or fire, and
//   turning to face where it runs or what it shoots.
// NOT its responsibility: deciding whether it is revealed (ShooterQueue's rule), how much
//   ammo remains (SlotRow's count), or when it runs, fires and leaves (the game loop's
//   calls, in a later chunk). It renders state; it never computes it.

using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>The visual of a single shooter.</summary>
    public sealed class ShooterView : MonoBehaviour
    {
        #region Fields

        /// <summary>Every renderer that wears the colour material. Assigned in the prefab.</summary>
        [Tooltip("Every renderer Wear recolours: the walking cube body and the cashier body.")]
        [SerializeField] Renderer[] _coloredParts;

        /// <summary>The ammo counter above the head. Assigned in the prefab.</summary>
        [Tooltip("The counter above the head. Shows ammo left, or ? while the shooter is hidden.")]
        [SerializeField] TMP_Text _ammoText;

        /// <summary>The WalkingCube animator. Assigned in the prefab.</summary>
        [Tooltip("The WalkingCube Animator. SetRunning drives isRun / isIdle, PlayShoot pulls the Shoot trigger.")]
        [SerializeField] Animator _animator;

        /// <summary>How much the counter swells per shot, as a fraction of its scale.</summary>
        [Tooltip("How much the ammo counter swells on each shot, as a fraction of its scale. 0 disables the punch.")]
        [SerializeField] float _ammoPunchScale = 0.3f;

        /// <summary>How long the counter's swell lasts. Shorter than the fire interval, so shots never queue punches.</summary>
        [Tooltip("Seconds the ammo counter's swell takes to settle. Keep it under the fire interval.")]
        [SerializeField] float _ammoPunchDuration = 0.15f;

        /// <summary>The inverted-hull outline, worn as a second material slot while selectable. Assigned in the prefab.</summary>
        [Tooltip("The inverted-hull outline material. SetOutlined adds it as a second slot on every coloured part while the shooter is a selectable front.")]
        [SerializeField] Material _outline;

        /// <summary>Animator parameter: standing still.</summary>
        static readonly int IsIdle = Animator.StringToHash("isIdle");

        /// <summary>Animator parameter: running.</summary>
        static readonly int IsRun = Animator.StringToHash("isRun");

        /// <summary>Animator trigger: one shot.</summary>
        static readonly int Shoot = Animator.StringToHash("Shoot");

        /// <summary>
        /// The one yaw tween, built on the first turn and reused by every turn after it: a
        /// DOTween shortcut allocates its getter and setter closures per call (three objects a
        /// shot, measured in Docs/PERFORMANCE.md step 3), so each turn re-targets and restarts
        /// this tween instead. Restart also means two turns never fight.
        /// </summary>
        Tweener _turn;

        /// <summary>True while the body faces the board straight on, so a targetless tick does not restart a turn it already made.</summary>
        bool _facingForward = true;

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

        /// <summary>Marks the shooter as selectable, or not: the outline rides as a second
        /// material slot so the colour in slot 0 is untouched and Wear keeps working.</summary>
        /// <param name="outlined">True for a column's front, false for everyone else.</param>
        public void SetOutlined(bool outlined)
        {
            foreach (var part in _coloredParts)
            {
                // A small array per toggle; a toggle is a tap, not a frame.
                part.sharedMaterials = outlined
                    ? new[] { part.sharedMaterial, _outline }
                    : new[] { part.sharedMaterial };
            }
        }

        /// <summary>Updates the counter as shots are spent.</summary>
        /// <param name="ammo">The shots remaining.</param>
        public void SetAmmo(int ammo)
        {
            _ammoText.SetText("{0}", ammo);

            // A punch returns to the scale it started from, so a punch still running is
            // completed first - otherwise the next one would start from a swollen scale
            // and the counter would drift larger shot by shot.
            Transform counter = _ammoText.transform;
            counter.DOKill(complete: true);
            // vibrato is per second: 2 x 0.15 s rounds to one segment, out and back - a tick, not a wobble.
            counter.DOPunchScale(Vector3.one * _ammoPunchScale, _ammoPunchDuration, vibrato: 2);
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

        /// <summary>Turns (yaw only) to face a world point: the slot it runs to, the cube it shoots.</summary>
        /// <param name="worldPoint">What to face.</param>
        /// <param name="duration">How long the turn takes.</param>
        public void TurnTo(Vector3 worldPoint, float duration)
        {
            // Yaw only, the way DOLookAt with a Y constraint did it: the direction flattened
            // onto the floor, its heading measured from +Z.
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            _facingForward = false;
            Turn(new Vector3(0f, yaw, 0f), duration);
        }

        /// <summary>Turns back to face the board straight on; nothing happens if it already does.</summary>
        /// <param name="duration">How long the turn takes.</param>
        public void FaceForward(float duration)
        {
            if (_facingForward) return;

            _facingForward = true;
            Turn(Vector3.zero, duration);
        }

        #endregion

        #region Private Methods

        /// <summary>Re-targets the reused yaw tween from the current rotation and starts it over.</summary>
        /// <param name="euler">The rotation to reach, as Euler angles.</param>
        /// <param name="duration">How long the turn takes.</param>
        void Turn(Vector3 euler, float duration)
        {
            if (_turn == null)
            {
                // The closures are allocated here, once per view, and never again.
                _turn = DOTween.To(() => transform.rotation, rotation => transform.rotation = rotation, euler, duration)
                    .SetAutoKill(false);
            }

            _turn.ChangeEndValue(euler, duration, snapStartValue: true).Restart();
        }

        /// <summary>Kills the reused tween with the view, since it never auto-kills.</summary>
        void OnDestroy()
        {
            _turn?.Kill();
        }

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

// CubeView - a cube-shaped visual that wears one colour material: a board cube, or the
//   bullet a shooter fires (the same mesh at bullet size, plus a trail).
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing the material its colour resolves to, and sliding to the rest
//   position it is told to reach when its column flows.
// NOT its responsibility: knowing its colour's meaning, its cell, where its rest is, or when
//   it dies. The spawner places it and computes every rest; the game loop tells it to leave;
//   this type never reads the domain.

using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>The visual of a single cube: a board cube or a bullet.</summary>
    public sealed class CubeView : MonoBehaviour
    {
        #region Fields

        /// <summary>The renderer that wears the colour material. Assigned in the prefab.</summary>
        [Tooltip("The renderer Wear recolours: the cube body, or the bullet body on the bullet prefab.")]
        [SerializeField] MeshRenderer _renderer;

        /// <summary>
        /// The one movement tween, built on the first slide or flight and re-targeted by every one after it.
        /// A DOTween shortcut allocates its getter and setter closures per call, and a column
        /// slides every survivor on every shot: 65% of all firing-time garbage before this
        /// (Docs/PERFORMANCE.md, step 3). Restart also answers overlap: a slide re-aimed
        /// mid-overshoot continues to the new rest from wherever the cube is.
        /// </summary>
        TweenerCore<Vector3, Vector3, VectorOptions> _move;

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

        /// <summary>Slides to an absolute rest position from wherever the cube is now.</summary>
        /// <param name="rest">Where the cube comes to rest, in world space.</param>
        /// <param name="duration">How long the slide takes, overshoot and settle included.</param>
        /// <param name="overshoot">How far past the rest the slide swings before settling.</param>
        /// <returns>The slide, so a caller can await the column's landing.</returns>
        public Tweener SlideTo(Vector3 rest, float duration, float overshoot) =>
            MoveTo(rest, duration, Ease.OutBack, overshoot);

        /// <summary>Flies straight to a target: the bullet's flight from muzzle to cube.</summary>
        /// <param name="target">Where the flight ends, in world space.</param>
        /// <param name="duration">How long the flight takes.</param>
        /// <returns>The flight, so the caller can await the impact.</returns>
        public Tweener FlyTo(Vector3 target, float duration) =>
            MoveTo(target, duration, Ease.Linear, 0f);

        #endregion

        #region Private Methods

        /// <summary>Re-targets the one movement tween from the current position and starts it over.</summary>
        /// <param name="target">Where the move ends.</param>
        /// <param name="duration">How long it takes.</param>
        /// <param name="ease">The ease to run it with.</param>
        /// <param name="overshoot">The ease's overshoot, where the ease has one.</param>
        Tweener MoveTo(Vector3 target, float duration, Ease ease, float overshoot)
        {
            if (_move == null)
            {
                // The closures are allocated here, once per cube, and never again.
                _move = DOTween.To(() => transform.position, position => transform.position = position, target, duration)
                    .SetAutoKill(false);
            }

            // The typed ChangeEndValue: the Tweener one takes object and boxes the Vector3 per call.
            _move.ChangeEndValue(target, duration, snapStartValue: true).SetEase(ease, overshoot);
            _move.Restart();
            return _move;
        }

        /// <summary>Kills the reused tween with the view, since it never auto-kills.</summary>
        void OnDestroy()
        {
            _move?.Kill();
        }

        #endregion
    }
}

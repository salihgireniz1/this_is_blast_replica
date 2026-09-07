// CubeView - a cube-shaped visual that wears one colour material: a board cube, or the
//   bullet a shooter fires (the same mesh at bullet size, plus a trail).
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing the material its colour resolves to (and, as a bullet, tinting its
//   trail to match), sliding to the rest position it is told to reach when its column flows,
//   and leaning over when a bullet brushes past (a yaw punch shaped by a curve).
// NOT its responsibility: knowing its colour's meaning, its cell, where its rest is, when
//   it dies, or which bullets brush it. The spawner places it and computes every rest; the
//   game loop tells it to leave; the director decides the lean's sign; this type never reads the domain.

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

        /// <summary>The streak behind a bullet. Assigned on the bullet prefab only; a board cube has none and is never tinted.</summary>
        [Tooltip("The bullet's trail sprite, tinted to the shooter's colour per shot. Leave empty on the board cube.")]
        [SerializeField] SpriteRenderer _trail;

        /// <summary>
        /// The one movement tween, built on the first slide or flight and re-targeted by every one after it.
        /// A DOTween shortcut allocates its getter and setter closures per call, and a column
        /// slides every survivor on every shot: 65% of all firing-time garbage before this
        /// (Docs/PERFORMANCE.md, step 3). Restart also answers overlap: a slide re-aimed
        /// mid-overshoot continues to the new rest from wherever the cube is.
        /// </summary>
        TweenerCore<Vector3, Vector3, VectorOptions> _move;

        /// <summary>
        /// The one lean tween: a 0..1 clock whose setter turns the cube about the up axis by
        /// the current nudge's angle times its shape at that moment. Built on the first nudge,
        /// never auto-killed, restarted by every nudge after: a bullet brushes a column's front
        /// cube on most shots, and a DOPunchRotation per brush is segment arrays plus two closures each.
        /// </summary>
        TweenerCore<float, float, FloatOptions> _lean;

        /// <summary>The nudge in progress: the signed yaw at full lean and the shape spending it.</summary>
        float _leanAngle;

        /// <summary>The nudge in progress: the lean over the clock, as a multiple of the angle.</summary>
        AnimationCurve _leanShape;

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

        /// <summary>Tints the trail to the colour the bullet is carrying.</summary>
        /// <param name="tint">The flat tint of the shooter's colour.</param>
        public void Tint(Color tint)
        {
            // A SpriteRenderer's color is per-renderer vertex colour: no material instance, no allocation.
            _trail.color = tint;
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

        /// <summary>Leans the cube over as a bullet brushes past, then lets the shape bring it back.</summary>
        /// <param name="signedAngle">The yaw at full lean, in degrees; the sign is the side the bullet pushes toward.</param>
        /// <param name="duration">How long the whole nudge lasts.</param>
        /// <param name="shape">The lean over the nudge: x is the fraction of duration, y the multiple of the angle. End at 0 to leave the cube upright.</param>
        /// <returns>The nudge, so a caller or a test can step it.</returns>
        public Tweener Nudge(float signedAngle, float duration, AnimationCurve shape)
        {
            _leanAngle = signedAngle;
            _leanShape = shape;

            if (_lean == null)
            {
                // The closures are allocated here, once per cube, and never again. The clock
                // runs 0..1 linearly; the shape is the curve's job, so the ease stays out of it.
                _lean = DOTween.To(() => 0f, ApplyLean, 1f, duration)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false);
            }

            // A nudge landing mid-nudge starts over from the top: the setter writes the
            // rotation absolutely, so nothing stacks and the cube cannot drift.
            _lean.ChangeEndValue(1f, duration, snapStartValue: true);
            _lean.Restart();
            return _lean;
        }

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

        /// <summary>Writes the cube's rotation for one moment of the nudge clock.</summary>
        /// <param name="clock">How far through the nudge, 0..1.</param>
        void ApplyLean(float clock)
        {
            // Yaw about world up: from the camera's raised view it reads as the in-screen spin
            // the original's cubes do, and it never tips a cube into the floor.
            transform.rotation = Quaternion.AngleAxis(_leanAngle * _leanShape.Evaluate(clock), Vector3.up);
        }

        /// <summary>Kills the reused tweens with the view, since they never auto-kill.</summary>
        void OnDestroy()
        {
            _move?.Kill();
            _lean?.Kill();
        }

        #endregion
    }
}

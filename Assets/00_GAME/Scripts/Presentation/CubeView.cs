// CubeView - a cube-shaped visual that wears one colour material: a board cube, or the
//   bullet a shooter fires (the same mesh at bullet size, plus a trail).
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: wearing the material its colour resolves to (and, as a bullet, tinting its
//   trail to match), sliding to the rest position it is told to reach when its column flows,
//   and taking a shove when a bullet brushes past: it swings about the point it was hit and
//   is pushed along the bullet's travel, then comes back the way a curve says.
// NOT its responsibility: knowing its colour's meaning, its cell, where its rest is, when
//   it dies, or which bullets brush it. The spawner places it and computes every rest; the
//   game loop tells it to leave; the director says where the hit landed; this type never reads the domain.
//
// Why the position is composed rather than written by whichever tween ran last: a slide
// and a shove can overlap (a brushed cube's own column may flow inside the shove's
// 200 ms). The slide owns the rest, the shove owns an offset, and both write rest plus
// offset, so a slide starting mid-shove never adopts the shove as part of its start and
// every cube ends exactly on its rest.

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
        /// The one shove tween: a 0..1 clock whose setter swings and pushes the cube by the
        /// current nudge times its shape at that moment. Built on the first nudge, never
        /// auto-killed, restarted by every nudge after: a bullet brushes a neighbour on most
        /// shots, and a DOPunch per brush is segment arrays plus two closures each.
        /// </summary>
        TweenerCore<float, float, FloatOptions> _shove;

        /// <summary>The nudge in progress: from the centre to where the hit landed, on the board plane.</summary>
        Vector3 _arm;

        /// <summary>The nudge in progress: how far, and which way, the cube is pushed at full shove.</summary>
        Vector3 _push;

        /// <summary>The nudge in progress: the signed yaw at full shove, lever already applied.</summary>
        float _spin;

        /// <summary>The nudge in progress: the shove over the clock, as a multiple of the full shove.</summary>
        AnimationCurve _shape;

        /// <summary>Where the movement tween has the cube: its position without any shove.</summary>
        Vector3 _rest;

        /// <summary>What the shove currently adds to the rest.</summary>
        Vector3 _offset;

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

        /// <summary>
        /// Shoves the cube as a bullet brushes past: it swings about the point it was hit, away
        /// from that side, and is pushed along the bullet's travel; the shape then brings it back.
        /// </summary>
        /// <param name="arm">From the cube's centre to where the hit landed, on the board plane. Zero means a dead-centre hit: push, no spin.</param>
        /// <param name="push">How far, and which way, the cube is pushed at full shove.</param>
        /// <param name="angle">The yaw at full shove for a grazing hit, in degrees; a hit nearer the centre line spins less.</param>
        /// <param name="duration">How long the whole nudge lasts.</param>
        /// <param name="shape">The shove over the nudge: x is the fraction of duration, y the multiple of the full shove. End at 0 to leave the cube at rest.</param>
        /// <returns>The nudge, so a caller or a test can step it.</returns>
        public Tweener Nudge(Vector3 arm, Vector3 push, float angle, float duration, AnimationCurve shape)
        {
            _arm = arm;
            _push = push;
            _shape = shape;

            // The lever: a push past the right of the centre turns the cube to the left, the
            // way a shoulder takes a punch. Torque is arm cross push; its y is the sine of the
            // angle between them, so a grazing hit spins fully and a centre hit not at all.
            Vector3 along = push.sqrMagnitude > 0f ? push.normalized : Vector3.zero;
            Vector3 reach = arm.sqrMagnitude > 0f ? arm.normalized : Vector3.zero;
            _spin = angle * Vector3.Cross(reach, along).y;

            SyncRest();

            if (_shove == null)
            {
                // The closures are allocated here, once per cube, and never again. The clock
                // runs 0..1 linearly; the shape is the curve's job, so the ease stays out of it.
                _shove = DOTween.To(() => 0f, ApplyShove, 1f, duration)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false);
            }

            // A nudge landing mid-nudge starts over from the top: the setter writes offset and
            // rotation absolutely, so nothing stacks and the cube cannot drift.
            _shove.ChangeEndValue(1f, duration, snapStartValue: true);
            _shove.Restart();
            return _shove;
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
            SyncRest();

            if (_move == null)
            {
                // The closures are allocated here, once per cube, and never again. The tween
                // drives the rest, not the transform: a shove may be adding to it.
                _move = DOTween.To(() => _rest, MoveRest, target, duration)
                    .SetAutoKill(false);
            }

            // The typed ChangeEndValue: the Tweener one takes object and boxes the Vector3 per call.
            _move.ChangeEndValue(target, duration, snapStartValue: true).SetEase(ease, overshoot);
            _move.Restart();
            return _move;
        }

        /// <summary>Reads the rest back from the transform, minus whatever a shove is adding, before a tween restarts from it.</summary>
        void SyncRest()
        {
            // The pool moves a bullet by its transform between flights, and a fresh cube has
            // never been tweened: the transform is the truth, the shove's offset is not part of it.
            _rest = transform.position - _offset;
        }

        /// <summary>The movement tween's setter: moves the rest and keeps the shove on top.</summary>
        /// <param name="rest">Where the slide or flight has the cube this frame.</param>
        void MoveRest(Vector3 rest)
        {
            _rest = rest;
            transform.position = _rest + _offset;
        }

        /// <summary>The shove tween's setter: writes the swing and the push for one moment of the clock.</summary>
        /// <param name="clock">How far through the nudge, 0..1.</param>
        void ApplyShove(float clock)
        {
            float amount = _shape.Evaluate(clock);

            // Yaw about world up: from the camera's raised view it reads as the in-screen spin
            // the original's cubes do, and it never tips a cube into the floor. Turning about
            // the hit rather than the centre moves the centre by the swing applied to the
            // arm's negation, plus the arm back; the push is added straight on.
            Quaternion swing = Quaternion.AngleAxis(_spin * amount, Vector3.up);
            _offset = swing * -_arm + _arm + _push * amount;

            transform.rotation = swing;
            transform.position = _rest + _offset;
        }

        /// <summary>Kills the reused tweens with the view, since they never auto-kill.</summary>
        void OnDestroy()
        {
            _move?.Kill();
            _shove?.Kill();
        }

        #endregion
    }
}

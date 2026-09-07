// CubeViewTests - a pooled bullet's flights reuse one tween, and so do a cube's nudges.
// Layer: Tests (EditMode).
// Responsibility: proving that flying the same view twice builds no second tween (a DOTween
//   shortcut allocates two closures per call, and five pooled bullets fly forty times a
//   second between them), that a flight still lands exactly on its target, and that a nudge
//   spins the cube about the point it was hit, shoves it along the push, reuses its tween,
//   leaves the cube upright and at rest when it ends, and never leaks into a slide.
// NOT its responsibility: how a flight or a nudge looks, or the board slide (LevelSpawnerTests).

using System.Reflection;
using Blast.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="CubeView"/>'s reused movement tween.</summary>
    public sealed class CubeViewTests
    {
        #region Fields

        /// <summary>The view under test: a bare MeshRenderer and the component.</summary>
        CubeView _view;

        /// <summary>A nudge shape that holds the full lean for its whole length, so any moment samples the amplitude.</summary>
        static readonly AnimationCurve FullLean = AnimationCurve.Constant(0f, 1f, 1f);

        /// <summary>A nudge shape that leans and comes back: 0 at the start, 1 in the middle, 0 at the end.</summary>
        static readonly AnimationCurve LeanAndBack = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

        /// <summary>A hit landing half a cube to the right of the centre.</summary>
        static readonly Vector3 RightOfCentre = new Vector3(0.5f, 0f, 0f);

        /// <summary>A hit landing half a cube to the left of the centre.</summary>
        static readonly Vector3 LeftOfCentre = new Vector3(-0.5f, 0f, 0f);

        /// <summary>A push straight up the board, a tenth of a unit long.</summary>
        static readonly Vector3 Forward = new Vector3(0f, 0f, 0.1f);

        #endregion

        #region Public Methods

        /// <summary>Builds one view.</summary>
        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Bullet");
            go.AddComponent<MeshRenderer>();
            _view = go.AddComponent<CubeView>();
        }

        /// <summary>Destroys the view and every tween.</summary>
        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_view.gameObject);
        }

        /// <summary>The first flight may build the tween; the second must reuse it.</summary>
        [Test]
        public void ASecondFlight_DoesNotAllocate()
        {
            _view.FlyTo(new Vector3(1f, 0f, 0f), 0.1f);

            Assert.That(() => { _view.FlyTo(new Vector3(2f, 0f, 0f), 0.1f); }, Is.Not.AllocatingGCMemory(),
                "The second flight allocated: the bullet's tween is being rebuilt per shot.");
        }

        /// <summary>The trail sprite wears the tint it is given; the bullet body is not the trail's business.</summary>
        [Test]
        public void Tint_ColoursTheTrail()
        {
            var trail = new GameObject("Trail").AddComponent<SpriteRenderer>();
            trail.transform.SetParent(_view.transform, false);
            typeof(CubeView)
                .GetField("_trail", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_view, trail);

            _view.Tint(Color.red);

            Assert.AreEqual(Color.red, trail.color, "The trail did not take the tint.");
        }

        /// <summary>A reused tween still lands on the new target, not the old one.</summary>
        [Test]
        public void ASecondFlight_LandsOnItsOwnTarget()
        {
            _view.FlyTo(new Vector3(1f, 0f, 0f), 0.1f);
            var target = new Vector3(0f, 0f, 3f);
            _view.FlyTo(target, 0.1f);

            DOTween.CompleteAll();

            Assert.That(Vector3.Distance(_view.transform.position, target), Is.LessThan(0.001f),
                "The second flight did not land on its target: the tween kept the first one.");
        }

        /// <summary>The first nudge may build the tween; the second must reuse it.</summary>
        [Test]
        public void ASecondNudge_DoesNotAllocate()
        {
            _view.Nudge(RightOfCentre, Forward, 10f, 0.2f, LeanAndBack);

            Assert.That(() => { _view.Nudge(LeftOfCentre, Forward, 10f, 0.2f, LeanAndBack); }, Is.Not.AllocatingGCMemory(),
                "The second nudge allocated: the cube's shove tween is being rebuilt per brush.");
        }

        /// <summary>
        /// A bullet grazing the right side of the cube spins it one way, the left side the
        /// other: the spin comes from where the hit landed, not from the bullet's heading.
        /// </summary>
        [Test]
        public void Nudge_SpinsAwayFromTheSideItWasHitOn()
        {
            _view.Nudge(RightOfCentre, Forward, 12f, 0.2f, FullLean).Goto(0.1f);
            float hitRight = Vector3.SignedAngle(Vector3.forward, _view.transform.forward, Vector3.up);

            _view.Nudge(LeftOfCentre, Forward, 12f, 0.2f, FullLean).Goto(0.1f);
            float hitLeft = Vector3.SignedAngle(Vector3.forward, _view.transform.forward, Vector3.up);

            Assert.That(hitRight, Is.EqualTo(-12f).Within(0.01f), "A hit on the right did not spin the cube to the left.");
            Assert.That(hitLeft, Is.EqualTo(12f).Within(0.01f), "A hit on the left did not spin the cube to the right.");
        }

        /// <summary>A hit dead on the centre line has no lever: the cube is shoved, not spun.</summary>
        [Test]
        public void Nudge_OnTheCentreLine_ShovesWithoutSpinning()
        {
            _view.Nudge(Vector3.zero, Forward, 12f, 0.2f, FullLean).Goto(0.1f);

            Assert.That(Quaternion.Angle(_view.transform.rotation, Quaternion.identity), Is.LessThan(0.01f), "A centre hit spun the cube.");
            Assert.That(_view.transform.position.z, Is.EqualTo(Forward.z).Within(0.001f), "A centre hit did not shove the cube the push's length.");
        }

        /// <summary>The cube swings about the point it was hit, so the centre moves as well as turning.</summary>
        [Test]
        public void Nudge_SwingsAboutTheContactPoint()
        {
            // Hit on the right, pushed forward: the spin is -90 about a point half a cube to
            // the right, which carries the centre off the line by the chord of the angle
            // instead of leaving it put; the push then adds straight on.
            _view.Nudge(RightOfCentre, Forward, 90f, 0.2f, FullLean).Goto(0.1f);

            Vector3 expected = Quaternion.AngleAxis(-90f, Vector3.up) * -RightOfCentre + RightOfCentre + Forward;
            Assert.That(Vector3.Distance(_view.transform.position, expected), Is.LessThan(0.001f),
                "The cube spun about its own centre instead of about the hit.");
        }

        /// <summary>A shape that ends at zero leaves the cube upright and back on its spot, however the previous nudge left it.</summary>
        [Test]
        public void Nudge_EndsUprightAndAtRest()
        {
            _view.transform.position = new Vector3(2f, 0f, 3f);
            _view.Nudge(RightOfCentre, Forward, 15f, 0.2f, LeanAndBack);

            DOTween.CompleteAll();

            Assert.That(Quaternion.Angle(_view.transform.rotation, Quaternion.identity), Is.LessThan(0.01f), "The cube stayed leaning after the nudge ended.");
            Assert.That(Vector3.Distance(_view.transform.position, new Vector3(2f, 0f, 3f)), Is.LessThan(0.001f), "The cube did not come back to its spot.");
        }

        /// <summary>
        /// A slide that starts while a nudge is still shoving the cube must not adopt the shove
        /// as part of its start: when both end the cube is exactly at the slide's rest.
        /// </summary>
        [Test]
        public void ASlideDuringANudge_StillEndsAtTheRest()
        {
            // Half-way through a shove that does come back: the cube is at its fullest shove here.
            _view.Nudge(RightOfCentre, Forward, 15f, 0.2f, LeanAndBack).Goto(0.1f);
            var rest = new Vector3(0f, 0f, -1f);
            _view.SlideTo(rest, 0.3f, 1.7f);

            DOTween.CompleteAll();

            Assert.That(Vector3.Distance(_view.transform.position, rest), Is.LessThan(0.001f),
                "The slide ended off its rest: the nudge's shove leaked into the slide.");
        }

        #endregion
    }
}

// CubeViewTests - a pooled bullet's flights reuse one tween, and so do a cube's nudges.
// Layer: Tests (EditMode).
// Responsibility: proving that flying the same view twice builds no second tween (a DOTween
//   shortcut allocates two closures per call, and five pooled bullets fly forty times a
//   second between them), that a flight still lands exactly on its target, and that a nudge
//   leans the cube by its shape, reuses its tween, and leaves the cube upright when it ends.
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
            _view.Nudge(10f, 0.2f, LeanAndBack);

            Assert.That(() => { _view.Nudge(-10f, 0.2f, LeanAndBack); }, Is.Not.AllocatingGCMemory(),
                "The second nudge allocated: the cube's lean tween is being rebuilt per brush.");
        }

        /// <summary>Mid-nudge the cube is turned about the up axis by the signed angle times the shape.</summary>
        [Test]
        public void Nudge_LeansAboutUpBySignedAngleTimesShape()
        {
            _view.Nudge(-12f, 0.2f, FullLean).Goto(0.1f);

            float yaw = Vector3.SignedAngle(Vector3.forward, _view.transform.forward, Vector3.up);
            Assert.That(yaw, Is.EqualTo(-12f).Within(0.01f), "The cube did not lean by the angle it was given.");
        }

        /// <summary>A shape that ends at zero leaves the cube upright, however the previous nudge left it.</summary>
        [Test]
        public void Nudge_EndsUpright()
        {
            _view.Nudge(15f, 0.2f, LeanAndBack);

            DOTween.CompleteAll();

            Assert.That(Quaternion.Angle(_view.transform.rotation, Quaternion.identity), Is.LessThan(0.01f),
                "The cube stayed leaning after the nudge ended.");
        }

        #endregion
    }
}

// CameraShakeTests - the impact shake can be kicked again and leaves the camera where it was.
// Layer: Tests (EditMode).
// Responsibility: pinning the two silent failures of a reusable shake tween: a second kick
//   that does nothing because the tween auto-killed itself after the first, and a camera left
//   off its rest position once the shake has run out.
// NOT its responsibility: how the shake looks (amplitude, duration, pattern are inspector
//   choices), or the director calling Kick at the right moment.

using Blast.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>The camera shake is one tween reused for every impact.</summary>
    public sealed class CameraShakeTests
    {
        #region Fields

        /// <summary>The stand-in camera the shake moves.</summary>
        GameObject _camera;

        /// <summary>The component under test.</summary>
        CameraShake _shake;

        #endregion

        #region Public Methods

        /// <summary>Builds a bare transform with a CameraShake on it.</summary>
        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera");
            _camera.transform.position = new Vector3(0f, 10f, -10f);
            _shake = _camera.AddComponent<CameraShake>();
        }

        /// <summary>Removes the stand-in and any tween still bound to it.</summary>
        [TearDown]
        public void TearDown()
        {
            _camera.transform.DOKill();
            Object.DestroyImmediate(_camera);
        }

        /// <summary>A kick after a finished shake must play again; a tween that auto-killed
        /// itself after the first run would silently do nothing from the second impact on.</summary>
        [Test]
        public void Kick_AfterAFinishedShake_PlaysAgain()
        {
            _shake.Kick();
            DOTween.CompleteAll();

            _shake.Kick();

            Assert.That(
                DOTween.IsTweening(_camera.transform, alsoCheckIfIsPlaying: true),
                "The second kick did not play: the shake tween was killed after the first run.");
        }

        /// <summary>Once the shake has run out the camera must sit exactly where it started.</summary>
        [Test]
        public void AFinishedShake_LeavesTheCameraAtRest()
        {
            Vector3 rest = _camera.transform.position;

            _shake.Kick();
            DOTween.CompleteAll();

            Assert.That(
                Vector3.Distance(_camera.transform.position, rest), Is.LessThan(0.0001f),
                "The camera was left off its rest position after the shake finished.");
        }

        #endregion
    }
}

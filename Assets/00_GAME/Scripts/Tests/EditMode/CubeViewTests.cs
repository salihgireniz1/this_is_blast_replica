// CubeViewTests - a pooled bullet's flights reuse one tween.
// Layer: Tests (EditMode).
// Responsibility: proving that flying the same view twice builds no second tween (a DOTween
//   shortcut allocates two closures per call, and five pooled bullets fly forty times a
//   second between them) and that a flight still lands exactly on its target.
// NOT its responsibility: how a flight looks, or the board slide (LevelSpawnerTests).

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

        #endregion
    }
}

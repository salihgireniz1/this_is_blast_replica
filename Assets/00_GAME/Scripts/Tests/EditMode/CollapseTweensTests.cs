// CollapseTweensTests - a cube's death collapse comes from a reused tween, not a new one.
// Layer: Tests (EditMode).
// Responsibility: proving that a collapse after a finished one builds no second tween (a
//   DOScale allocates two closures per call, and a cube dies on every shot), that two cubes
//   dying at once each get their own, and that a reused tween still shrinks its new target
//   to nothing.
// NOT its responsibility: how the collapse looks, or when the director asks for one.

using Blast.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="CollapseTweens"/>.</summary>
    public sealed class CollapseTweensTests
    {
        #region Fields

        /// <summary>Any duration: every test completes its tweens by hand.</summary>
        const float Duration = 0.1f;

        /// <summary>The pool under test.</summary>
        CollapseTweens _collapses;

        /// <summary>Two throwaway cubes to shrink.</summary>
        Transform _first, _second;

        #endregion

        #region Public Methods

        /// <summary>Builds a fresh pool and two cubes at full scale.</summary>
        [SetUp]
        public void SetUp()
        {
            _collapses = new CollapseTweens();
            _first = new GameObject("First").transform;
            _second = new GameObject("Second").transform;
        }

        /// <summary>Kills every tween and destroys the cubes.</summary>
        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_first.gameObject);
            Object.DestroyImmediate(_second.gameObject);
        }

        /// <summary>A finished collapse's tween is reused for the next cube instead of a new one.</summary>
        [Test]
        public void ACollapseAfterAFinishedOne_DoesNotAllocate()
        {
            _collapses.Play(_first, Duration);
            DOTween.CompleteAll();

            Assert.That(() => { _collapses.Play(_second, Duration); }, Is.Not.AllocatingGCMemory(),
                "The second collapse allocated: the finished tween was not reused.");
        }

        /// <summary>Two cubes dying at once each shrink to nothing; a tween serving both would leave one standing.</summary>
        [Test]
        public void TwoCollapsesAtOnce_BothReachZero()
        {
            _collapses.Play(_first, Duration);
            _collapses.Play(_second, Duration);

            DOTween.CompleteAll();

            Assert.That(_first.localScale, Is.EqualTo(Vector3.zero), "The first cube did not collapse: its tween was taken over by the second.");
            Assert.That(_second.localScale, Is.EqualTo(Vector3.zero), "The second cube did not collapse.");
        }

        /// <summary>A reused tween shrinks its NEW target, not the cube it served before.</summary>
        [Test]
        public void AReusedTween_CollapsesItsNewTarget()
        {
            _collapses.Play(_first, Duration);
            DOTween.CompleteAll();
            _first.localScale = Vector3.one;

            _collapses.Play(_second, Duration);
            DOTween.CompleteAll();

            Assert.That(_second.localScale, Is.EqualTo(Vector3.zero), "The reused tween did not collapse its new target.");
            Assert.That(_first.localScale, Is.EqualTo(Vector3.one), "The reused tween shrank the cube it served before.");
        }

        #endregion
    }
}

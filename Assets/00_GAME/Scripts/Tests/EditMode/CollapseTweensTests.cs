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

        /// <summary>A swell large enough to read on a hit; the reuse tests do not care which.</summary>
        const float Swell = 2.5f;

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
            _collapses.Play(_first, Duration, Swell);
            DOTween.CompleteAll();

            Assert.That(() => { _collapses.Play(_second, Duration, Swell); }, Is.Not.AllocatingGCMemory(),
                "The second collapse allocated: the finished tween was not reused.");
        }

        /// <summary>Two cubes dying at once each shrink to nothing; a tween serving both would leave one standing.</summary>
        [Test]
        public void TwoCollapsesAtOnce_BothReachZero()
        {
            _collapses.Play(_first, Duration, Swell);
            _collapses.Play(_second, Duration, Swell);

            DOTween.CompleteAll();

            Assert.That(_first.localScale, Is.EqualTo(Vector3.zero), "The first cube did not collapse: its tween was taken over by the second.");
            Assert.That(_second.localScale, Is.EqualTo(Vector3.zero), "The second cube did not collapse.");
        }

        /// <summary>A reused tween shrinks its NEW target, not the cube it served before.</summary>
        [Test]
        public void AReusedTween_CollapsesItsNewTarget()
        {
            _collapses.Play(_first, Duration, Swell);
            DOTween.CompleteAll();
            _first.localScale = Vector3.one;

            _collapses.Play(_second, Duration, Swell);
            DOTween.CompleteAll();

            Assert.That(_second.localScale, Is.EqualTo(Vector3.zero), "The reused tween did not collapse its new target.");
            Assert.That(_first.localScale, Is.EqualTo(Vector3.one), "The reused tween shrank the cube it served before.");
        }

        /// <summary>
        /// The hit reads before the death: partway through, the cube is bigger than it
        /// started, and only then does it go to nothing. A plain shrink never crosses its
        /// start; a swell of zero must not either, so the knob really is the flinch.
        /// </summary>
        [Test]
        public void ACollapseWithSwell_GrowsBeforeItShrinks()
        {
            // andPlay keeps the tween playing: Goto pauses by default, and the pool reads a
            // paused tween as idle and would hand it to the second cube.
            Tween swelling = _collapses.Play(_first, Duration, Swell);
            swelling.Goto(Duration * 0.4f, andPlay: true);

            Assert.That(_first.localScale.x, Is.GreaterThan(1f), "The cube never swelled: the hit does not read before the shrink.");

            Tween plain = _collapses.Play(_second, Duration, swell: 0f);
            plain.Goto(Duration * 0.4f, andPlay: true);

            Assert.That(_second.localScale.x, Is.LessThan(1f), "A zero swell still grew the cube.");

            DOTween.CompleteAll();

            Assert.That(_first.localScale, Is.EqualTo(Vector3.zero), "The swelling cube did not end at nothing.");
        }

        #endregion
    }
}

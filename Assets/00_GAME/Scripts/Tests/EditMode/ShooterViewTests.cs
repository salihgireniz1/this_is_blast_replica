// ShooterViewTests - the ammo counter's punch reuses one tween.
// Layer: Tests (EditMode).
// Responsibility: proving that a second punch builds no second tween (a DOPunchScale
//   allocates its segment arrays per call, and every shot ticks a counter) and that a
//   punch cut short by the next shot still leaves the counter at its rest scale.
// NOT its responsibility: the counter's text. SetAmmo as a whole cannot be measured here:
//   TMP's SetText rebuilds its inspector string under #if UNITY_EDITOR, an allocation a
//   Player never makes, so the punch is measured through PunchCounter alone.

using Blast.Presentation;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="ShooterView"/>'s reused counter punch.</summary>
    public sealed class ShooterViewTests
    {
        #region Fields

        /// <summary>The view under test: a bare root with a 3D TMP counter child.</summary>
        ShooterView _view;

        /// <summary>The counter's transform, whose scale the punch drives.</summary>
        Transform _counter;

        #endregion

        #region Public Methods

        /// <summary>Builds one view with only the counter wired; nothing here needs the animator or the renderers.</summary>
        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("Shooter");
            var counter = new GameObject("Counter").AddComponent<TextMeshPro>();
            counter.transform.SetParent(root.transform);
            _counter = counter.transform;
            _view = root.AddComponent<ShooterView>();

            var serialized = new SerializedObject(_view);
            serialized.FindProperty("_ammoText").objectReferenceValue = counter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Kills every tween and destroys the view.</summary>
        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_view.gameObject);
        }

        /// <summary>The first punch may build the tween; the second must reuse it.</summary>
        [Test]
        public void ASecondPunch_DoesNotAllocate()
        {
            _view.PunchCounter();
            DOTween.CompleteAll();

            Assert.That(() => { _view.PunchCounter(); }, Is.Not.AllocatingGCMemory(),
                "The second punch allocated: the counter's tween is being rebuilt per shot.");
        }

        /// <summary>A punch restarted mid-swell still ends at rest; a punch that stacked would drift the counter larger shot by shot.</summary>
        [Test]
        public void APunchCutShortByTheNextShot_EndsAtRest()
        {
            _view.SetAmmo(10);
            _view.SetAmmo(9);

            DOTween.CompleteAll();

            Assert.That(Vector3.Distance(_counter.localScale, Vector3.one), Is.LessThan(0.001f),
                "The counter did not return to rest: a punch started from a swollen scale.");
        }

        #endregion
    }
}

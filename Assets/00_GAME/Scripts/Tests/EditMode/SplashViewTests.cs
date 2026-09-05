// SplashViewTests - a pooled splash takes the shooter's colour on every play.
// Layer: Tests (EditMode).
// Responsibility: proving that one Tint reaches every particle system the splash is made
//   of (the prefab is two: the burst and its inner core) and that a repeat tint allocates
//   nothing, since a splash plays twice per shot at the fire interval.
// NOT its responsibility: how the splash looks, or when it plays (GameDirector).

using System.Reflection;
using Blast.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="SplashView"/>.</summary>
    public sealed class SplashViewTests
    {
        #region Fields

        /// <summary>The view under test, made of two particle systems.</summary>
        SplashView _view;

        /// <summary>The systems the view was given, in order.</summary>
        ParticleSystem[] _systems;

        #endregion

        #region Public Methods

        /// <summary>Builds one splash out of two bare particle systems.</summary>
        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("Splash");
            var inner = new GameObject("Inner");
            inner.transform.SetParent(root.transform, false);

            _systems = new[] { root.AddComponent<ParticleSystem>(), inner.AddComponent<ParticleSystem>() };
            _view = root.AddComponent<SplashView>();
            typeof(SplashView)
                .GetField("_systems", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_view, _systems);
        }

        /// <summary>Destroys the splash.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
        }

        /// <summary>One tint reaches both systems; a tint on the root alone leaves the core white.</summary>
        [Test]
        public void Tint_ColoursEverySystem()
        {
            _view.Tint(Color.red);

            foreach (var system in _systems)
            {
                Assert.AreEqual(Color.red, system.main.startColor.color, $"{system.name} did not take the tint.");
            }
        }

        /// <summary>A second tint is a native setter per system: nothing managed is built.</summary>
        [Test]
        public void ASecondTint_DoesNotAllocate()
        {
            _view.Tint(Color.red);

            Assert.That(() => { _view.Tint(Color.blue); }, Is.Not.AllocatingGCMemory(),
                "Tinting allocated: the splash is building something per play.");
        }

        #endregion
    }
}

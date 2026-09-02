// ComponentPoolTests - the pool reuses a free instance instead of instantiating another.
// Layer: Tests (EditMode).
// Responsibility: proving the one mistake a pool exists to prevent - an Instantiate per
//   use - cannot come back silently, on both ways an instance becomes free: the caller
//   returning it (a bullet after impact) and Unity switching it off (a splash whose Stop
//   Action is Disable).
// NOT its responsibility: particle timing or bullet flight. A finished splash is
//   simulated by deactivating its GameObject, which is exactly what Stop Action = Disable
//   does at runtime.

using Blast.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="ComponentPool{T}"/>.</summary>
    public sealed class ComponentPoolTests
    {
        #region Fields

        /// <summary>The stand-in prefab: a bare ParticleSystem on a throwaway GameObject.</summary>
        ParticleSystem _prefab;

        /// <summary>The parent every instance is spawned under, destroyed with its children.</summary>
        Transform _parent;

        #endregion

        #region Public Methods

        /// <summary>Builds the prefab stand-in and the parent.</summary>
        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Prefab").AddComponent<ParticleSystem>();
            _parent = new GameObject("Parent").transform;
        }

        /// <summary>Destroys everything the test spawned.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent.gameObject);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        /// <summary>An instance Unity switched off itself (Stop Action = Disable) is handed out again, not joined by a third.</summary>
        [Test]
        public void AnInstanceUnitySwitchedOff_IsReused()
        {
            var pool = new ComponentPool<ParticleSystem>(_prefab, _parent);

            pool.Take(Vector3.zero);
            ParticleSystem second = pool.Take(Vector3.one);
            Assert.That(_parent.childCount, Is.EqualTo(2), "Two live instances need two clones.");

            // Stop Action = Disable does this at runtime when the system finishes.
            second.gameObject.SetActive(false);

            ParticleSystem third = pool.Take(Vector3.zero);
            Assert.That(_parent.childCount, Is.EqualTo(2), "A switched-off instance was not reused; a third was instantiated.");
            Assert.That(third, Is.SameAs(second), "The pool handed out a different instance than the free one.");
            Assert.That(third.gameObject.activeSelf, Is.True, "The reused instance was not switched back on.");
        }

        /// <summary>An instance the caller returns is handed out again, not joined by a third.</summary>
        [Test]
        public void AReturnedInstance_IsReused()
        {
            var pool = new ComponentPool<ParticleSystem>(_prefab, _parent);

            pool.Take(Vector3.zero);
            ParticleSystem second = pool.Take(Vector3.one);

            pool.Return(second);

            ParticleSystem third = pool.Take(Vector3.zero);
            Assert.That(_parent.childCount, Is.EqualTo(2), "A returned instance was not reused; a third was instantiated.");
            Assert.That(third, Is.SameAs(second), "The pool handed out a different instance than the returned one.");
        }

        /// <summary>Take places the instance before switching it on, so a Play On Awake effect never fires at the old spot.</summary>
        [Test]
        public void Take_PlacesTheInstanceWhereAsked()
        {
            var pool = new ComponentPool<ParticleSystem>(_prefab, _parent);

            ParticleSystem taken = pool.Take(new Vector3(1f, 2f, 3f));

            Assert.That(taken.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)), "The instance was not moved to the requested position.");
        }

        #endregion
    }
}

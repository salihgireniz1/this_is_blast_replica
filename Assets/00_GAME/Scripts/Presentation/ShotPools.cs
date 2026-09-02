// ShotPools - the pooled visuals of a shot: bullets and muzzle splashes.
// Layer: Presentation.
// Responsibility: owning the two ComponentPools, prewarming them before the first shot and
//   giving each its own named child so the hierarchy shows what is what.
// NOT its responsibility: when a shot happens or what it looks like. The director takes and
//   returns; this file only keeps the instances alive between shots.
//
// Awake order against GameLifetimeScope is undefined and does not matter: nothing reads a
// pool until the first tap, frames after every Awake has run.

using System;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Owns and prewarms the bullet and splash pools.</summary>
    public sealed class ShotPools : MonoBehaviour
    {
        #region Fields

        /// <summary>The bullet visual and how many to make up front: one per slot, since a flight is shorter than the fire interval.</summary>
        [SerializeField] PoolSettings<CubeView> _bullets = new PoolSettings<CubeView> { Prewarm = 5 };

        /// <summary>The muzzle flash and how many to make up front: enough for five slots chain-firing. Its Stop Action is Disable, which is how the pool sees it finish.</summary>
        [SerializeField] PoolSettings<ParticleSystem> _splashes = new PoolSettings<ParticleSystem> { Prewarm = 40 };

        #endregion

        #region Properties

        /// <summary>The bullets, reused across shots.</summary>
        public ComponentPool<CubeView> Bullets { get; private set; }

        /// <summary>The muzzle splashes, reused across shots.</summary>
        public ComponentPool<ParticleSystem> Splashes { get; private set; }

        #endregion

        #region Private Methods

        /// <summary>Builds and prewarms both pools.</summary>
        void Awake()
        {
            Bullets = Build(_bullets, "BulletPool");
            Splashes = Build(_splashes, "SplashPool");
        }

        /// <summary>Makes one pool under its own empty child and fills it to its prewarm count.</summary>
        /// <param name="settings">What to clone and how many to have ready.</param>
        /// <param name="childName">The name of the child the clones live under.</param>
        ComponentPool<T> Build<T>(PoolSettings<T> settings, string childName) where T : Component
        {
            Transform home = new GameObject(childName).transform;
            home.SetParent(transform, false);

            var pool = new ComponentPool<T>(settings.Prefab, home);
            pool.Prewarm(settings.Prewarm);

            return pool;
        }

        #endregion

        #region Nested Types

        /// <summary>One pool's inspector row: the prefab and how many to make before the first use.</summary>
        /// <typeparam name="T">The component the prefab is addressed by.</typeparam>
        [Serializable]
        public struct PoolSettings<T> where T : Component
        {
            /// <summary>What every instance is cloned from.</summary>
            public T Prefab;

            /// <summary>How many instances exist before the first Take.</summary>
            public int Prewarm;
        }

        #endregion
    }
}

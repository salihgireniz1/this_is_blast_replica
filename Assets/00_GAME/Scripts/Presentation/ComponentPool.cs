// ComponentPool - hands out clones of one prefab without instantiating one per use.
// Layer: Presentation.
// Responsibility: keeping every instance ever made and switching a free one back on
//   before creating another. One type serves every pooled visual (splashes, bullets), so
//   there is one reuse rule in the project, not one per prefab.
// NOT its responsibility: knowing when an instance is done. "Off" is the only signal it
//   reads. A caller that knows the end (a bullet after impact) says Return; a prefab
//   whose Stop Action is Disable (a splash) switches itself off and needs no call.

using System.Collections.Generic;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>A grow-only pool of one prefab's clones.</summary>
    /// <typeparam name="T">The component the prefab is addressed by.</typeparam>
    public sealed class ComponentPool<T> where T : Component
    {
        #region Fields

        /// <summary>What every instance is cloned from.</summary>
        readonly T _prefab;

        /// <summary>Where instances live in the hierarchy.</summary>
        readonly Transform _parent;

        /// <summary>Every instance made so far, in use or free.</summary>
        readonly List<T> _instances = new List<T>();

        #endregion

        #region Public Methods

        /// <summary>Creates an empty pool.</summary>
        /// <param name="prefab">The prefab to clone.</param>
        /// <param name="parent">The parent for every clone.</param>
        public ComponentPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        /// <summary>Hands out a free instance at a world position in the prefab's own rotation, cloning one only when none is free.</summary>
        /// <param name="position">Where the instance appears. Set before switching on, so Play On Awake fires there.</param>
        public T Take(Vector3 position)
        {
            return Take(position, _prefab.transform.rotation);
        }

        /// <summary>Hands out a free instance placed and turned, cloning one only when none is free.</summary>
        /// <param name="position">Where the instance appears. Set before switching on, so Play On Awake fires there.</param>
        /// <param name="rotation">Which way it faces (a splash follows the shooter's aim).</param>
        public T Take(Vector3 position, Quaternion rotation)
        {
            T item = FindFree() ?? Create();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);

            return item;
        }

        /// <summary>Marks an instance free again by switching it off.</summary>
        /// <param name="item">An instance this pool handed out.</param>
        public void Return(T item)
        {
            item.gameObject.SetActive(false);
        }

        /// <summary>Creates instances up front so the first uses do not pay for them.</summary>
        /// <param name="count">How many to have ready.</param>
        public void Prewarm(int count)
        {
            while (_instances.Count < count)
            {
                Create();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>The first switched-off instance, or null while all are in use.</summary>
        T FindFree()
        {
            // ponytail: linear scan; a few dozen entries at most, swap for a free-list if it ever shows in a profile.
            foreach (var item in _instances)
            {
                if (!item.gameObject.activeSelf)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Instantiates a new, switched-off instance and remembers it.</summary>
        T Create()
        {
            T item = Object.Instantiate(_prefab, _parent);
            item.gameObject.SetActive(false);
            _instances.Add(item);

            return item;
        }

        #endregion
    }
}

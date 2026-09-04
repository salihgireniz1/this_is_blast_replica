// SceneRestarter - what the overlay's Restart means: reload this scene.
// Layer: Bootstrap.
// Responsibility: listening to LevelEndViewModel's Restart command and reloading the
//   active scene when it fires. An entry point rather than a line in Configure, so the
//   decision has a name, resolves its dependency like everything else, and dies with the
//   container.
// NOT its responsibility: when a restart is offered (the view model) or how the button
//   looks (the view). It knows nothing about the verdict.

using System;
using Blast.UI;
using R3;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Blast.Bootstrap
{
    /// <summary>Reloads the active scene whenever the level-end overlay asks for a restart.</summary>
    public sealed class SceneRestarter : IInitializable, IDisposable
    {
        #region Fields

        /// <summary>The overlay state whose Restart command this listens to.</summary>
        readonly LevelEndViewModel _levelEnd;

        /// <summary>The live subscription; released when the container disposes this.</summary>
        IDisposable _subscription;

        #endregion

        #region Public Methods

        /// <summary>Takes the view model whose Restart command means a reload.</summary>
        /// <param name="levelEnd">The level-end overlay's state.</param>
        public SceneRestarter(LevelEndViewModel levelEnd)
        {
            _levelEnd = levelEnd;
        }

        /// <summary>Starts listening. Runs as the container finishes building, before any tap.</summary>
        public void Initialize()
        {
            _subscription = _levelEnd.Restart.Subscribe(
                _ => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        }

        /// <summary>Stops listening. The container disposes its entry points with the scope.</summary>
        public void Dispose()
        {
            _subscription?.Dispose();
        }

        #endregion
    }
}

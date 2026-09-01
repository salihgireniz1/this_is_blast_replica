// GameLifetimeScopeTests - guards the wiring of the composition root in the shipped scene.
// Layer: Test (EditMode).
// Responsibility: that the scene actually holds a GameLifetimeScope, and that every asset it
//   is meant to register was dragged into its inspector.
// NOT its responsibility: what the scope registers, or that resolving works. "I registered X,
//   is X registered" restates the code it tests - the container is VContainer's to get right.
//
// Why this one earns its place: an unassigned serialized reference registers null without a
// word. The container builds, the scene loads, and the first thing to resolve a palette throws
// deep inside gameplay - a phase away from the empty inspector field that caused it.

using System.Linq;
using Blast.Bootstrap;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies the composition root is present and fully wired in the game scene.
    /// </summary>
    public sealed class GameLifetimeScopeTests
    {
        #region Fields

        /// <summary>The scene the game boots from.</summary>
        const string ScenePath = "Assets/00_GAME/Scenes/Game_Scene.unity";

        #endregion

        #region Public Methods

        /// <summary>Fails when the scene has no composition root, or more than one.</summary>
        [Test]
        public void GameScene_HasExactlyOneLifetimeScope()
        {
            // More than one is as broken as none: two scopes build two containers, and which
            // instance a component gets then depends on injection order.
            Assert.AreEqual(1, ScopesInScene().Length, "GameLifetimeScope count in the scene.");
        }

        /// <summary>Fails when an asset the scope registers was never dragged in.</summary>
        [Test]
        public void GameScene_ScopeHasEveryAssetAssigned()
        {
            var scope = ScopesInScene().Single();

            Assert.IsNotNull(scope.Palette, "The palette field is empty, so it registers null.");
            Assert.IsNotNull(scope.Juice, "The juice field is empty, so it registers null.");
        }

        #endregion

        #region Private Methods

        /// <summary>Opens the game scene and returns every composition root it contains.</summary>
        /// <returns>The scopes found in the scene, inactive objects included.</returns>
        /// <remarks>
        /// The scene is opened rather than inspected as a YAML file: a serialized reference is
        /// what Unity resolves it to, not what the text says, and a broken GUID would read as a
        /// perfectly good line of YAML.
        /// </remarks>
        static GameLifetimeScope[] ScopesInScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            return Object.FindObjectsByType<GameLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        #endregion
    }
}

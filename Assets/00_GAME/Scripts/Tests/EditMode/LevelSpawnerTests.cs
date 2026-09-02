// LevelSpawnerTests - a column that loses two cubes in one frame flows two cells, not one.
// Layer: Tests (EditMode).
// Responsibility: proving the registry's move targets are absolute. Deriving them from
//   transform.position instead reads a half-finished tween and the column ends up short -
//   the bug a fast player hits when two shots reach one column inside a flow duration.
// NOT its responsibility: how a flow looks. Tweens are completed instantly here; only
//   where the survivors come to rest is asserted.

using Blast.Domain;
using Blast.Presentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="LevelSpawner"/>'s view bookkeeping.</summary>
    public sealed class LevelSpawnerTests
    {
        #region Fields

        /// <summary>The stand-in cube prefab: a bare MeshRenderer the views can wear nothing on.</summary>
        CubeView _cubePrefab;

        /// <summary>The spawner under test, and the parent every spawned view lands under.</summary>
        LevelSpawner _spawner;

        #endregion

        #region Public Methods

        /// <summary>Builds the prefab stand-in and a spawner wired to it.</summary>
        [SetUp]
        public void SetUp()
        {
            var prefabObject = new GameObject("CubePrefab");
            prefabObject.AddComponent<MeshRenderer>();
            _cubePrefab = prefabObject.AddComponent<CubeView>();
            AssignField(_cubePrefab, "_renderer", prefabObject.GetComponent<MeshRenderer>());

            _spawner = new GameObject("Spawner").AddComponent<LevelSpawner>();
            AssignField(_spawner, "_cubePrefab", _cubePrefab);
        }

        /// <summary>Destroys everything the test spawned.</summary>
        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            Object.DestroyImmediate(_spawner.gameObject);
            Object.DestroyImmediate(_cubePrefab.gameObject);
        }

        /// <summary>Two removals inside one flow duration shift the survivors two cells, not one.</summary>
        [Test]
        public void TwoRemovalsInOneFrame_FlowTheColumnTwoCells()
        {
            // No shooters: this test is about the board, and a stand-in ShooterView would
            // need an animator and a TMP counter to survive being dressed.
            _spawner.Construct(
                new BoardModel(1, 3, 1), new ShooterQueue(new Shooter[0][]), new SlotRow(1), new NoMaterials());

            // The spawner groups its views by kind; the board lives under "Cubes".
            Transform cubes = _spawner.transform.Find("Cubes");
            Transform front = cubes.GetChild(0);
            Transform survivor = cubes.GetChild(2);
            Vector3 frontStart = front.position;

            // The fast case: both shots resolve before either flow has tweened a frame.
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.15f, 0.1f, 0.15f);
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.15f, 0.1f, 0.15f);

            DOTween.CompleteAll();

            Assert.That(
                Vector3.Distance(survivor.position, frontStart), Is.LessThan(0.001f),
                "The third cube did not reach the front: the column flowed short by a cell.");
        }

        #endregion

        #region Private Methods

        /// <summary>Writes a serialized private field, the way a prefab's inspector would.</summary>
        /// <param name="target">The component to write to.</param>
        /// <param name="field">The serialized field's name.</param>
        /// <param name="value">The object reference to assign.</param>
        static void AssignField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        #endregion

        #region Nested Types

        /// <summary>A colour table that dresses everything in nothing; this test never looks.</summary>
        sealed class NoMaterials : IColorMaterials
        {
            /// <summary>No material for any colour.</summary>
            /// <param name="color">Ignored.</param>
            public Material MaterialOf(BlastColor color) => null;

            /// <summary>No material for concealment either.</summary>
            public Material HiddenMaterial => null;
        }

        #endregion
    }
}

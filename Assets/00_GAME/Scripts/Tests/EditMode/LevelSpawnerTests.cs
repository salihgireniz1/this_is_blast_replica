// LevelSpawnerTests - the registry hands views out in the domain's order and flows a column
//   only when a whole stack is gone.
// Layer: Tests (EditMode).
// Responsibility: proving the registry's move targets are absolute (deriving them from
//   transform.position reads a half-finished tween and the column ends up short - the bug a
//   fast player hits when two shots reach one column inside a flow duration), that the view
//   popped for a shot is the top of its stack (BoardModel removes top layer first, and a
//   registry listing ground first would shrink the bottom cube and leave the top floating),
//   and that survivors slide only once a stack is empty (a row falls when its last cube dies,
//   not on every shot).
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
            AssignField(_spawner, "_prefabs.Cube", _cubePrefab);
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

        /// <summary>
        /// The view handed out for a shot is the top of the stack, because that is the cube the
        /// domain removed. A ground-first registry shrinks the bottom cube and leaves the top
        /// one floating over a hole.
        /// </summary>
        [Test]
        public void PopFrontCube_HandsOutTheTopOfTheStackFirst()
        {
            _spawner.Construct(
                new BoardModel(1, 2, 3), new ShooterQueue(new Shooter[0][]), new SlotRow(1), new NoMaterials());

            Transform cubes = _spawner.transform.Find("Cubes");
            float highest = float.MinValue;
            for (int i = 0; i < cubes.childCount; i++)
            {
                highest = Mathf.Max(highest, cubes.GetChild(i).position.y);
            }

            CubeView popped = _spawner.PopFrontCube(0);

            Assert.That(popped.transform.position.y, Is.EqualTo(highest).Within(0.001f),
                "The popped view is not the top of its stack; the domain removes the top layer first.");
        }

        /// <summary>
        /// Losing the top cube of a stack moves nothing: the row still stands on its ground
        /// cube. Only when the last cube of the stack dies do the survivors slide forward.
        /// </summary>
        [Test]
        public void FlowBoardColumn_WaitsUntilTheWholeStackIsGone()
        {
            _spawner.Construct(
                new BoardModel(1, 2, 2), new ShooterQueue(new Shooter[0][]), new SlotRow(1), new NoMaterials());

            Transform cubes = _spawner.transform.Find("Cubes");
            Vector3 frontGround = LowestFrontPosition(cubes);
            Vector3[] spawned = new Vector3[cubes.childCount];
            for (int i = 0; i < spawned.Length; i++)
            {
                spawned[i] = cubes.GetChild(i).position;
            }

            // Top of the front stack dies: the row behind must not move yet.
            CubeView first = _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.15f, 0.1f, 0.15f);
            DOTween.CompleteAll();

            for (int i = 0; i < spawned.Length; i++)
            {
                if (cubes.GetChild(i) == first.transform) continue;
                Assert.That(Vector3.Distance(cubes.GetChild(i).position, spawned[i]), Is.LessThan(0.001f),
                    "A survivor slid while the front stack still had a cube standing.");
            }

            // Ground of the front stack dies: now the next row's ground cube reaches the front.
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.15f, 0.1f, 0.15f);
            DOTween.CompleteAll();

            bool someoneReachedTheFront = false;
            for (int i = 0; i < spawned.Length; i++)
            {
                someoneReachedTheFront |= Vector3.Distance(cubes.GetChild(i).position, frontGround) < 0.001f;
            }

            Assert.IsTrue(someoneReachedTheFront,
                "The next row did not reach the front once the whole stack was gone.");
        }

        #endregion

        #region Private Methods

        /// <summary>The spawn position of the front row's ground cube: lowest y, then smallest z.</summary>
        /// <param name="cubes">The group every cube view spawned under.</param>
        static Vector3 LowestFrontPosition(Transform cubes)
        {
            Vector3 best = cubes.GetChild(0).position;
            for (int i = 1; i < cubes.childCount; i++)
            {
                Vector3 candidate = cubes.GetChild(i).position;
                if (candidate.y < best.y - 0.001f || (Mathf.Abs(candidate.y - best.y) < 0.001f && candidate.z < best.z))
                {
                    best = candidate;
                }
            }

            return best;
        }


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

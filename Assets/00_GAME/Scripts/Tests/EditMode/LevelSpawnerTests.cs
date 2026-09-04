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
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

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

        /// <summary>The stand-in shooter prefab, built only by the test that needs one.</summary>
        ShooterView _shooterPrefab;

        /// <summary>The outline material the stand-in shooter wears while selectable.</summary>
        Material _outline;

        /// <summary>The two-material colour table, built only by the tests that read what a shooter wears.</summary>
        TwoMaterials _materials;

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
            if (_shooterPrefab != null)
            {
                Object.DestroyImmediate(_shooterPrefab.gameObject);
                Object.DestroyImmediate(_outline);
            }
            if (_materials != null)
            {
                Object.DestroyImmediate(_materials.Colour);
                Object.DestroyImmediate(_materials.Hidden);
            }
        }

        /// <summary>
        /// The outline marks exactly the shooters the player may tap: each column's front,
        /// and nobody else. An outline on the second row invites a tap that does nothing;
        /// a front without one hides the only legal move. When the front is taken, the
        /// outline leaves with it and lands on the shooter stepping up.
        /// </summary>
        [Test]
        public void Outline_FollowsTheSelectableFront()
        {
            BuildShooterPrefab();
            var queue = new ShooterQueue(new[] { new[] { new Shooter(BlastColor.Red, 1, false), new Shooter(BlastColor.Red, 1, false) } });
            _spawner.Construct(new BoardModel(1, 1, 1), queue, new SlotRow(1), new NoMaterials());

            Transform shooters = _spawner.transform.Find("Shooters");
            Renderer first = shooters.GetChild(0).GetComponent<Renderer>();
            Renderer second = shooters.GetChild(1).GetComponent<Renderer>();

            Assert.IsTrue(Wears(first, _outline), "The front is not outlined; the only legal move is hidden.");
            Assert.IsFalse(Wears(second, _outline), "The second row is outlined; it invites a tap that does nothing.");

            ShooterView popped = _spawner.PopFrontShooter(0);
            _spawner.StepQueueForward(0, duration: 0f);

            Assert.IsFalse(Wears(popped.GetComponent<Renderer>(), _outline), "A selected shooter kept its outline.");
            Assert.IsFalse(Wears(second, _outline), "The shooter stepping up was outlined before it arrived at the front.");

            DOTween.CompleteAll(withCallbacks: true);

            Assert.IsTrue(Wears(second, _outline), "The shooter that arrived at the front did not receive the outline.");
        }

        /// <summary>
        /// The Hidden Shooter rule, as the brief words it: the colour shows when the shooter
        /// reaches the front row, not when it starts walking there. A reveal at the start of
        /// the step tells the player the colour a quarter second early.
        /// </summary>
        [Test]
        public void AHiddenShooter_RevealsWhenItArrivesAtTheFront()
        {
            BuildShooterPrefab();
            TwoMaterials materials = BuildMaterials();
            var queue = new ShooterQueue(new[] { new[] { new Shooter(BlastColor.Red, 1, false), new Shooter(BlastColor.Red, 1, true) } });
            _spawner.Construct(new BoardModel(1, 1, 1), queue, new SlotRow(1), materials);

            Renderer second = _spawner.transform.Find("Shooters").GetChild(1).GetComponent<Renderer>();
            Assert.AreEqual(materials.Hidden, second.sharedMaterial, "A hidden shooter spawned showing its colour.");

            _spawner.PopFrontShooter(0);
            _spawner.StepQueueForward(0, duration: 0f);

            Assert.AreEqual(materials.Hidden, second.sharedMaterial, "The colour showed the moment the step began, before the shooter reached the front.");

            DOTween.CompleteAll(withCallbacks: true);

            Assert.AreEqual(materials.Colour, second.sharedMaterial, "The shooter reached the front and its colour never showed.");
        }

        /// <summary>
        /// A shooter tapped while still stepping up must leave revealed and without the
        /// outline: the arrival reveal that was pending has to land before the pop takes
        /// effect, or a hidden shooter would sit in a slot wearing grey, and an outline would
        /// land on it once it is already seated.
        /// </summary>
        [Test]
        public void PoppingAShooterMidStep_RevealsItAndKeepsTheOutlineOffIt()
        {
            BuildShooterPrefab();
            TwoMaterials materials = BuildMaterials();
            var queue = new ShooterQueue(new[]
            {
                new[] { new Shooter(BlastColor.Red, 1, false), new Shooter(BlastColor.Red, 1, true), new Shooter(BlastColor.Red, 1, false) },
            });
            _spawner.Construct(new BoardModel(1, 1, 1), queue, new SlotRow(1), materials);
            Transform shooters = _spawner.transform.Find("Shooters");
            Renderer second = shooters.GetChild(1).GetComponent<Renderer>();
            Renderer third = shooters.GetChild(2).GetComponent<Renderer>();

            _spawner.PopFrontShooter(0);
            _spawner.StepQueueForward(0, duration: 1f);
            ShooterView poppedMidStep = _spawner.PopFrontShooter(0);
            _spawner.StepQueueForward(0, duration: 1f);

            Assert.AreEqual(second.GetComponent<ShooterView>(), poppedMidStep, "The pop did not hand out the stepping shooter.");
            Assert.AreEqual(materials.Colour, second.sharedMaterial, "A shooter tapped mid-step left for its slot still concealed.");
            Assert.IsFalse(Wears(second, _outline), "A shooter tapped mid-step kept the outline.");

            DOTween.CompleteAll(withCallbacks: true);

            Assert.IsFalse(Wears(second, _outline), "The pending arrival reveal outlined a shooter that had already left the queue.");
            Assert.IsTrue(Wears(third, _outline), "The new front did not receive the outline on arrival.");
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
            _spawner.FlowBoardColumn(0, 0.3f, 1.7f);
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.3f, 1.7f);

            DOTween.CompleteAll();

            Assert.That(
                Vector3.Distance(survivor.position, frontStart), Is.LessThan(0.001f),
                "The third cube did not reach the front: the column flowed short by a cell.");
        }

        /// <summary>
        /// A column's second flow re-targets the tweens its first flow built instead of building
        /// new ones. A DOTween shortcut allocates two closures per call, and a slide runs for every
        /// survivor on every shot: measured at 65% of all firing-time garbage
        /// (Docs/PERFORMANCE.md, step 3).
        /// </summary>
        [Test]
        public void ASecondFlow_DoesNotAllocate()
        {
            _spawner.Construct(
                new BoardModel(1, 4, 1), new ShooterQueue(new Shooter[0][]), new SlotRow(1), new NoMaterials());

            // The first flow may build each survivor's tween; that is the one-time cost.
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.3f, 1.7f);
            _spawner.PopFrontCube(0);

            Assert.That(() => { _spawner.FlowBoardColumn(0, 0.3f, 1.7f); }, Is.Not.AllocatingGCMemory(),
                "The second flow allocated: the survivors' slide tweens are being rebuilt per shot.");
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
            _spawner.FlowBoardColumn(0, 0.3f, 1.7f);
            DOTween.CompleteAll();

            for (int i = 0; i < spawned.Length; i++)
            {
                if (cubes.GetChild(i) == first.transform) continue;
                Assert.That(Vector3.Distance(cubes.GetChild(i).position, spawned[i]), Is.LessThan(0.001f),
                    "A survivor slid while the front stack still had a cube standing.");
            }

            // Ground of the front stack dies: now the next row's ground cube reaches the front.
            _spawner.PopFrontCube(0);
            _spawner.FlowBoardColumn(0, 0.3f, 1.7f);
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


        /// <summary>Builds a ShooterView stand-in: one renderer to colour, a TMP counter, an outline material.</summary>
        void BuildShooterPrefab()
        {
            var prefabObject = new GameObject("ShooterPrefab");
            prefabObject.AddComponent<MeshRenderer>();
            var counter = new GameObject("Counter").AddComponent<TextMeshPro>();
            counter.transform.SetParent(prefabObject.transform);
            _outline = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _shooterPrefab = prefabObject.AddComponent<ShooterView>();

            var serialized = new SerializedObject(_shooterPrefab);
            SerializedProperty parts = serialized.FindProperty("_coloredParts");
            parts.arraySize = 1;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = prefabObject.GetComponent<MeshRenderer>();
            serialized.FindProperty("_ammoText").objectReferenceValue = counter;
            serialized.FindProperty("_outline").objectReferenceValue = _outline;
            serialized.ApplyModifiedProperties();

            AssignField(_spawner, "_prefabs.Shooter", _shooterPrefab);
        }

        /// <summary>Builds a colour table with one material for every colour and another for concealment.</summary>
        TwoMaterials BuildMaterials()
        {
            _materials = new TwoMaterials
            {
                Colour = new Material(Shader.Find("Hidden/InternalErrorShader")),
                Hidden = new Material(Shader.Find("Hidden/InternalErrorShader")),
            };

            return _materials;
        }

        /// <summary>Whether a renderer carries a material in any of its slots.</summary>
        /// <param name="renderer">The renderer to inspect.</param>
        /// <param name="material">The material to look for.</param>
        static bool Wears(Renderer renderer, Material material)
        {
            foreach (Material slot in renderer.sharedMaterials)
            {
                if (slot == material)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>A colour table that tells revealed from concealed and nothing more.</summary>
        sealed class TwoMaterials : IColorMaterials
        {
            /// <summary>What every revealed colour wears.</summary>
            public Material Colour;

            /// <summary>What a concealed shooter wears.</summary>
            public Material Hidden;

            /// <summary>The one colour material, whatever the colour.</summary>
            /// <param name="color">Ignored.</param>
            public Material MaterialOf(BlastColor color) => Colour;

            /// <summary>The concealment material.</summary>
            public Material HiddenMaterial => Hidden;
        }

        #endregion
    }
}

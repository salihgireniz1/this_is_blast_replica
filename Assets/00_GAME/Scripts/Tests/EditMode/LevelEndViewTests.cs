// LevelEndViewTests - the overlay's binding, exercised without a scene.
// Layer: Tests (EditMode).
// Responsibility: the one mistake a humble view can make silently - a stream or a click
//   left unbound, so the overlay never appears or the button does nothing.
// NOT its responsibility: what the streams carry (LevelEndViewModelTests) or layout.

using Blast.Application;
using Blast.Domain;
using Blast.UI;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="LevelEndView"/>'s bindings.</summary>
    public sealed class LevelEndViewTests
    {
        #region Fields

        /// <summary>Every object the test made, destroyed in TearDown.</summary>
        GameObject _root;

        #endregion

        #region Public Methods

        /// <summary>Destroys the throwaway hierarchy.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        /// <summary>
        /// Once the loop decides, the panel is on and titled; a click on the button reaches
        /// the view model's Restart. Each assert names the binding that is missing.
        /// </summary>
        [Test]
        public void Deciding_ShowsThePanel_AndTheButtonRestarts()
        {
            _root = new GameObject("LevelEnd");
            var view = _root.AddComponent<LevelEndView>();
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform);
            panel.SetActive(false);
            var title = new GameObject("Title").AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(_root.transform);
            var button = new GameObject("Restart").AddComponent<Button>();
            button.transform.SetParent(_root.transform);
            AssignField(view, "_panel", panel);
            AssignField(view, "_title", title);
            AssignField(view, "_restart", button);

            var board = new BoardModel(columns: 1, rows: 1, layers: 1);
            board.Set(new Cell(0, 0, 0), BlastColor.Blue);
            var queue = new ShooterQueue(new[] { new[] { new Shooter(BlastColor.Red, 1, false) } });
            var loop = new GameLoop(board, queue, new SlotRow(1));
            var viewModel = new LevelEndViewModel(loop);
            int restarts = 0;
            viewModel.Restart.Subscribe(_ => restarts++);

            view.Construct(viewModel);
            loop.TrySelect(0, out _);

            Assert.IsTrue(panel.activeSelf, "The panel is not bound to IsShown.");
            Assert.AreEqual(LevelEndViewModel.LostTitle, title.text, "The title label is not bound to Title.");

            button.onClick.Invoke();

            Assert.AreEqual(1, restarts, "The button's click does not reach Restart.");
        }

        #endregion

        #region Private Methods

        /// <summary>Writes a serialized private field, the way the inspector would.</summary>
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
    }
}

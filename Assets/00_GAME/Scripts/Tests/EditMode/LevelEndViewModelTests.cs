// LevelEndViewModelTests - the overlay's state, derived from the loop without a scene.
// Layer: Tests (EditMode).
// Responsibility: the mistakes that stay silent in LevelEndViewModel's source - an overlay
//   that is visible before the level is decided, and a verdict wired to the wrong title.
// NOT its responsibility: the loop's verdicts (GameLoopTests) or the binding on the view,
//   which has no branches to test.

using Blast.Application;
using Blast.Domain;
using Blast.UI;
using NUnit.Framework;

namespace Blast.Tests
{
    /// <summary>Exercises <see cref="LevelEndViewModel"/>'s visibility and title.</summary>
    public sealed class LevelEndViewModelTests
    {
        #region Private Methods

        /// <summary>A loop over a single cube whose front colour is given, and one matching or
        /// mismatching shooter with one ammo in a one-slot row.</summary>
        /// <param name="cube">The board's only cube.</param>
        /// <param name="shooter">The queue's only shooter.</param>
        static GameLoop LoopOf(BlastColor cube, BlastColor shooter)
        {
            var board = new BoardModel(columns: 1, rows: 1, layers: 1);
            board.Set(new Cell(0, 0, 0), cube);
            var queue = new ShooterQueue(new[] { new[] { new Shooter(shooter, 1, false) } });

            return new GameLoop(board, queue, new SlotRow(1));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Hidden while the level is played, shown with the win title the moment the last
        /// cube dies. A view model that starts visible, or that shows on the selection
        /// rather than the decision, fails here.
        /// </summary>
        [Test]
        public void AWin_ShowsLevelComplete()
        {
            GameLoop loop = LoopOf(BlastColor.Red, BlastColor.Red);
            var viewModel = new LevelEndViewModel(loop);

            loop.TrySelect(0, out int slot);

            Assert.IsFalse(viewModel.IsShown.CurrentValue, "The overlay showed before the level was decided.");

            loop.TryShoot(slot, out _);

            Assert.IsTrue(viewModel.IsShown.CurrentValue, "The win did not show the overlay.");
            Assert.AreEqual(LevelEndViewModel.WonTitle, viewModel.Title.CurrentValue, "A win showed the wrong title.");
            Assert.AreEqual(LevelEndViewModel.RestartLabel, viewModel.ButtonLabel.CurrentValue, "A win does not offer RESTART; the brief replays the same level after a win.");
        }

        /// <summary>A stuck row shows the overlay with the fail title.</summary>
        [Test]
        public void ALoss_ShowsLevelFailed()
        {
            GameLoop loop = LoopOf(BlastColor.Blue, BlastColor.Red);
            var viewModel = new LevelEndViewModel(loop);

            loop.TrySelect(0, out _);

            Assert.IsTrue(viewModel.IsShown.CurrentValue, "The loss did not show the overlay.");
            Assert.AreEqual(LevelEndViewModel.LostTitle, viewModel.Title.CurrentValue, "A loss showed the wrong title.");
            Assert.AreEqual(LevelEndViewModel.RestartLabel, viewModel.ButtonLabel.CurrentValue, "A loss does not offer RESTART; the player would think they move on.");
        }

        #endregion
    }
}

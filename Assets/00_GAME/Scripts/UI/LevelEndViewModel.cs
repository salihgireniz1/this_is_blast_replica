// LevelEndViewModel - the win/fail overlay's state, as R3 streams a view can bind to.
// Layer: UI.
// Responsibility: turning the loop's Decided announcement into what the overlay shows -
//   whether it is visible and which title it wears - and publishing the player's restart
//   intent for whoever owns the scene to act on.
// NOT its responsibility: anything visual (LevelEndView binds these streams to UGUI) and
//   the restart itself. Restart is a command this type only raises; Bootstrap decides that
//   it means a scene reload. Keeping that here would make the view model depend on Unity.
//
// Pure C#: no MonoBehaviour, no scene, so it is tested in EditMode with a real GameLoop.

using Blast.Application;
using R3;

namespace Blast.UI
{
    /// <summary>What the level-end overlay shows, derived from the loop's verdict.</summary>
    public sealed class LevelEndViewModel
    {
        #region Fields

        /// <summary>The title a won level shows.</summary>
        public const string WonTitle = "Level Complete";

        /// <summary>The title a lost level shows.</summary>
        public const string LostTitle = "Level Failed";

        /// <summary>Backs IsShown; flips once, on the decision.</summary>
        readonly ReactiveProperty<bool> _isShown = new ReactiveProperty<bool>(false);

        /// <summary>Backs Title; written before IsShown flips so a shown overlay is never untitled.</summary>
        readonly ReactiveProperty<string> _title = new ReactiveProperty<string>(string.Empty);

        #endregion

        #region Properties

        /// <summary>Whether the overlay is visible: false while playing, true once decided.</summary>
        public ReadOnlyReactiveProperty<bool> IsShown => _isShown;

        /// <summary>The title matching the verdict; empty while playing.</summary>
        public ReadOnlyReactiveProperty<string> Title => _title;

        /// <summary>The player's restart intent. The view executes it; Bootstrap subscribes.</summary>
        public ReactiveCommand<Unit> Restart { get; } = new ReactiveCommand<Unit>();

        #endregion

        #region Public Methods

        /// <summary>Listens to the loop for the one announcement this overlay exists for.</summary>
        /// <param name="loop">The level's loop.</param>
        public LevelEndViewModel(GameLoop loop)
        {
            loop.Decided += OnDecided;
        }

        #endregion

        #region Private Methods

        /// <summary>Titles the overlay for the verdict, then shows it.</summary>
        /// <param name="verdict">How the level ended.</param>
        void OnDecided(GameVerdict verdict)
        {
            _title.Value = verdict == GameVerdict.Won ? WonTitle : LostTitle;
            _isShown.Value = true;
        }

        #endregion
    }
}

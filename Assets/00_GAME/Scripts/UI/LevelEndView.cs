// LevelEndView - the win/fail overlay on screen: a panel, a title, a restart button.
// Layer: UI.
// Responsibility: binding LevelEndViewModel's streams to UGUI, and nothing else. The panel
//   follows IsShown, the label follows Title, the button executes Restart.
// NOT its responsibility: a single decision. There is no if in this file; what to show
//   and when is the view model's, what restart means is Bootstrap's. A humble object: the
//   only mistake it can make is a binding left out, which LevelEndViewTests catches.
//
// Bound in Construct rather than Start because the scope hands the view model over from
// its Configure, which runs before any Start; a decision cannot arrive before a tap, so
// nothing is missed either way.

using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.UI
{
    /// <summary>Shows the level-end overlay the view model describes.</summary>
    public sealed class LevelEndView : MonoBehaviour
    {
        #region Fields

        /// <summary>The overlay root; switched on when the level is decided.</summary>
        [Tooltip("The overlay's root object. Off while playing, on once the level is decided.")]
        [SerializeField] GameObject _panel;

        /// <summary>The label wearing the verdict's title.</summary>
        [Tooltip("The title label; shows Level Complete or Level Failed.")]
        [SerializeField] TMP_Text _title;

        /// <summary>The button that asks for a restart.</summary>
        [Tooltip("The restart button.")]
        [SerializeField] Button _restart;

        #endregion

        #region Public Methods

        /// <summary>Binds the overlay to the view model. Subscriptions die with this object.</summary>
        /// <param name="viewModel">The overlay's state.</param>
        public void Construct(LevelEndViewModel viewModel)
        {
            viewModel.IsShown.Subscribe(shown => _panel.SetActive(shown)).AddTo(this);
            viewModel.Title.Subscribe(title => _title.text = title).AddTo(this);
            _restart.onClick.AddListener(() => viewModel.Restart.Execute(Unit.Default));
        }

        #endregion
    }
}

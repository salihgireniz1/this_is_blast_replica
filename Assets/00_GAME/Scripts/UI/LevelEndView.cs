// LevelEndView - the win/fail overlay on screen: a panel, a title, a restart button.
// Layer: UI.
// Responsibility: binding LevelEndViewModel's streams to UGUI, and the entrance the panel
//   makes when IsShown flips: a fade of the whole panel and a pop of the title. The label
//   follows Title, the button executes Restart.
// NOT its responsibility: a single decision. There is no if in this file; what to show
//   and when is the view model's, what restart means is Bootstrap's. A humble object: the
//   only mistake it can make is a binding left out, which LevelEndViewTests catches.
//
// The fade uses DOTween.To on the CanvasGroup's alpha rather than CanvasGroup.DOFade:
// that extension lives in DOTween's UI module, which compiles into the Plugins assembly
// an asmdef cannot reference. The core tween does the same job with no reference at all.
//
// Bound in Construct rather than Start because the scope hands the view model over from
// its Configure, which runs before any Start; a decision cannot arrive before a tap, so
// nothing is missed either way.

using DG.Tweening;
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

        /// <summary>The panel's CanvasGroup, faded in on show.</summary>
        [Tooltip("The CanvasGroup on the panel; its alpha fades from 0 to 1 when the overlay shows.")]
        [SerializeField] CanvasGroup _panelGroup;

        /// <summary>How long the panel takes to fade in.</summary>
        [Tooltip("Seconds the panel takes to fade from invisible to solid.")]
        [SerializeField] float _fadeDuration = 0.3f;

        /// <summary>How long the title takes to pop from nothing to full size.</summary>
        [Tooltip("Seconds the title takes to pop from zero to full size, with an overshoot.")]
        [SerializeField] float _titlePopDuration = 0.4f;

        /// <summary>The label wearing the verdict's title.</summary>
        [Tooltip("The title label; shows Level Complete or Level Failed.")]
        [SerializeField] TMP_Text _title;

        /// <summary>The button that asks for the reload.</summary>
        [Tooltip("The button that reloads the scene: NEXT after a win, RESTART after a loss.")]
        [SerializeField] Button _restart;

        /// <summary>The button's label; follows ButtonLabel.</summary>
        [Tooltip("The label on the button; shows NEXT or RESTART.")]
        [SerializeField] TMP_Text _buttonLabel;

        #endregion

        #region Public Methods

        /// <summary>Binds the overlay to the view model. Subscriptions die with this object.</summary>
        /// <param name="viewModel">The overlay's state.</param>
        public void Construct(LevelEndViewModel viewModel)
        {
            viewModel.IsShown.Subscribe(Show).AddTo(this);
            viewModel.Title.Subscribe(title => _title.text = title).AddTo(this);
            viewModel.ButtonLabel.Subscribe(label => _buttonLabel.text = label).AddTo(this);
            _restart.onClick.AddListener(() => viewModel.Restart.Execute(Unit.Default));
        }

        #endregion

        #region Private Methods

        /// <summary>Switches the panel to the given state; on show, fades it in and pops the title.</summary>
        /// <param name="shown">Whether the overlay is visible.</param>
        void Show(bool shown)
        {
            _panel.SetActive(shown);

            // Both start from nothing on every show, so a replay of the stream (or a future
            // hide-and-show) never inherits a half-faded state. Fire-and-forget: nothing
            // waits for the entrance, the button is clickable from the first frame.
            _panelGroup.alpha = 0f;
            DOTween.To(() => _panelGroup.alpha, alpha => _panelGroup.alpha = alpha, 1f, _fadeDuration);
            _title.transform.localScale = Vector3.zero;
            _title.transform.DOScale(Vector3.one, _titlePopDuration).SetEase(Ease.OutBack);
        }

        #endregion
    }
}

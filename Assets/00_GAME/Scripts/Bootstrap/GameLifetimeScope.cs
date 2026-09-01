// GameLifetimeScope - the composition root: the one place that knows how the game is wired.
// Layer: Bootstrap.
// Responsibility: registering the authored assets every later layer resolves, and the one
//   engine setting that has no home in an asset file.
// NOT its responsibility: any game logic, and any decision about what a dependency does.
//   It only says which instance answers to which type; behaviour lives in the layer it belongs
//   to. Nothing here is allowed to run gameplay code.
//
// Why the assets arrive through the inspector rather than a Resources.Load or an asset path
// constant: a path is a string nothing verifies, and it resolves at runtime in a build. A
// serialized reference is checked by Unity, survives a rename, and is visible to a test.

using Blast.Infrastructure;
using DG.Tweening;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Blast.Bootstrap
{
    /// <summary>
    /// Builds the container the whole game resolves from.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
        #region Fields

        /// <summary>Tweeners reserved up front. Worst case is 360 cubes, each able to animate.</summary>
        const int TweenersCapacity = 1500;

        /// <summary>Sequences reserved up front. Only the scripted set pieces use sequences.</summary>
        const int SequencesCapacity = 100;

        /// <summary>The authored colour table. Assigned in the inspector.</summary>
        [SerializeField] PaletteData _palette;

        /// <summary>The authored tween timings. Assigned in the inspector.</summary>
        [SerializeField] JuiceConfig _juice;

        #endregion

        #region Properties

        /// <summary>The palette this scope will register. Exposed so a test can see the wiring.</summary>
        public PaletteData Palette => _palette;

        /// <summary>The juice config this scope will register. Exposed so a test can see the wiring.</summary>
        public JuiceConfig Juice => _juice;

        #endregion

        #region Protected Methods

        /// <summary>Registers the authored assets and prepares the tween engine.</summary>
        /// <param name="builder">The container being built.</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // The one part of plan E4 with no field in DOTweenSettings.asset. Everything else the
            // plan lists there - safe mode, pooling, log level - is already in the asset, and
            // DOTween reads it itself the first time anything tweens. Reserved once so the board
            // filling up never triggers a mid-game resize.
            DOTween.SetTweensCapacity(TweenersCapacity, SequencesCapacity);

            builder.RegisterInstance(_palette);
            builder.RegisterInstance(_juice);
        }

        #endregion
    }
}

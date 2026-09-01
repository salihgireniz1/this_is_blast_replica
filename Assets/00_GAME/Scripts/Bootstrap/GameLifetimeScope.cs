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
using Blast.Presentation;
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

        /// <summary>The level file the game boots into. Assigned in the inspector.</summary>
        [SerializeField] TextAsset _level;

        /// <summary>The spawner that builds the level on screen. Assigned in the inspector.</summary>
        [SerializeField] LevelSpawner _spawner;

        #endregion

        #region Properties

        /// <summary>The palette this scope will register. Exposed so a test can see the wiring.</summary>
        public PaletteData Palette => _palette;

        /// <summary>The juice config this scope will register. Exposed so a test can see the wiring.</summary>
        public JuiceConfig Juice => _juice;

        /// <summary>The level file this scope will boot. Exposed so a test can see the wiring.</summary>
        public TextAsset Level => _level;

        /// <summary>The spawner this scope will construct. Exposed so a test can see the wiring.</summary>
        public LevelSpawner Spawner => _spawner;

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

            // The level is parsed once, here, because this is the only layer that may see
            // both the parser (Infrastructure) and the views (Presentation). The domain
            // models are registered individually - the game loop will resolve them without
            // ever learning a level file exists.
            ParsedLevel level = LevelParser.Parse(_level.text);
            builder.RegisterInstance(level.Board);
            builder.RegisterInstance(level.Shooters);
            builder.RegisterInstance(level.Slots);

            // The spawner is a scene object, so its dependencies are handed over directly;
            // registering it in the container would only re-route the same handshake.
            _spawner.Construct(level.Board, level.Shooters, new PaletteColorMaterials(_palette));
        }

        #endregion
    }
}

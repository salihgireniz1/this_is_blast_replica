// GameLifetimeScope - the composition root: the one place that knows how the game is wired.
// Layer: Bootstrap.
// Responsibility: parsing the level, registering the models, the use case, the palette seam
//   and the scene components, so that VContainer builds the object graph and calls every
//   [Inject] Construct; plus the one engine setting that has no home in an asset file.
// NOT its responsibility: any game logic, and any decision about what a dependency does.
//   It only says which type answers to which; behaviour lives in the layer it belongs to.
//   Nothing here is allowed to run gameplay code.
//
// Why the assets arrive through the inspector rather than a Resources.Load or an asset path
// constant: a path is a string nothing verifies, and it resolves at runtime in a build. A
// serialized reference is checked by Unity, survives a rename, and is visible to a test.

using Blast.Application;
using Blast.Infrastructure;
using Blast.Presentation;
using Blast.UI;
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

        /// <summary>
        /// Frames per second the player is asked for. Android's default is 30 (measured:
        /// Docs/PERFORMANCE.md, baseline), which halves the fire rhythm's visible frames.
        /// Asking for more than the display can show is harmless: the OS picks its highest
        /// refresh rate at or below this, so a 90 Hz phone runs at 90 and a 60 Hz one at 60.
        /// </summary>
        const int TargetFrameRate = 120;

        /// <summary>The authored colour table. Assigned in the inspector.</summary>
        [Tooltip("The colour table every cube and shooter renders from. Swap it for another palette to retheme the whole game.")]
        [SerializeField] PaletteData _palette;

        /// <summary>The level this scene boots. Assigned in the inspector.</summary>
        [Tooltip("The JSON level the scene boots. The case ships one; swap this field to play another. It must live in Assets/00_GAME/Levels so LevelFileTests checks it.")]
        [SerializeField] TextAsset _level;

        /// <summary>The spawner that builds the level on screen. Assigned in the inspector.</summary>
        [Tooltip("The scene's LevelSpawner; it is handed the parsed level.")]
        [SerializeField] LevelSpawner _spawner;

        /// <summary>The director that runs the play session. Assigned in the inspector.</summary>
        [Tooltip("The scene's GameDirector; it is handed the domain models.")]
        [SerializeField] GameDirector _director;

        /// <summary>The overlay that shows the verdict. Assigned in the inspector.</summary>
        [Tooltip("The scene's LevelEndView; it is handed the overlay's view model.")]
        [SerializeField] LevelEndView _levelEnd;

        #endregion

        #region Properties

        /// <summary>The palette this scope will register. Exposed so a test can see the wiring.</summary>
        public PaletteData Palette => _palette;

        /// <summary>The level this scope boots. Exposed so a test can see the wiring.</summary>
        public TextAsset Level => _level;

        /// <summary>The spawner this scope will construct. Exposed so a test can see the wiring.</summary>
        public LevelSpawner Spawner => _spawner;

        /// <summary>The director this scope will construct. Exposed so a test can see the wiring.</summary>
        public GameDirector Director => _director;

        /// <summary>The overlay this scope will construct. Exposed so a test can see the wiring.</summary>
        public LevelEndView LevelEnd => _levelEnd;

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
            UnityEngine.Application.targetFrameRate = TargetFrameRate;

            // The level is parsed once, here, because this is the only layer that may see
            // both the parser (Infrastructure) and the views (Presentation). The three
            // models are registered on their own: GameLoop and the views resolve them
            // without ever learning a level file exists.
            //
            // One level, no stored progress: the brief ships a single sample level and
            // replays it after a win as well as a loss, so an index to remember would
            // never move off zero.
            ParsedLevel level = LevelParser.Parse(_level.text);
            builder.RegisterInstance(level.Board);
            builder.RegisterInstance(level.Shooters);
            builder.RegisterInstance(level.Slots);

            // Presentation asks for IColorMaterials and never sees PaletteData; this is the
            // one line where the seam meets the asset.
            builder.RegisterInstance(_palette);
            builder.Register<IColorMaterials, PaletteColorMaterials>(Lifetime.Singleton);

            builder.Register<GameLoop>(Lifetime.Singleton);
            builder.Register<LevelEndViewModel>(Lifetime.Singleton);

            // The scene components already exist, so they are registered as instances: the
            // container calls each one's [Inject] Construct as it builds, in this order. The
            // director asks for the spawner, so the level is spawned before the director
            // holds it whichever way the order went.
            builder.RegisterComponent(_spawner);
            builder.RegisterComponent(_director);
            builder.RegisterComponent(_levelEnd);

            // The overlay only raises the restart intent; that it means "reload this scene"
            // is a composition decision, so the listener lives in this layer.
            builder.RegisterEntryPoint<SceneRestarter>();
        }

        #endregion
    }
}

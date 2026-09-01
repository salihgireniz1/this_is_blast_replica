// GameVerdict - whether the level is still being played, and how it ended if not.
// Layer: Application.
// Responsibility: naming the three states a level can be in. Nothing more.
// NOT its responsibility: deciding transitions - GameLoop moves between them, GameRules
//   supplies the conditions.

namespace Blast.Application
{
    /// <summary>The state of the running level.</summary>
    public enum GameVerdict
    {
        /// <summary>Input is accepted and shooters may fire.</summary>
        Playing,

        /// <summary>Every cube is destroyed. The level is over and frozen.</summary>
        Won,

        /// <summary>Every slot is stuck. The level is over and frozen.</summary>
        Lost,
    }
}

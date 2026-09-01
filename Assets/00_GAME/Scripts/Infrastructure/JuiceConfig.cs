// JuiceConfig - the authored tween timings, edited as an asset rather than in code.
// Layer: Infrastructure.
// Responsibility: holding the durations, eases and punch parameters section C of the plan
//   specifies, so that retuning the feel of the game never recompiles anything.
// NOT its responsibility: playing anything. No DOTween call lives here - Presentation reads
//   these values and drives the tweens. Groups for cannon movement, firing, merge and the
//   win sequence join this asset in the phase that first animates them; an unused field
//   nobody can verify yet is worse than a missing one.

using System;
using DG.Tweening;
using UnityEngine;

namespace Blast.Infrastructure
{
    /// <summary>How long a tween runs and the curve it runs on.</summary>
    [Serializable]
    public struct Timing
    {
        /// <summary>Seconds the tween takes. Zero would make it finish before it is seen.</summary>
        public float Duration;

        /// <summary>The DOTween ease applied over that duration.</summary>
        public Ease Ease;
    }

    /// <summary>A punch tween: a timing plus the shake it applies.</summary>
    [Serializable]
    public struct Punch
    {
        /// <summary>How long the punch takes and how it settles.</summary>
        public Timing Timing;

        /// <summary>How far the punch throws the transform, in world units.</summary>
        public float Strength;

        /// <summary>How many times it crosses back over the rest position.</summary>
        public int Vibrato;

        /// <summary>How far past the rest position it is allowed to overshoot, 0 to 1.</summary>
        [Range(0f, 1f)] public float Elasticity;
    }

    /// <summary>
    /// The tuning asset. Every number here is meant to be argued about in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Juice", menuName = "Blast/Juice Config")]
    public sealed class JuiceConfig : ScriptableObject
    {
        #region Fields

        /// <summary>Timings for a board cube.</summary>
        [SerializeField] CubeJuice _cube;

        #endregion

        #region Properties

        /// <summary>Timings for a board cube.</summary>
        public CubeJuice Cube => _cube;

        #endregion

        #region Nested Types

        /// <summary>The five things a cube ever does.</summary>
        [Serializable]
        public struct CubeJuice
        {
            /// <summary>Scaling up as it leaves the pool.</summary>
            public Timing Appear;

            /// <summary>Scaling down to zero before it returns to the pool.</summary>
            public Timing Death;

            /// <summary>
            /// Sliding into the gap the dead cube left. Delayed by exactly Death.Duration, so
            /// that no cube starts moving while the one in front of it is still shrinking -
            /// that is a relationship between two numbers, not a third number to author.
            /// </summary>
            public Timing Slide;

            /// <summary>The small bounce that lands the slide.</summary>
            public Punch Settle;

            /// <summary>The flinch when a projectile connects.</summary>
            public Punch Hit;
        }

        #endregion
    }
}

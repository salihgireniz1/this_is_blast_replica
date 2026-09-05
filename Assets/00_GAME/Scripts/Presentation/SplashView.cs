// SplashView - the pooled splash a shot plays at the muzzle and on the hit cube, in the
//   shooter's colour.
// Layer: Presentation (humble: holds references and applies what it is told, decides nothing).
// Responsibility: pushing one tint into every particle system the splash is made of. The
//   prefab is two systems (the burst and its inner core) and both emit white until told
//   otherwise; a caller that tinted the root alone would leave the core white.
// NOT its responsibility: playing, stopping or ending. Play On Awake starts it when the pool
//   switches it on, Stop Action Disable switches it off, and the pool reads only that.
//
// Why startColor and not a material: both systems render through URP's Particles/Unlit,
// which multiplies the vertex colour in. startColor is that vertex colour: a native setter
// per system, no material instance, no property block, no allocation, no extra draw call.

using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>The visual of one splash: every particle system it is made of, tinted as one.</summary>
    public sealed class SplashView : MonoBehaviour
    {
        #region Fields

        /// <summary>Every particle system the splash is made of. Assigned in the prefab: the root and the inner core.</summary>
        [Tooltip("Every particle system in this splash. Tint reaches all of them; one left out keeps emitting white.")]
        [SerializeField] ParticleSystem[] _systems;

        #endregion

        #region Public Methods

        /// <summary>Sets the colour the next particles are born with, on every system.</summary>
        /// <param name="tint">The flat tint of the shooter's colour.</param>
        public void Tint(Color tint)
        {
            // Set before the frame's particle update, so the burst Play On Awake just started
            // emits in this colour: startColor is read at each particle's birth, not at play.
            foreach (var system in _systems)
            {
                var main = system.main;
                main.startColor = tint;
            }
        }

        #endregion
    }
}

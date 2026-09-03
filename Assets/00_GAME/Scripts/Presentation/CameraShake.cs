// CameraShake - the tick the camera gives when a bullet lands.
// Layer: Presentation.
// Responsibility: one small positional shake on the camera it sits on, restarted from the top
//   on every impact. The shake is a single DOTween tween built once and kept alive, so a kick
//   costs a Restart and nothing else: no tween, no segment arrays, no allocation per shot.
// NOT its responsibility: when an impact happens (the director calls Kick), or any other camera
//   motion; the rig is static and this is the only thing that moves it.
//
// Why one reused tween rather than DOShakePosition per impact: DOShake builds its segment
// arrays on every call, and five slots at the fire interval is ~40 impacts a second - a steady
// allocation the frame budget forbids. Restart also answers overlap: a kick during a shake
// snaps back to the rest and starts over, so offsets never stack. The cost is that every shake
// follows the same random pattern; at a twentieth of a unit nobody can tell. Rejected: a
// Cinemachine impulse (a Brain driving the camera every frame plus a package, for a wobble
// measured at 1-2 px in the original) and a hand-written LateUpdate (more lines for the same
// zero-allocation result).

using DG.Tweening;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Kicks the camera a little on impact, through one tween reused for every shot.</summary>
    public sealed class CameraShake : MonoBehaviour
    {
        #region Fields

        /// <summary>How far the camera moves, in world units. The original measures 1-2 px on a 37 px cell.</summary>
        [Tooltip("World units the camera is thrown by an impact. 0.05 is the original's tick; 0.2 reads as an earthquake.")]
        [SerializeField] float _amplitude = 0.05f;

        /// <summary>How long one shake lasts before the camera is back at rest.</summary>
        [Tooltip("Seconds one impact shakes the camera for.")]
        [SerializeField] float _duration = 0.12f;

        /// <summary>Oscillations per second: DOTween cuts the shake into vibrato x duration segments.</summary>
        [Tooltip("Oscillations per SECOND. Segments = vibrato x duration, rounded down, at least 2.")]
        [SerializeField] int _vibrato = 20;

        /// <summary>The one shake tween, built on the first kick and never auto-killed.</summary>
        Tween _shake;

        #endregion

        #region Public Methods

        /// <summary>Shakes the camera from the top; a shake already running starts over.</summary>
        public void Kick()
        {
            if (_shake == null)
            {
                // The start position is captured here, so this must run with the camera at rest.
                _shake = transform.DOShakePosition(_duration, _amplitude, _vibrato)
                    .SetAutoKill(false)
                    .Pause();
            }

            _shake.Restart();
        }

        #endregion
    }
}

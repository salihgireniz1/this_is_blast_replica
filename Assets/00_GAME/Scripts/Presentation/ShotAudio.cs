// ShotAudio - the sound of a shot, capped at a few voices.
// Layer: Presentation.
// Responsibility: playing the shot clip through a small fixed set of AudioSources, round-robin,
//   so a burst of shots never stacks more voices than the scene authored. The oldest voice is
//   the one restarted, which is how a shot older than the cap gets cut.
// NOT its responsibility: when a shot happens (the director calls Play), which clip plays or how
//   loud (both live on the authored AudioSources), or any other sound in the game.
//
// Why not PlayOneShot or an AudioSource per splash: both stack one voice per shot with no cap,
// and five slots chain-firing at the fire interval is a wall of overlapping splashes. Restarting
// a source with Play is free of allocation and needs no bookkeeping beyond the next index.

using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Plays the shot sound through a fixed ring of voices; the count is the concurrency cap.</summary>
    public sealed class ShotAudio : MonoBehaviour
    {
        #region Fields

        /// <summary>The voices, each carrying the clip. Their count is how many shots can sound at once.</summary>
        [Tooltip("One AudioSource per shot sound that may overlap. Add one to allow one more; when all are busy the oldest shot is cut.")]
        [SerializeField] AudioSource[] _voices;

        /// <summary>How far a shot's pitch may wander from 1, either way. Five slots firing the one sample eight times a second read as a buzzer without it.</summary>
        [Tooltip("How far each shot's pitch may wander from 1, either way. 0.1 makes a burst read as many shots instead of one repeated sample; 0 disables it.")]
        [SerializeField, Range(0f, 0.5f)] float _pitchJitter = 0.1f;

        /// <summary>Which voice the next shot takes.</summary>
        int _next;

        #endregion

        #region Public Methods

        /// <summary>Sounds one shot at a slightly different pitch each time, restarting the oldest voice when every voice is busy.</summary>
        public void Play()
        {
            AudioSource voice = _voices[_next];

            // Set before Play: a pitch changed on a playing source bends the sound mid-flight.
            voice.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            voice.Play();
            _next = (_next + 1) % _voices.Length;
        }

        #endregion
    }
}

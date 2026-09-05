// CollapseTweens - a grow-only pool of scale-to-zero tweens whose target and shape can change.
// Layer: Presentation.
// Responsibility: shrinking any transform to nothing without building a tween per death.
//   A cube dies exactly once, so a reused tween PER CUBE would save nothing: the closures
//   would still be built at the death. The pool is shared instead: each entry owns a
//   tween whose closures read a Target field, and a death only re-points the field. After
//   the first few deaths no collapse allocates (Docs/PERFORMANCE.md, step 3).
// NOT its responsibility: when a cube dies, or what happens after (the director destroys
//   it and flows the column).

using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Shrinks transforms to zero scale through a shared, reused set of tweens.</summary>
    public sealed class CollapseTweens
    {
        #region Fields

        /// <summary>Every entry ever built; an entry whose tween is not playing is free.</summary>
        readonly List<Entry> _entries = new List<Entry>();

        #endregion

        #region Public Methods

        /// <summary>Collapses a transform along a curve, from its current scale to nothing.</summary>
        /// <param name="target">The transform to collapse.</param>
        /// <param name="duration">How long the whole collapse takes.</param>
        /// <param name="curve">The death's shape: x is the fraction of the duration, y is the scale as a
        /// multiple of the scale at impact - 1 at the start, 0 at the end, above 1 wherever a flinch is wanted.</param>
        /// <returns>The collapse; await it with AwaitForComplete, since a reused tween never kills.</returns>
        public Tween Play(Transform target, float duration, AnimationCurve curve)
        {
            // ponytail: linear scan; at most a handful of cubes die at once.
            Entry entry = FirstIdle() ?? Add();
            entry.Target = target;

            // A reference, never wrapped: the ease built in Add reads it every frame, so a
            // death allocates nothing and a curve edited in the inspector is picked up live.
            entry.Curve = curve;
            entry.Tween.ChangeValues(target.localScale, Vector3.zero, duration);
            entry.Tween.Restart();
            return entry.Tween;
        }

        /// <summary>Kills every tween; the owner calls this when it dies, since none auto-kills.</summary>
        public void KillAll()
        {
            foreach (Entry entry in _entries)
            {
                entry.Tween.Kill();
            }

            _entries.Clear();
        }

        #endregion

        #region Private Methods

        /// <summary>The first entry whose tween has finished, or null when every one is mid-collapse.</summary>
        Entry FirstIdle()
        {
            foreach (Entry entry in _entries)
            {
                if (!entry.Tween.IsPlaying())
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Builds one more entry; the only place a collapse allocates.</summary>
        Entry Add()
        {
            var entry = new Entry();
            // The closures capture the entry, not a transform, so re-pointing Target re-aims them.
            entry.Tween = DOTween.To(() => entry.Target.localScale, scale => entry.Target.localScale = scale, Vector3.zero, 1f)
                // DOTween's ease answers "how far from start to end"; the curve answers "how big
                // is the cube". With the end at zero the two are one minus the other.
                .SetEase((time, duration, _, _) => 1f - entry.Curve.Evaluate(time / duration))
                .SetAutoKill(false)
                .Pause();
            _entries.Add(entry);
            return entry;
        }

        #endregion

        #region Nested Types

        /// <summary>One reusable tween and the transform it currently shrinks.</summary>
        sealed class Entry
        {
            /// <summary>The transform the tween's closures read and write right now.</summary>
            public Transform Target;

            /// <summary>The tween, built once and restarted per death.</summary>
            public TweenerCore<Vector3, Vector3, VectorOptions> Tween;

            /// <summary>The shape the tween's ease reads right now: scale over normalised time.</summary>
            public AnimationCurve Curve;
        }

        #endregion
    }
}

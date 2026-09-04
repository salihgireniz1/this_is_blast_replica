// ShotAudioTests - every shot sounds a little different, and never out of range.
// Layer: Tests (EditMode).
// Responsibility: proving that the pitch jitter stays inside the authored band (a sign
//   slip or a missing clamp would send a voice to pitch 0 or 2, which is silence or a
//   chipmunk) and that it actually varies (a jitter applied once and never again is the
//   same repeated sample the feature exists to break up).
// NOT its responsibility: the clip, the volume or the voice cap; all three are authored
//   on the AudioSources.

using Blast.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>Behaviour tests for <see cref="ShotAudio"/>'s pitch jitter.</summary>
    public sealed class ShotAudioTests
    {
        #region Fields

        /// <summary>How far from 1 a voice's pitch may wander in these tests.</summary>
        const float Jitter = 0.1f;

        /// <summary>Enough shots for "every pitch came out exactly 1" to be impossible in practice.</summary>
        const int Shots = 24;

        /// <summary>The audio under test.</summary>
        ShotAudio _audio;

        /// <summary>Its voices, read back after the shots.</summary>
        AudioSource[] _voices;

        #endregion

        #region Public Methods

        /// <summary>Builds an audio object with two voices and the test jitter.</summary>
        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("ShotAudio");
            _voices = new[] { root.AddComponent<AudioSource>(), root.AddComponent<AudioSource>() };
            _audio = root.AddComponent<ShotAudio>();

            var serialized = new SerializedObject(_audio);
            SerializedProperty voices = serialized.FindProperty("_voices");
            voices.arraySize = _voices.Length;
            for (int i = 0; i < _voices.Length; i++)
            {
                voices.GetArrayElementAtIndex(i).objectReferenceValue = _voices[i];
            }
            serialized.FindProperty("_pitchJitter").floatValue = Jitter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Destroys the audio object.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_audio.gameObject);
        }

        /// <summary>Every shot's pitch lands inside 1 +/- jitter, and the shots do not all land on 1.</summary>
        [Test]
        public void Shots_VaryInPitchWithinTheJitter()
        {
            bool anyOffOne = false;

            for (int shot = 0; shot < Shots; shot++)
            {
                _audio.Play();

                foreach (AudioSource voice in _voices)
                {
                    Assert.That(voice.pitch, Is.InRange(1f - Jitter, 1f + Jitter),
                        "A shot's pitch left the authored band.");
                    anyOffOne |= !Mathf.Approximately(voice.pitch, 1f);
                }
            }

            Assert.IsTrue(anyOffOne, "Every shot played at pitch 1: the jitter is not applied.");
        }

        #endregion
    }
}

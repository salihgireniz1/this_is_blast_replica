// JuiceConfigTests - guards the tuning assets the animations read their timings from.
// Layer: Test (EditMode).
// Responsibility: that no timing in any authored juice asset is zero or negative.
// NOT its responsibility: whether 0.15 s feels better than 0.12 s, nor how many juice assets
//   the project keeps. Those numbers are there to be argued about in the inspector; a test
//   asserting them would fight that.

using System.Collections.Generic;
using System.Linq;
using Blast.Infrastructure;
using NUnit.Framework;
using UnityEditor;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies every authored juice asset stays within the range a tween can use.
    /// </summary>
    public sealed class JuiceConfigTests
    {
        #region Fields

        /// <summary>Name of the serialised field every timing block exposes.</summary>
        const string DurationField = "Duration";

        #endregion

        #region Public Methods

        /// <summary>Fails when any authored duration would make its tween finish instantly.</summary>
        [Test]
        public void EveryDuration_IsPositive()
        {
            foreach (var config in AllJuiceConfigs())
            {
                var iterator = new SerializedObject(config).GetIterator();

                // Walking the serialised tree rather than named fields on purpose: every timing
                // block added in a later phase is covered by this test the day it is added.
                while (iterator.NextVisible(true))
                {
                    if (iterator.name == DurationField)
                    {
                        Assert.Greater(iterator.floatValue, 0f,
                            $"{config.name}, {iterator.propertyPath} is not a usable duration.");
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>Every juice asset in the project, however many the designer keeps.</summary>
        /// <returns>The authored configs.</returns>
        static IEnumerable<JuiceConfig> AllJuiceConfigs()
        {
            return AssetDatabase.FindAssets($"t:{nameof(JuiceConfig)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<JuiceConfig>);
        }

        #endregion
    }
}

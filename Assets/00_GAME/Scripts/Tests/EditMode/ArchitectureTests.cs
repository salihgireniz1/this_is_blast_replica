// ArchitectureTests - enforces the layering rules at test time.
// Layer: Test (EditMode).
// Responsibility: the shape of the .asmdef files - which layers exist, whether the
//   dependency direction is one-way, and whether Domain stays engine-free.
// NOT its responsibility: the content of the code. (Source scans for FindObjectOfType
//   and .material come in a later chunk - there is no gameplay code to scan yet.)
//
// These tests read the asmdef files from disk rather than through reflection, so they hold
// even for a layer that contains no script yet - and they stay valid whatever else the test
// assembly happens to reference.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blast.Tests
{
    /// <summary>
    /// Verifies that the assembly definitions describe the intended layer architecture.
    /// </summary>
    public sealed class ArchitectureTests
    {
        #region Fields

        /// <summary>
        /// Maps each layer to the Blast layers it is allowed to reference.
        /// An empty entry means "may reference no other layer of ours".
        /// </summary>
        static readonly Dictionary<string, string[]> AllowedReferences = new()
        {
            ["Blast.Domain"]         = new string[0],
            ["Blast.Application"]    = new[] { "Blast.Domain" },
            ["Blast.Presentation"]   = new[] { "Blast.Domain", "Blast.Application" },
            ["Blast.UI"]             = new[] { "Blast.Domain", "Blast.Application" },
            ["Blast.Infrastructure"] = new[] { "Blast.Domain", "Blast.Application" },
            ["Blast.Bootstrap"]      = new[]
            {
                "Blast.Domain", "Blast.Application",
                "Blast.Presentation", "Blast.UI", "Blast.Infrastructure"
            },
        };

        /// <summary>Prefix Unity writes when an asmdef stores a reference as a GUID.</summary>
        const string GuidPrefix = "GUID:";

        #endregion

        #region Public Methods

        /// <summary>Fails when a layer is missing, or when an unplanned layer appears.</summary>
        [Test]
        public void AllLayerAssemblyDefinitions_Exist()
        {
            var found = LoadBlastAssemblyDefinitions().Keys;

            // Equivalence, not subset: a new layer nobody declared must fail just as loudly
            // as a missing one.
            CollectionAssert.AreEquivalent(AllowedReferences.Keys, found);
        }

        /// <summary>Fails when Domain is allowed to see UnityEngine.</summary>
        [Test]
        public void Domain_HasNoEngineReferences()
        {
            var domain = LoadBlastAssemblyDefinitions()["Blast.Domain"];

            // Without this flag a stray "using UnityEngine" in Domain would compile and no one
            // would notice. With it the build breaks - that is what protects the architecture.
            Assert.IsTrue(domain.noEngineReferences, "Blast.Domain must set noEngineReferences: true.");
        }

        /// <summary>Fails when any layer references a layer above itself.</summary>
        [Test]
        public void Dependencies_PointDownwardsOnly()
        {
            foreach (var (name, definition) in LoadBlastAssemblyDefinitions())
            {
                // Third-party references (UniTask, R3, VContainer) are out of scope here; this
                // rule only describes how our own layers are allowed to look at each other.
                var blastReferences = definition.references
                    .Select(ResolveReferenceName)
                    .Where(reference => reference.StartsWith("Blast."))
                    .ToArray();

                CollectionAssert.IsSubsetOf(
                    blastReferences, AllowedReferences[name],
                    $"{name} references a layer it is not allowed to see.");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads every asmdef whose name starts with "Blast.", keyed by assembly name.
        /// Test assemblies are excluded - they are allowed to reference everything.
        /// </summary>
        /// <returns>Assembly name to its parsed definition.</returns>
        /// <remarks>
        /// Parsed from the file on disk rather than through reflection on purpose: Unity emits
        /// no assembly for an asmdef that contains no scripts, and it never exposes the
        /// noEngineReferences flag at runtime.
        /// </remarks>
        static Dictionary<string, AssemblyDefinition> LoadBlastAssemblyDefinitions()
        {
            return AssetDatabase.FindAssets("t:AssemblyDefinitionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path)))
                .Where(definition => definition.name.StartsWith("Blast.")
                                     && !definition.name.StartsWith("Blast.Tests"))
                .ToDictionary(definition => definition.name);
        }

        /// <summary>Reduces an asmdef reference to a plain assembly name.</summary>
        /// <param name="reference">Either a plain name or a "GUID:..." entry.</param>
        /// <returns>The referenced assembly name.</returns>
        /// <remarks>
        /// Unity rewrites references as GUIDs once an asmdef is edited through the inspector,
        /// so both spellings have to be understood or the test breaks for the wrong reason.
        /// </remarks>
        static string ResolveReferenceName(string reference)
        {
            if (!reference.StartsWith(GuidPrefix)) return reference;

            var path = AssetDatabase.GUIDToAssetPath(reference.Substring(GuidPrefix.Length));
            return Path.GetFileNameWithoutExtension(path);
        }

        #endregion

        #region Nested Types

        /// <summary>The subset of the asmdef JSON schema these tests care about.</summary>
        [System.Serializable]
        class AssemblyDefinition
        {
            /// <summary>Assembly name, for example "Blast.Domain".</summary>
            public string name;

            /// <summary>Referenced assemblies, as plain names or "GUID:..." entries.</summary>
            public string[] references = new string[0];

            /// <summary>True when the assembly is compiled without the UnityEngine reference.</summary>
            public bool noEngineReferences;
        }

        #endregion
    }
}

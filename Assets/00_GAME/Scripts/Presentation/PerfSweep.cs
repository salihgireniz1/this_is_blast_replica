// PerfSweep - one build, every render variant, for Docs/PERFORMANCE.md step 2c.
// Layer: Presentation.
// Responsibility: walk a fixed list of render settings on a timer and log which one is live,
//   so PerfProbe's lines can be read per variant. Each variant is applied on top of the
//   scene's own values and undone before the next, so every number is one change against
//   the same baseline. The list is rewritten per experiment; the ledger records which list
//   produced which table.
// NOT its responsibility: measuring (PerfProbe) or shipping any of these settings. It runs
//   only where Debug.isDebugBuild is true and only when enabled in the inspector; everything
//   it touches is restored when the sweep ends or the component is disabled.
//
// Why a runtime sweep instead of one build per variant: an incremental Android build is
// three minutes and a variant is one line; six builds would have measured the same scene
// six times with six chances of the phone thermally throttling in between.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blast.Presentation
{
    /// <summary>Cycles through render-setting variants on a timer and logs each one, development builds only.</summary>
    public sealed class PerfSweep : MonoBehaviour
    {
        #region Fields

        /// <summary>Seconds each variant is left running; the probe logs once a second, so six lines each.</summary>
        const float VariantSeconds = 6f;

        /// <summary>The cube shader's keyword that skips the shadow-map sample per pixel.</summary>
        const string ReceiveShadowsOff = "_RECEIVE_SHADOWS_OFF";

        /// <summary>Scene objects switched off by the allocation variants, by name.</summary>
        const string LeanTouchObject = "LeanTouch";

        /// <summary>Scene objects switched off by the allocation variants, by name.</summary>
        const string EventSystemObject = "EventSystem";

        /// <summary>The variants, in order. Index 0 is the untouched baseline.</summary>
        static readonly string[] Names =
        {
            "baseline",
            "render scale 0.9",
            "render scale 0.8",
            "cubes receive off",
            "opaque cube shader",
            "opaque + receive off",
            "opaque + receive off + scale 0.8",
            "leantouch off",
            "eventsystem off",
        };

        /// <summary>Whether the sweep runs at all. Off by default; on for a measurement build.</summary>
        [Tooltip("Run the render-setting sweep on start. Leave off except for a measurement build.")]
        [SerializeField] bool _run;

        /// <summary>The camera whose post processing is toggled.</summary>
        [Tooltip("The camera whose post processing the sweep switches off and on.")]
        [SerializeField] Camera _camera;

        /// <summary>The cube shader without the alpha clip, swapped in by the opaque variants. Serialized so the build ships it.</summary>
        [Tooltip("The cube shader variant without the alpha clip.")]
        [SerializeField] Shader _opaqueShader;

        /// <summary>The pipeline asset in use, read once.</summary>
        UniversalRenderPipelineAsset _pipeline;

        /// <summary>Every board cube's renderer, collected on the first variant that needs them.</summary>
        MeshRenderer[] _cubeRenderers;

        /// <summary>The distinct cube materials, for the receive-shadows keyword and the shader swap.</summary>
        readonly List<Material> _cubeMaterials = new List<Material>();

        /// <summary>The cube materials' own shader, restored between variants.</summary>
        Shader _cubeShader;

        /// <summary>The asset's own render scale, restored between variants.</summary>
        float _renderScale;

        /// <summary>
        /// True once the baseline has been read. Unity calls OnDisable even when Awake itself
        /// disabled the component, and Restore before this point would write zeros (a render
        /// scale of 0 shipped once, measured as a blurry phone: Docs/PERFORMANCE.md 2f).
        /// </summary>
        bool _armed;

        /// <summary>Seconds the current variant has been live.</summary>
        float _elapsed;

        /// <summary>Index into Names of the variant currently applied; Names.Length once the sweep is over.</summary>
        int _variant;

        #endregion

        #region Private Methods

        /// <summary>Reads the baseline values and switches the sweep off where it must not run.</summary>
        void Awake()
        {
            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!_run || !Debug.isDebugBuild || _pipeline == null || _camera == null)
            {
                enabled = false;
                return;
            }

            _renderScale = _pipeline.renderScale;
            _armed = true;
        }

        /// <summary>Applies the baseline once the level has spawned (the cubes exist from the scope's Awake).</summary>
        void Start() => Apply(0);

        /// <summary>Advances to the next variant when the current one has had its time.</summary>
        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < VariantSeconds) return;

            _elapsed = 0f;
            _variant++;
            if (_variant < Names.Length)
            {
                Apply(_variant);
                return;
            }

            Restore();
            Debug.Log("[sweep] done, baseline restored");
            enabled = false;
        }

        /// <summary>Puts everything back exactly as it was read in Awake.</summary>
        void OnDisable() => Restore();

        /// <summary>Restores the baseline, then applies one variant on top of it and logs its name.</summary>
        void Apply(int variant)
        {
            Restore();
            switch (variant)
            {
                case 1: _pipeline.renderScale = 0.9f; break;
                case 2: _pipeline.renderScale = 0.8f; break;
                case 3: SetCubesReceive(false); break;
                case 4: SetCubeShader(_opaqueShader); break;
                case 5:
                    SetCubeShader(_opaqueShader);
                    SetCubesReceive(false);
                    break;
                case 6:
                    SetCubeShader(_opaqueShader);
                    SetCubesReceive(false);
                    _pipeline.renderScale = 0.8f;
                    break;
                case 7: SetObjectActive(LeanTouchObject, false); break;
                case 8: SetObjectActive(EventSystemObject, false); break;
            }

            Debug.Log("[sweep] " + Names[variant]);
        }

        /// <summary>Writes the baseline values back.</summary>
        void Restore()
        {
            if (!_armed) return;
            _pipeline.renderScale = _renderScale;
            SetCubesReceive(true);
            SetCubeShader(_cubeShader);
            SetObjectActive(LeanTouchObject, true);
            SetObjectActive(EventSystemObject, true);
        }

        /// <summary>Switches shadow receiving on the cube materials through the shader's keyword.</summary>
        void SetCubesReceive(bool receive)
        {
            CubeMaterials();
            foreach (Material material in _cubeMaterials)
            {
                if (receive) material.DisableKeyword(ReceiveShadowsOff);
                else material.EnableKeyword(ReceiveShadowsOff);
            }
        }

        /// <summary>Points every cube material at the given shader; null means the original.</summary>
        void SetCubeShader(Shader shader)
        {
            CubeMaterials();
            if (shader == null) return;
            foreach (Material material in _cubeMaterials) material.shader = shader;
        }

        /// <summary>Activates or deactivates a scene object by name; a missing object is ignored.</summary>
        static void SetObjectActive(string name, bool active)
        {
            GameObject target = GameObject.Find(name);
            if (target == null && active)
            {
                // Find skips inactive objects, so the object switched off is looked up through the probe's own scene.
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name == name) target = root;
                }
            }

            if (target != null) target.SetActive(active);
        }

        /// <summary>The board cubes' distinct materials and their original shader, collected once.</summary>
        void CubeMaterials()
        {
            if (_cubeRenderers != null) return;

            CubeView[] cubes = FindObjectsByType<CubeView>(FindObjectsSortMode.None);
            _cubeRenderers = new MeshRenderer[cubes.Length];
            for (int i = 0; i < cubes.Length; i++)
            {
                _cubeRenderers[i] = cubes[i].GetComponentInChildren<MeshRenderer>();
                Material material = _cubeRenderers[i].sharedMaterial;
                if (!_cubeMaterials.Contains(material)) _cubeMaterials.Add(material);
            }

            if (_cubeMaterials.Count > 0) _cubeShader = _cubeMaterials[0].shader;
        }

        #endregion
    }
}

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

        /// <summary>The variants, in order. Index 0 is the untouched baseline.</summary>
        static readonly string[] Names =
        {
            "baseline (soft high)",
            "soft medium",
            "soft low",
            "hard",
            "soft low + cubes receive off",
            "soft low + post off",
            "hard + post off + hdr off",
            "soft low + post off + hdr off",
        };

        /// <summary>Whether the sweep runs at all. Off by default; on for a measurement build.</summary>
        [Tooltip("Run the render-setting sweep on start. Leave off except for a measurement build.")]
        [SerializeField] bool _run;

        /// <summary>The camera whose post processing is toggled.</summary>
        [Tooltip("The camera whose post processing the sweep switches off and on.")]
        [SerializeField] Camera _camera;

        /// <summary>The pipeline asset in use, read once.</summary>
        UniversalRenderPipelineAsset _pipeline;

        /// <summary>The camera's URP data, read once.</summary>
        UniversalAdditionalCameraData _cameraData;

        /// <summary>The scene's main light, whose shadow type is toggled.</summary>
        Light _sun;

        /// <summary>The main light's URP data, whose soft shadow quality is toggled.</summary>
        UniversalAdditionalLightData _sunData;

        /// <summary>Every board cube's renderer, collected on the first variant that needs them.</summary>
        MeshRenderer[] _cubeRenderers;

        /// <summary>The distinct cube materials, for the receive-shadows keyword.</summary>
        readonly List<Material> _cubeMaterials = new List<Material>();

        /// <summary>The asset's own HDR flag, restored between variants.</summary>
        bool _hdr;

        /// <summary>The light's own shadow type, restored between variants.</summary>
        LightShadows _lightShadows;

        /// <summary>The light's own soft shadow quality, restored between variants.</summary>
        SoftShadowQuality _softQuality;

        /// <summary>The camera's own post-processing flag, restored between variants.</summary>
        bool _post;

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
            _cameraData = _camera != null ? _camera.GetUniversalAdditionalCameraData() : null;
            _sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            if (!_run || !Debug.isDebugBuild || _pipeline == null || _cameraData == null || _sun == null)
            {
                enabled = false;
                return;
            }

            _sunData = _sun.GetUniversalAdditionalLightData();
            _hdr = _pipeline.supportsHDR;
            _lightShadows = _sun.shadows;
            _softQuality = _sunData.softShadowQuality;
            _post = _cameraData.renderPostProcessing;
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
                case 1: _sunData.softShadowQuality = SoftShadowQuality.Medium; break;
                case 2: _sunData.softShadowQuality = SoftShadowQuality.Low; break;
                case 3: _sun.shadows = LightShadows.Hard; break;
                case 4:
                    _sunData.softShadowQuality = SoftShadowQuality.Low;
                    SetCubesReceive(false);
                    break;
                case 5:
                    _sunData.softShadowQuality = SoftShadowQuality.Low;
                    _cameraData.renderPostProcessing = false;
                    break;
                case 6:
                    _sun.shadows = LightShadows.Hard;
                    _cameraData.renderPostProcessing = false;
                    _pipeline.supportsHDR = false;
                    break;
                case 7:
                    _sunData.softShadowQuality = SoftShadowQuality.Low;
                    _cameraData.renderPostProcessing = false;
                    _pipeline.supportsHDR = false;
                    break;
            }

            Debug.Log("[sweep] " + Names[variant]);
        }

        /// <summary>Writes the baseline values back.</summary>
        void Restore()
        {
            if (_pipeline == null || _sunData == null) return;
            _pipeline.supportsHDR = _hdr;
            _sun.shadows = _lightShadows;
            _sunData.softShadowQuality = _softQuality;
            _cameraData.renderPostProcessing = _post;
            SetCubesReceive(true);
        }

        /// <summary>Switches shadow receiving on the cube materials through the shader's keyword.</summary>
        void SetCubesReceive(bool receive)
        {
            CubeRenderers();
            foreach (Material material in _cubeMaterials)
            {
                if (receive) material.DisableKeyword(ReceiveShadowsOff);
                else material.EnableKeyword(ReceiveShadowsOff);
            }
        }

        /// <summary>The board cubes' renderers and their distinct materials, collected once.</summary>
        MeshRenderer[] CubeRenderers()
        {
            if (_cubeRenderers != null) return _cubeRenderers;

            CubeView[] cubes = FindObjectsByType<CubeView>(FindObjectsSortMode.None);
            _cubeRenderers = new MeshRenderer[cubes.Length];
            for (int i = 0; i < cubes.Length; i++)
            {
                _cubeRenderers[i] = cubes[i].GetComponentInChildren<MeshRenderer>();
                Material material = _cubeRenderers[i].sharedMaterial;
                if (!_cubeMaterials.Contains(material)) _cubeMaterials.Add(material);
            }

            return _cubeRenderers;
        }

        #endregion
    }
}

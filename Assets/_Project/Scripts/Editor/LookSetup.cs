using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// The game's global look for outdoor gig scenes: gradient sky, warm late-afternoon sun,
    /// coloured ambient light, horizon-coloured fog and gentle post-processing.
    /// Call <see cref="ApplyToOpenScene"/> while building a scene.
    /// </summary>
    static class LookSetup
    {
        const string SettingsDir = SetupUtil.Root + "/Art/Settings";
        const string SkyMaterialPath = SetupUtil.MaterialsDir + "/M_Sky.mat";
        const string ProfilePath = SettingsDir + "/GigLook.asset";
        const string SkyShaderName = "Freelancer/GradientSky";

        // Late-afternoon sun from behind the street, lighting the front of buildings.
        static readonly Quaternion k_SunRotation = Quaternion.Euler(38f, 28f, 0f);

        public static void ApplyToOpenScene()
        {
            var sun = SetupSun();
            Vector3 toSun = -(k_SunRotation * Vector3.forward);

            RenderSettings.skybox = SkyMaterial(toSun);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Fog;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 140f;

            var volumeGo = new GameObject("PostProcessing");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Profile();

            var cam = Camera.main;
            if (cam != null) ConfigureCamera(cam);
        }

        /// <summary>Post-processing and smooth edges on a camera (player cameras use this too).</summary>
        public static void ConfigureCamera(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        static Light SetupSun()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();

            sun.name = "Sun";
            sun.type = LightType.Directional;
            sun.transform.rotation = k_SunRotation;
            sun.color = Palette.Sun;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            return sun;
        }

        static Material SkyMaterial(Vector3 toSun)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find(SkyShaderName));
                AssetDatabase.CreateAsset(mat, SkyMaterialPath);
            }
            mat.shader = Shader.Find(SkyShaderName);
            mat.SetColor("_TopColor", Palette.SkyTop);
            mat.SetColor("_HorizonColor", Palette.SkyHorizon);
            mat.SetColor("_BottomColor", Palette.SkyBelow);
            mat.SetColor("_SunColor", Palette.Sun);
            mat.SetVector("_SunDir", toSun.normalized);
            mat.SetFloat("_Exponent", 2.4f);
            mat.SetFloat("_SunSize", 0.006f);
            mat.SetFloat("_SunGlow", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile Profile()
        {
            SetupUtil.EnsureFolder(SettingsDir);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(14f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.6f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile); // components live inside the profile asset
            return c;
        }
    }
}

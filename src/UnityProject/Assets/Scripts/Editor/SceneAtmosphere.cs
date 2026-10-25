using System.Collections.Generic;
using System.IO;
using Pacifico.Effects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if PACIFICO_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Ambiente de cada capítulo: cielo real (HDRI CC0 de Poly Haven, sin edificios ni objetos modernos), luz ambiente y
    /// reflejos tomados de ese cielo, sombras suaves y un posproceso de cine (tonos ACES, color de época algo apagado y
    /// cálido, brillo suave y viñeta). Se aplica sobre las escenas ya construidas, sin tocar sus constructores: el sol
    /// de cada capítulo conserva su dirección y el cielo se gira para que su sol coincida con él.
    /// </summary>
    public static class SceneAtmosphere
    {
        public const string SkyRoot = ArtLibrary.Root;
        private const string ProfileFolder = "Assets/Settings/Ambiente";

        private sealed class Mood
        {
            public string Sky;
            /// <summary>Columna del sol en el HDRI (0–1, medida en su versión JPG); negativo si el cielo está cubierto.</summary>
            public float SunU = -1f;
            public float Exposure = 1f;
            public float Ambient = 0.8f;
            public float Temperature;
            public float Saturation;
            public float Contrast;
            public float PostExposure = -0.3f;
            public float Bloom = 0.25f;
            public float Vignette = 0.22f;
        }

        // Valores de ajuste: comprobados con las fotos de AtmospherePreview.
        private static readonly Dictionary<string, Mood> Moods = new Dictionary<string, Mood>
        {
            // «El cielo de Iquique está cubierto por una neblina pálida».
            ["Capitulo1_Rada_de_Iquique"] = new Mood { Sky = "overcast_soil_puresky", SunU = 0.4657f, Temperature = -4f, Saturation = -12f, Contrast = 10f },
            // Mar y cielo plomizos al amanecer.
            ["Capitulo2_Punta_Angamos"] = new Mood { Sky = "mud_road_puresky", Exposure = 0.9f, Temperature = -8f, Saturation = -22f, Contrast = 12f },
            ["Capitulo3_Bahia_de_Pisagua"] = new Mood { Sky = "kloofendal_48d_partly_cloudy_puresky", SunU = 0.6042f, Temperature = 6f, Saturation = -6f, Contrast = 8f },
            // Sol de la mañana sobre la pampa.
            ["Capitulo4_Quebrada_de_Tarapaca"] = new Mood { Sky = "kloofendal_43d_clear_puresky", SunU = 0.5925f, Temperature = 10f, Saturation = -8f, Contrast = 12f, Ambient = 0.7f, PostExposure = -0.75f },
            // Alba con camanchaca.
            ["Capitulo5_Alto_de_la_Alianza"] = new Mood { Sky = "kloppenheim_06_puresky", SunU = 0.6234f, Temperature = 12f, Saturation = -6f, Contrast = 10f, Ambient = 0.7f, PostExposure = -0.75f },
            // Alba rasante sobre el morro.
            ["Capitulo6_Morro_de_Arica"] = new Mood { Sky = "belfast_sunset_puresky", SunU = 0.6242f, Temperature = 14f, Saturation = -4f, Contrast = 10f, Ambient = 0.7f, PostExposure = -0.6f },
            // Noche con luna en los cañaverales.
            ["Capitulo7_Canaverales_de_Canete"] = new Mood { Sky = "kloppenheim_02_puresky", Exposure = 0.6f, Ambient = 1f, PostExposure = 0.4f, Temperature = -14f, Saturation = -25f, Contrast = 6f, Bloom = 0.35f, Vignette = 0.3f },
            // Tarde de verano en los olivares.
            ["Capitulo8_Reductos_de_Miraflores"] = new Mood { Sky = "rustig_koppie_puresky", SunU = 0.5972f, Temperature = 8f, Saturation = -6f, Contrast = 12f, Ambient = 0.7f, PostExposure = -0.75f },
        };

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.SceneAtmosphere.ApplyAllBatch</c>.</summary>
        public static void ApplyAllBatch()
        {
            try
            {
                RenderPipelineSetup.Ensure();
                ApplyAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Aplica el ambiente a todas las escenas de capítulo ya construidas.</summary>
        public static void ApplyAll()
        {
            foreach (KeyValuePair<string, Mood> entry in Moods)
            {
                string path = GameBuilder.ScenePath(entry.Key);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (Apply(entry.Key, entry.Value)) EditorSceneManager.SaveScene(scene);
            }
        }

        private static bool Apply(string sceneName, Mood mood)
        {
            string hdr = SkyRoot + "/" + mood.Sky + "/" + mood.Sky + "_2k.hdr";
            var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(hdr);
            Shader panoramic = Shader.Find("Skybox/Panoramic");
            if (sky == null || panoramic == null) return false;

            Light sun = MainLight();
            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
            string materialPath = ProjectPaths.Materials + "/Cielo_" + sceneName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(panoramic);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = panoramic;
            material.SetTexture("_MainTex", sky);
            material.SetFloat("_Mapping", 1f); // equirectangular
            material.SetFloat("_ImageType", 0f); // 360°
            material.SetFloat("_Exposure", mood.Exposure);
            material.SetFloat("_Rotation", SkyRotation(mood, sun));
            EditorUtility.SetDirty(material);

            RenderSettings.skybox = material;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = mood.Ambient;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            if (sun != null)
            {
                RenderSettings.sun = sun;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.9f;
            }
            DynamicGI.UpdateEnvironment();

            GameObject holder = GameObject.Find("Ambiente_Posproceso");
            if (holder == null) holder = new GameObject("Ambiente_Posproceso");
            if (holder.GetComponent<EnvironmentRefresh>() == null) holder.AddComponent<EnvironmentRefresh>();
#if PACIFICO_URP
            var volume = holder.GetComponent<Volume>();
            if (volume == null) volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = Profile(sceneName, mood);
            foreach (Camera camera in All<Camera>())
            {
                camera.allowHDR = true;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
#endif
            EditorSceneManager.MarkSceneDirty(holder.scene);
            return true;
        }

        private static T[] All<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(true);
#endif
        }

        /// <summary>La luz direccional más intensa de la escena (sol o luna).</summary>
        private static Light MainLight()
        {
            Light best = null;
            foreach (Light light in All<Light>())
            {
                if (light.type != LightType.Directional || !light.isActiveAndEnabled) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }
            return best;
        }

        /// <summary>
        /// Giro del cielo (grados) para que el sol del HDRI quede en la dirección de la luz del capítulo. El shader
        /// panorámico lee la columna u = 0,5 − atan2(z, x) / 2π de la dirección girada; el giro suma ese ángulo.
        /// </summary>
        private static float SkyRotation(Mood mood, Light sun)
        {
            if (mood.SunU < 0f || sun == null) return 0f;
            Vector3 toSun = -sun.transform.forward;
            float skyAngle = (0.5f - mood.SunU) * 2f * Mathf.PI;
            float worldAngle = Mathf.Atan2(toSun.z, toSun.x);
            return Mathf.Repeat((skyAngle - worldAngle) * Mathf.Rad2Deg, 360f);
        }

#if PACIFICO_URP
        private static VolumeProfile Profile(string sceneName, Mood mood)
        {
            HistoricalDataAssetGenerator.EnsureFolder(ProfileFolder);
            string path = ProfileFolder + "/Posproceso_" + sceneName + ".asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                T component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }
            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = Add<ColorAdjustments>();
            color.postExposure.Override(mood.PostExposure);
            color.contrast.Override(mood.Contrast);
            color.saturation.Override(mood.Saturation);
            WhiteBalance balance = Add<WhiteBalance>();
            balance.temperature.Override(mood.Temperature);
            Bloom bloom = Add<Bloom>();
            bloom.intensity.Override(mood.Bloom);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);
            Vignette vignette = Add<Vignette>();
            vignette.intensity.Override(mood.Vignette);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
#endif
    }
}

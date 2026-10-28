using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.Audio
{
    /// <summary>
    /// Fondo sonoro de cada capítulo: mar, gaviotas, viento del desierto, cañaverales, grillos y el fragor de la batalla
    /// a lo lejos. Son capas en bucle sin posición (suenan alrededor del jugador) que se mezclan con un volumen por
    /// capa. Al cargar una escena se funden las capas de la anterior y entran las nuevas. Escenas sin receta, en
    /// silencio. Las capas son grupos <c>Amb_*</c> de <c>Resources/Sonido</c>.
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        private const float FadeSeconds = 2.5f;

        /// <summary>Capas por escena: grupo y volumen (valores de ajuste).</summary>
        private static readonly Dictionary<string, (string group, float volume)[]> Recipes = new Dictionary<string, (string, float)[]>
        {
            { "Menu_Principal", new[] { ("Amb_Mar", 0.35f), ("Amb_Viento", 0.12f) } },
            // Despacho de Valparaíso: el puerto.
            { "Proto_Prologo_Ojo_de_Europa", new[] { ("Amb_Mar", 0.3f), ("Amb_Gaviotas", 0.2f), ("Amb_Puerto", 0.3f) } },
            { "Capitulo1_Rada_de_Iquique", new[] { ("Amb_Mar", 0.5f), ("Amb_Gaviotas", 0.22f), ("Amb_Puerto", 0.18f) } },
            { "Capitulo2_Punta_Angamos", new[] { ("Amb_Mar", 0.6f), ("Amb_Viento", 0.3f) } },
            { "Capitulo3_Bahia_de_Pisagua", new[] { ("Amb_Olas", 0.55f), ("Amb_Gaviotas", 0.15f), ("Amb_Batalla", 0.22f) } },
            { "Capitulo4_Quebrada_de_Tarapaca", new[] { ("Amb_Desierto", 0.5f), ("Amb_Batalla", 0.28f) } },
            { "Capitulo5_Alto_de_la_Alianza", new[] { ("Amb_Desierto", 0.5f), ("Amb_Batalla", 0.35f) } },
            { "Capitulo6_Morro_de_Arica", new[] { ("Amb_Olas", 0.4f), ("Amb_Viento", 0.3f), ("Amb_Batalla", 0.3f) } },
            { "Capitulo7_Canaverales_de_Canete", new[] { ("Amb_Canas", 0.5f), ("Amb_Grillos", 0.28f) } },
            { "Capitulo8_Reductos_de_Miraflores", new[] { ("Amb_Pasto", 0.42f), ("Amb_Batalla", 0.4f) } },
        };

        private readonly List<(AudioSource source, float volume)> _layers = new List<(AudioSource, float)>();
        private readonly List<AudioSource> _leaving = new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("Ambiente_Sonoro");
            DontDestroyOnLoad(go);
            var ambience = go.AddComponent<Ambience>();
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (ambience != null && mode == LoadSceneMode.Single) ambience.Play(scene.name);
            };
            ambience.Play(SceneManager.GetActiveScene().name);
        }

        private void Play(string scene)
        {
            foreach ((AudioSource source, float _) in _layers) _leaving.Add(source);
            _layers.Clear();
            if (!Recipes.TryGetValue(scene, out var recipe)) return;
            foreach ((string group, float volume) in recipe)
            {
                AudioClip clip = GameAudio.Clip(group);
                if (clip == null) continue;
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                source.time = Random.Range(0f, clip.length * 0.9f);
                source.Play();
                _layers.Add((source, volume));
            }
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            foreach ((AudioSource source, float volume) in _layers) source.volume = Mathf.MoveTowards(source.volume, volume, step * volume);
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                AudioSource source = _leaving[i];
                source.volume = Mathf.MoveTowards(source.volume, 0f, step);
                if (source.volume > 0f) continue;
                Destroy(source);
                _leaving.RemoveAt(i);
            }
        }
    }
}

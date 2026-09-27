using System;
using System.Collections.Generic;
using Pacifico.Core.Narrative;
using UnityEngine;

namespace Pacifico.Data
{
    /// <summary>
    /// Cinemática de corresponsal (ROADMAP 5.2) lista para el juego: el texto se lee del guion
    /// (<c>Historia_Completa_Guion.md</c>) al generar el asset, y las imágenes se importan del Archivo Histórico.
    /// La voz en off (en inglés) y la música son opcionales: sin voz, los subtítulos siguen el ritmo estimado.
    /// </summary>
    [CreateAssetMenu(fileName = "Cinematic_", menuName = "Pacífico/Datos/Cinemática", order = 3)]
    public sealed class CinematicDataSO : ScriptableObject
    {
        [Serializable]
        public sealed class ArchiveImage
        {
            [Tooltip("Ruta relativa a la raíz del repositorio (la clave que usa el guion de planos).")]
            public string path = string.Empty;
            public Texture2D texture;
        }

        public string title = string.Empty;
        public string location = string.Empty;
        public string narrator = string.Empty;
        [TextArea(1, 3)] public string musicCue = string.Empty;
        [TextArea(3, 12)] public List<string> paragraphs = new List<string>();
        public List<ArchiveImage> images = new List<ArchiveImage>();

        [Header("Audio (opcional)")]
        [Tooltip("Voz en off del corresponsal; si existe, los subtítulos se ajustan a su duración.")]
        public AudioClip narration;
        public AudioClip music;

        [Header("Procedencia")]
        public string sourceFile = string.Empty;

        public CinematicScript ToScript()
        {
            var script = new CinematicScript { Title = title, Location = location, Narrator = narrator, MusicCue = musicCue };
            script.Paragraphs.AddRange(paragraphs);
            return script;
        }

        public void CopyFrom(CinematicScript script, string source)
        {
            title = script.Title;
            location = script.Location;
            narrator = script.Narrator;
            musicCue = script.MusicCue;
            paragraphs = new List<string>(script.Paragraphs);
            sourceFile = source;
        }

        public Texture2D Image(string path)
        {
            foreach (ArchiveImage image in images)
            {
                if (image.path == path) return image.texture;
            }
            return null;
        }
    }
}

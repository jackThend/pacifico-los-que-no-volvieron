using System.Reflection;
using NUnit.Framework;
using Pacifico.Audio;
using UnityEngine;

namespace Pacifico.Tests.Unity
{
    /// <summary>Cada grupo de sonidos que usa el juego tiene clips en <c>Resources/Sonido</c> (se ejecuta en el Test Runner de Unity).</summary>
    public class SoundLibraryTests
    {
        private static readonly string[] Ambiences =
        {
            "Amb_Mar", "Amb_Olas", "Amb_Gaviotas", "Amb_Puerto", "Amb_Desierto", "Amb_Viento", "Amb_Pasto", "Amb_Canas",
            "Amb_Grillos", "Amb_Batalla",
        };

        [Test]
        public void CadaGrupo_TieneSonidos()
        {
            foreach (FieldInfo field in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string group = (string)field.GetValue(null);
                AudioClip[] clips = Resources.LoadAll<AudioClip>("Sonido/" + group);
                Assert.That(clips, Is.Not.Empty, group);
                foreach (AudioClip clip in clips) Assert.That(clip.length, Is.GreaterThan(0.05f), clip.name);
            }
        }

        [Test]
        public void CadaAmbiente_EsUnBucleLargo()
        {
            foreach (string group in Ambiences)
            {
                AudioClip[] clips = Resources.LoadAll<AudioClip>("Sonido/" + group);
                Assert.That(clips, Is.Not.Empty, group);
                foreach (AudioClip clip in clips) Assert.That(clip.length, Is.GreaterThan(20f), clip.name);
            }
        }
    }
}

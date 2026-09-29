using System;
using System.Collections.Generic;
using System.Linq;

namespace Pacifico.Core.Campaign
{
    public enum ChapterMode
    {
        Cinematic,
        Naval,
        Fps,
        Rts,
    }

    /// <summary>Un capítulo de la campaña, con la escena que lo juega (si ya existe).</summary>
    public sealed class ChapterEntry
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>Número del GDD (0 = prólogo).</summary>
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public ChapterMode Mode { get; set; }
        /// <summary>Nombre de la escena (sin ruta ni extensión), o null si el capítulo aún no se ha desarrollado.</summary>
        public string Scene { get; set; }
        public bool Implemented => !string.IsNullOrEmpty(Scene);
    }

    /// <summary>
    /// La campaña en el orden del GDD (§5): el prólogo y los ocho capítulos, con la escena de cada uno. Los capítulos 2
    /// (Angamos), 3 (Pisagua, RTS) y 7 (Cañete) no están en el roadmap de esta versión: figuran en el menú como «en
    /// desarrollo» y la campaña salta de uno implementado al siguiente. El orden de las escenas de la build sale de aquí.
    /// </summary>
    public static class CampaignCatalog
    {
        public const string MenuScene = "Menu_Principal";
        public const string PrologueId = "prologo";

        public static IReadOnlyList<ChapterEntry> Chapters { get; } = new[]
        {
            new ChapterEntry { Id = PrologueId, Number = 0, Title = "El Ojo de Europa", Date = "Valparaíso, 1879", Mode = ChapterMode.Cinematic, Scene = "Proto_Prologo_Ojo_de_Europa" },
            new ChapterEntry { Id = IquiqueChapter.Id, Number = 1, Title = "Madera y blindaje", Date = "21 de mayo de 1879", Mode = ChapterMode.Naval, Scene = "Capitulo1_Rada_de_Iquique" },
            new ChapterEntry { Id = "cap2_angamos", Number = 2, Title = "Caza en alta mar", Date = "8 de octubre de 1879", Mode = ChapterMode.Naval },
            new ChapterEntry { Id = "cap3_pisagua", Number = 3, Title = "Sangre en el salitre", Date = "2 de noviembre de 1879", Mode = ChapterMode.Rts },
            new ChapterEntry { Id = TarapacaChapter.Id, Number = 4, Title = "Sed en la quebrada", Date = "27 de noviembre de 1879", Mode = ChapterMode.Fps, Scene = "Capitulo4_Quebrada_de_Tarapaca" },
            new ChapterEntry { Id = AltoDeLaAlianzaChapter.Id, Number = 5, Title = "El trueno de Intiorko", Date = "26 de mayo de 1880", Mode = ChapterMode.Rts, Scene = "Capitulo5_Alto_de_la_Alianza" },
            new ChapterEntry { Id = AricaChapter.Id, Number = 6, Title = "Hasta el último cartucho", Date = "7 de junio de 1880", Mode = ChapterMode.Fps, Scene = "Capitulo6_Morro_de_Arica" },
            new ChapterEntry { Id = "cap7_canete", Number = 7, Title = "Cadenas rotas", Date = "Diciembre de 1880", Mode = ChapterMode.Fps },
            new ChapterEntry { Id = MirafloresChapter.Id, Number = 8, Title = "Los que no volvieron", Date = "15 de enero de 1881", Mode = ChapterMode.Fps, Scene = "Capitulo8_Reductos_de_Miraflores" },
        };

        public static ChapterEntry Get(string id) => Chapters.FirstOrDefault(c => c.Id == id);

        public static IEnumerable<ChapterEntry> Playable => Chapters.Where(c => c.Implemented);

        /// <summary>El siguiente capítulo jugable después de <paramref name="id"/>, o null al final de la campaña.</summary>
        public static ChapterEntry Next(string id)
        {
            int index = Chapters.ToList().FindIndex(c => c.Id == id);
            if (index < 0) throw new ArgumentException("Capítulo desconocido: " + id, nameof(id));
            for (int i = index + 1; i < Chapters.Count; i++)
            {
                if (Chapters[i].Implemented) return Chapters[i];
            }
            return null;
        }

        /// <summary>El jugable anterior (el que hay que completar para abrir este), o null para el primero.</summary>
        public static ChapterEntry Previous(string id)
        {
            int index = Chapters.ToList().FindIndex(c => c.Id == id);
            if (index < 0) throw new ArgumentException("Capítulo desconocido: " + id, nameof(id));
            for (int i = index - 1; i >= 0; i--)
            {
                if (Chapters[i].Implemented) return Chapters[i];
            }
            return null;
        }

        /// <summary>Un capítulo se abre al completar el jugable anterior; el prólogo está siempre abierto.</summary>
        public static bool IsUnlocked(ChapterEntry chapter, CampaignProgress progress)
        {
            if (chapter == null || !chapter.Implemented) return false;
            ChapterEntry previous = Previous(chapter.Id);
            return previous == null || (progress != null && progress.IsCompleted(previous.Id));
        }

        /// <summary>El capítulo con el que «Continuar»: el primero abierto y sin completar (o el último, si todo está hecho).</summary>
        public static ChapterEntry Continue(CampaignProgress progress)
        {
            ChapterEntry last = null;
            foreach (ChapterEntry c in Playable)
            {
                last = c;
                if (IsUnlocked(c, progress) && (progress == null || !progress.IsCompleted(c.Id))) return c;
            }
            return last;
        }

        /// <summary>Escenas de la build, en orden: el menú y los capítulos jugables.</summary>
        public static IEnumerable<string> BuildScenes() => new[] { MenuScene }.Concat(Playable.Select(c => c.Scene));
    }
}

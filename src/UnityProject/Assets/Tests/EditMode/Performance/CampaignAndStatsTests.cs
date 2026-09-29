using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;

namespace Pacifico.Tests.Performance
{
    /// <summary>ROADMAP 6.6 — la campaña enlazada (orden, desbloqueo, escenas de la build) y la estadística de fotogramas.</summary>
    public class CampaignAndStatsTests
    {
        [Test]
        public void Campana_EnElOrdenDelGDD_YConLasMisionesQueExisten()
        {
            Assert.That(CampaignCatalog.Chapters.Select(c => c.Number), Is.EqualTo(Enumerable.Range(0, 9)));
            Assert.That(CampaignCatalog.Playable.Select(c => c.Number), Is.EqualTo(new[] { 0, 1, 4, 5, 6, 8 }));
            // Los ids de los capítulos jugables son los de sus guiones de misión (así se anota el progreso).
            Assert.That(CampaignCatalog.Get(IquiqueChapter.Id).Scene, Is.Not.Null);
            Assert.That(CampaignCatalog.Get(MirafloresChapter.Id).Title, Is.EqualTo("Los que no volvieron"));
            Assert.That(CampaignCatalog.BuildScenes().First(), Is.EqualTo(CampaignCatalog.MenuScene));
            Assert.That(CampaignCatalog.BuildScenes().Count(), Is.EqualTo(7));
        }

        [Test]
        public void Campana_SeAbreCapituloACapitulo()
        {
            var progress = new CampaignProgress();
            ChapterEntry iquique = CampaignCatalog.Get(IquiqueChapter.Id);
            ChapterEntry tarapaca = CampaignCatalog.Get(TarapacaChapter.Id);
            Assert.That(CampaignCatalog.IsUnlocked(CampaignCatalog.Get(CampaignCatalog.PrologueId), progress), Is.True);
            Assert.That(CampaignCatalog.IsUnlocked(iquique, progress), Is.False);
            Assert.That(CampaignCatalog.Continue(progress).Id, Is.EqualTo(CampaignCatalog.PrologueId));

            progress.CompleteChapter(CampaignCatalog.PrologueId);
            Assert.That(CampaignCatalog.IsUnlocked(iquique, progress), Is.True);
            progress.CompleteChapter(IquiqueChapter.Id);
            // Angamos y Pisagua aún no existen: tras Iquique se abre Tarapacá.
            Assert.That(CampaignCatalog.Next(IquiqueChapter.Id), Is.SameAs(tarapaca));
            Assert.That(CampaignCatalog.IsUnlocked(tarapaca, progress), Is.True);
            Assert.That(CampaignCatalog.IsUnlocked(CampaignCatalog.Get("cap2_angamos"), progress), Is.False, "en desarrollo");
            Assert.That(CampaignCatalog.Continue(progress), Is.SameAs(tarapaca));
            Assert.That(CampaignCatalog.Next(MirafloresChapter.Id), Is.Null, "Miraflores cierra la campaña");
        }

        [Test]
        public void Campana_LasEscenasSonLasQueConstruyenLosEditores()
        {
            string editor = Path.Combine(TestPaths.RepositoryRoot, "src", "UnityProject", "Assets", "Scripts", "Editor");
            string sources = string.Join("\n", Directory.GetFiles(editor, "*.cs").Select(File.ReadAllText));
            foreach (string scene in CampaignCatalog.BuildScenes())
            {
                bool built = sources.Contains("/" + scene + ".unity\"") ||
                             (scene == CampaignCatalog.MenuScene && sources.Contains("CampaignCatalog.MenuScene + \".unity\""));
                Assert.That(built, Is.True, "ningún constructor crea la escena " + scene);
            }
        }

        /// <summary>
        /// Integración de la campaña entera: cada capítulo jugable tiene su guion de misión (leído del guion real,
        /// válido y con el mismo id que el catálogo) y su recompensa existe en el catálogo de coleccionables; el epílogo
        /// también se lee sin errores.
        /// </summary>
        [Test]
        public void Campana_CadaCapituloTieneSuMisionYSuRecompensa()
        {
            System.Func<string, string> read = Campaign.MissionTests.Read;
            MissionScript[] missions =
            {
                IquiqueChapter.Build(read), TarapacaChapter.Build(read), AltoDeLaAlianzaChapter.Build(read),
                AricaChapter.Build(read), MirafloresChapter.Build(read),
            };
            var ids = CampaignCatalog.Playable.Where(c => c.Id != CampaignCatalog.PrologueId).Select(c => c.Id).ToList();
            Assert.That(missions.Select(m => m.Id), Is.EquivalentTo(ids));
            foreach (MissionScript mission in missions)
            {
                Assert.That(mission.Validate(), Is.Empty, mission.Id);
                if (mission.RewardCollectibleId == null) continue; // Tarapacá: el Archivo no tiene su documento
                Assert.That(Pacifico.Core.Narrative.CollectibleCatalog.Definitions.Any(d => d.Id == mission.RewardCollectibleId), Is.True, mission.Id);
            }
            Assert.That(MemoriaRotaEpilogue.Load(read).Letter, Is.Not.Empty);
        }

        [Test]
        public void Estadistica_PercentilesTironesYVeredicto()
        {
            var stats = new FrameStats(1000);
            for (int i = 0; i < 990; i++) stats.Add(1f / 60f);
            for (int i = 0; i < 10; i++) stats.Add(0.05f); // diez tirones de 50 ms
            Assert.That(stats.MeanMs, Is.EqualTo((990 * 16.667f + 10 * 50f) / 1000f).Within(0.01f));
            Assert.That(stats.PercentileMs(50f), Is.EqualTo(16.667f).Within(0.01f));
            Assert.That(stats.PercentileMs(99.5f), Is.EqualTo(50f).Within(0.01f));
            Assert.That(stats.Hitches, Is.EqualTo(10));
            Assert.That(stats.MeetsTarget, Is.False, "un 1 % de tirones no cumple");
            stats.Clear();
            for (int i = 0; i < 1000; i++) stats.Add(i % 200 == 0 ? 0.04f : 0.012f);
            Assert.That(stats.MeetsTarget, Is.True);
            StringAssert.StartsWith("Capitulo5;1000;", stats.CsvRow("Capitulo5", 0));
            Assert.That(stats.CsvRow("x", 2).Split(';').Length, Is.EqualTo(FrameStats.CsvHeader.Split(';').Length));
        }

        [Test]
        public void Estadistica_BufferCircularYSinBasura()
        {
            var stats = new FrameStats(100);
            for (int i = 0; i < 250; i++) stats.Add(i < 150 ? 0.1f : 0.01f);
            Assert.That(stats.Count, Is.EqualTo(100));
            Assert.That(stats.MaxMs, Is.EqualTo(10f).Within(1e-3f), "solo quedan los 100 últimos");
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                stats.Add(0.016f);
                _ = stats.MeanMs + stats.Hitches;
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}

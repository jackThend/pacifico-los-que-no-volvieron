using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Tactics
{
    /// <summary>ROADMAP 4.1 — formaciones, asignación de puestos, órdenes a varias escuadras y selección.</summary>
    public class FormationAndOrdersTests
    {
        // ------------------------------------------------------------------------------------------
        // Formaciones
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Linea_DeOchoEnUnaFila_DeDoceEnDos()
        {
            Vec3[] eight = Formation.LocalSlots(FormationType.Line, 8);
            Assert.That(eight.All(s => s.Z == 0f), Is.True);
            Assert.That(eight.Select(s => s.X), Is.EqualTo(new[] { -3.5f, -2.5f, -1.5f, -0.5f, 0.5f, 1.5f, 2.5f, 3.5f }));
            Assert.That(Formation.Frontage(FormationType.Line, 8), Is.EqualTo(7f));

            Vec3[] twelve = Formation.LocalSlots(FormationType.Line, 12);
            Assert.That(twelve.Count(s => s.Z == 0f), Is.EqualTo(6));
            Assert.That(twelve.Count(s => Math.Abs(s.Z + Formation.LineRankDepthM) < 1e-5f), Is.EqualTo(6));
            Assert.That(Formation.Frontage(FormationType.Line, 12), Is.EqualTo(5f));

            // Nueve hombres: 5 delante y 4 centrados detrás.
            Vec3[] nine = Formation.LocalSlots(FormationType.Line, 9);
            Assert.That(nine.Where(s => s.Z < 0f).Select(s => s.X), Is.EqualTo(new[] { -1.5f, -0.5f, 0.5f, 1.5f }));
        }

        [Test]
        public void Guerrilla_OrdenAbiertoYEscalonado()
        {
            Vec3[] slots = Formation.LocalSlots(FormationType.Skirmish, 10);
            for (int i = 1; i < slots.Length; i++) Assert.That(slots[i].X - slots[i - 1].X, Is.EqualTo(Formation.SkirmishSpacingM).Within(1e-5f));
            Assert.That(slots.Count(s => s.Z < 0f), Is.EqualTo(5), "uno de cada dos, retrasado");
            Assert.That(slots.Sum(s => s.X), Is.EqualTo(0f).Within(1e-4f), "centrada en el ancla");
            Assert.That(Formation.Frontage(FormationType.Skirmish, 10), Is.EqualTo(45f));
        }

        [Test]
        public void Orientacion_DerechaYConversionAlMundo()
        {
            var east = new Vec3(1f, 0f, 0f);
            Vec3 right = Formation.RightOf(east);
            Assert.That(right.Z, Is.EqualTo(-1f).Within(1e-6f), "mirando al este, la derecha es el sur");
            Vec3 w = Formation.ToWorld(new Vec3(10f, 2f, 10f), east, new Vec3(1f, 0f, 2f));
            Assert.That(w.X, Is.EqualTo(12f).Within(1e-5f));
            Assert.That(w.Z, Is.EqualTo(9f).Within(1e-5f));
            Assert.That(w.Y, Is.EqualTo(2f));
            Assert.That(Formation.HeadingDeg(east), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(Formation.HeadingDeg(Formation.FromHeadingDeg(-135f)), Is.EqualTo(-135f).Within(1e-3f));
        }

        // ------------------------------------------------------------------------------------------
        // Asignación de puestos
        // ------------------------------------------------------------------------------------------

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Hungaro_EsOptimo(int seed)
        {
            var rng = new Random(seed);
            var from = Enumerable.Range(0, 6).Select(_ => new Vec3((float)rng.NextDouble() * 20f, 0f, (float)rng.NextDouble() * 20f)).ToList();
            var to = Enumerable.Range(0, 6).Select(_ => new Vec3((float)rng.NextDouble() * 20f, 0f, (float)rng.NextDouble() * 20f)).ToList();
            int[] assignment = SlotAssignment.Solve(from, to);
            Assert.That(assignment.Distinct().Count(), Is.EqualTo(6));
            float best = Permutations(6).Min(p => SlotAssignment.TotalDistance(from, to, p));
            Assert.That(SlotAssignment.TotalDistance(from, to, assignment), Is.EqualTo(best).Within(1e-3f));
        }

        [Test]
        public void Hungaro_ConMasPuestosQueSoldados()
        {
            var from = new List<Vec3> { new Vec3(0f, 0f, 0f), new Vec3(10f, 0f, 0f) };
            var to = new List<Vec3> { new Vec3(100f, 0f, 0f), new Vec3(9f, 0f, 0f), new Vec3(1f, 0f, 0f) };
            Assert.That(SlotAssignment.Solve(from, to), Is.EqualTo(new[] { 2, 1 }));
            Assert.Throws<ArgumentException>(() => SlotAssignment.Solve(to, from));
        }

        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        public void LasTrayectoriasNoSeCruzan(int seed)
        {
            var rng = new Random(seed);
            var soldiers = Enumerable.Range(0, 12).Select(_ => new Vec3((float)rng.NextDouble() * 30f, 0f, (float)rng.NextDouble() * 30f)).ToList();
            Vec3[] slots = Formation.LocalSlots(FormationType.Line, 12).Select(s => Formation.ToWorld(new Vec3(15f, 0f, 40f), new Vec3(0f, 0f, 1f), s)).ToArray();
            int[] a = SlotAssignment.Solve(soldiers, slots);
            for (int i = 0; i < 12; i++)
            {
                for (int j = i + 1; j < 12; j++)
                {
                    Assert.That(SegmentsCross(soldiers[i], slots[a[i]], soldiers[j], slots[a[j]]), Is.False, i + " y " + j);
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Órdenes a varias escuadras
        // ------------------------------------------------------------------------------------------

        private static List<SquadFootprint> ThreeSquads()
        {
            var north = new Vec3(0f, 0f, 1f);
            return new List<SquadFootprint>
            {
                new SquadFootprint(new Vec3(-30f, 0f, 0f), north, 12, FormationType.Line),
                new SquadFootprint(new Vec3(0f, 0f, 0f), north, 10, FormationType.Skirmish),
                new SquadFootprint(new Vec3(30f, 0f, 0f), north, 8, FormationType.Line),
            };
        }

        [Test]
        public void Clic_LasEscuadrasFormanCodoConCodoMirandoHaciaDondeAvanzan()
        {
            List<SquadFootprint> squads = ThreeSquads();
            SquadDestination[] plan = OrderPlanner.PlanMove(squads, new Vec3(0f, 0f, 100f), new Vec3(0f, 0f, 100f));
            foreach (SquadDestination d in plan) Assert.That(d.Facing.Z, Is.EqualTo(1f).Within(1e-4f));
            // Mantienen su orden de izquierda a derecha y sus frentes no se solapan.
            Assert.That(plan[0].Anchor.X, Is.LessThan(plan[1].Anchor.X));
            Assert.That(plan[1].Anchor.X, Is.LessThan(plan[2].Anchor.X));
            AssertNoOverlap(squads, plan);
            float left = plan[0].Anchor.X - squads[0].Frontage / 2f, right = plan[2].Anchor.X + squads[2].Frontage / 2f;
            Assert.That((left + right) / 2f, Is.EqualTo(0f).Within(1e-3f), "centradas en el punto");
        }

        [Test]
        public void Arrastre_MarcaElFrenteYLaOrientacion()
        {
            List<SquadFootprint> squads = ThreeSquads();
            // Trazo de oeste a este a 100 m: miran al norte (hacia delante).
            SquadDestination[] plan = OrderPlanner.PlanMove(squads, new Vec3(-80f, 0f, 100f), new Vec3(80f, 0f, 100f));
            foreach (SquadDestination d in plan)
            {
                Assert.That(d.Facing.Z, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(d.Anchor.Z, Is.EqualTo(100f).Within(1e-3f));
            }
            Assert.That(plan[0].Anchor.X - squads[0].Frontage / 2f, Is.EqualTo(-80f).Within(1e-3f), "ocupan el trazo entero");
            Assert.That(plan[2].Anchor.X + squads[2].Frontage / 2f, Is.EqualTo(80f).Within(1e-3f));

            // Al revés (de este a oeste): miran al sur, y la escuadra de la izquierda (oeste) sigue al oeste.
            SquadDestination[] reverse = OrderPlanner.PlanMove(squads, new Vec3(80f, 0f, 100f), new Vec3(-80f, 0f, 100f));
            foreach (SquadDestination d in reverse) Assert.That(d.Facing.Z, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(reverse[0].Anchor.X, Is.LessThan(reverse[2].Anchor.X), "sin cruzarse");
            AssertNoOverlap(squads, reverse);
        }

        [Test]
        public void Arrastre_Corto_SeAlargaPorIgual()
        {
            List<SquadFootprint> squads = ThreeSquads();
            SquadDestination[] plan = OrderPlanner.PlanMove(squads, new Vec3(-5f, 0f, 50f), new Vec3(5f, 0f, 50f));
            AssertNoOverlap(squads, plan);
            float left = plan[0].Anchor.X - squads[0].Frontage / 2f, right = plan[2].Anchor.X + squads[2].Frontage / 2f;
            Assert.That((left + right) / 2f, Is.EqualTo(0f).Within(1e-3f));
        }

        private static void AssertNoOverlap(List<SquadFootprint> squads, SquadDestination[] plan)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                for (int j = i + 1; j < squads.Count; j++)
                {
                    Vec3 right = Formation.RightOf(plan[i].Facing);
                    float gap = Math.Abs(Vec3.Dot(plan[i].Anchor - plan[j].Anchor, right)) - squads[i].Frontage / 2f - squads[j].Frontage / 2f;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(Formation.IntervalM(FormationType.Line) - 1e-3f), i + "–" + j);
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Selección
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Recuadro_SeleccionaLaEscuadraEntera()
        {
            var soldiers = new List<ScreenPoint>
            {
                new ScreenPoint(1, 100f, 100f), new ScreenPoint(1, 300f, 100f),
                new ScreenPoint(2, 500f, 500f),
                new ScreenPoint(3, 150f, 120f, inFront: false),
            };
            // Arrastre de abajo a la derecha hacia arriba a la izquierda: el recuadro se normaliza.
            ScreenRect rect = ScreenRect.FromCorners(200f, 200f, 50f, 50f);
            Assert.That(rect.XMin, Is.EqualTo(50f));
            Assert.That(SelectionLogic.Pick(rect, soldiers), Is.EqualTo(new[] { 1 }), "basta un hombre dentro; lo que está detrás de la cámara no cuenta");
        }

        [Test]
        public void Clic_SeleccionaLaEscuadraMasCercana()
        {
            var soldiers = new List<ScreenPoint> { new ScreenPoint(1, 100f, 100f), new ScreenPoint(2, 112f, 100f) };
            Assert.That(SelectionLogic.Pick(ScreenRect.FromCorners(110f, 100f, 111f, 101f), soldiers), Is.EqualTo(new[] { 2 }));
            Assert.That(SelectionLogic.Pick(ScreenRect.FromCorners(400f, 400f, 401f, 401f), soldiers), Is.Empty, "clic en el vacío");
        }

        [Test]
        public void Modos_SustituirAnadirAlternar()
        {
            var selection = new HashSet<int> { 1, 2 };
            SelectionLogic.Apply(selection, new[] { 3 }, SelectionMode.Add);
            Assert.That(selection, Is.EquivalentTo(new[] { 1, 2, 3 }));
            SelectionLogic.Apply(selection, new[] { 2, 4 }, SelectionMode.Toggle);
            Assert.That(selection, Is.EquivalentTo(new[] { 1, 3, 4 }));
            SelectionLogic.Apply(selection, new[] { 5 }, SelectionMode.Replace);
            Assert.That(selection, Is.EquivalentTo(new[] { 5 }));
            SelectionLogic.Apply(selection, Array.Empty<int>(), SelectionMode.Replace);
            Assert.That(selection, Is.Empty, "clic en el vacío deselecciona");
        }

        // ------------------------------------------------------------------------------------------

        internal static bool SegmentsCross(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
        {
            float Cross(Vec3 o, Vec3 p, Vec3 q) => (p.X - o.X) * (q.Z - o.Z) - (p.Z - o.Z) * (q.X - o.X);
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 1e-4f && d2 < -1e-4f) || (d1 < -1e-4f && d2 > 1e-4f)) && ((d3 > 1e-4f && d4 < -1e-4f) || (d3 < -1e-4f && d4 > 1e-4f));
        }

        private static IEnumerable<int[]> Permutations(int n)
        {
            var items = Enumerable.Range(0, n).ToArray();
            return Permute(items, 0);
        }

        private static IEnumerable<int[]> Permute(int[] items, int k)
        {
            if (k == items.Length)
            {
                yield return (int[])items.Clone();
                yield break;
            }
            for (int i = k; i < items.Length; i++)
            {
                (items[k], items[i]) = (items[i], items[k]);
                foreach (int[] p in Permute(items, k + 1)) yield return p;
                (items[k], items[i]) = (items[i], items[k]);
            }
        }
    }

    /// <summary>
    /// Verificación de ROADMAP 4.1: mando coordinado de escuadras de 8 a 12 hombres en línea y en guerrilla. Los
    /// soldados se simulan como puntos que corren hacia su puesto a la velocidad que les pide la escuadra (lo que hace
    /// el NavMeshAgent en Unity).
    /// </summary>
    public class SquadMarchTests
    {
        private sealed class SimSquad
        {
            public SquadCommand Command;
            public List<Vec3> Soldiers;
            public float MaxLagWhileMarching;
            public Vec3 Final = new Vec3(0f, 0f, 1f);
            public bool Formed;

            public float FinalHeading() => Formation.HeadingDeg(Final);

            public void Step(float dt)
            {
                Command.Step(dt, Soldiers);
                for (int i = 0; i < Soldiers.Count; i++)
                {
                    Vec3 slot = Command.SlotOf(i);
                    Vec3 to = new Vec3(slot.X - Soldiers[i].X, 0f, slot.Z - Soldiers[i].Z);
                    float d = to.Magnitude;
                    float step = Command.FollowSpeed(d) * dt;
                    Soldiers[i] = d <= step ? new Vec3(slot.X, 0f, slot.Z) : Soldiers[i] + to / d * step;
                }
                float lag = Command.MaxLag(Soldiers);
                if (lag < 1.5f) Formed = true;
                // Cohesión en el primer tramo recto, una vez formados (al principio están desbandados).
                Vec3 a = Command.March.Anchor;
                if (Formed && Command.March.Moving && a.Z < 50f && a.X < 1f) MaxLagWhileMarching = Math.Max(MaxLagWhileMarching, lag);
            }
        }

        private static SimSquad Create(FormationType type, int count, Vec3 anchor, int seed, float scatter = 6f)
        {
            var rng = new Random(seed);
            // Desbandados alrededor del ancla (como tras desembarcar o después de un asalto).
            var soldiers = Enumerable.Range(0, count)
                .Select(_ => anchor + new Vec3((float)(rng.NextDouble() - 0.5) * scatter * 2f, 0f, (float)(rng.NextDouble() - 0.5) * scatter * 2f))
                .ToList();
            return new SimSquad { Command = new SquadCommand(type, soldiers, anchor, new Vec3(0f, 0f, 1f)), Soldiers = soldiers };
        }

        private static float Run(IList<SimSquad> squads, float dt, float maxSeconds)
        {
            float t = 0f;
            while (t < maxSeconds)
            {
                foreach (SimSquad s in squads) s.Step(dt);
                t += dt;
                bool done = squads.All(s => !s.Command.March.Moving && s.Command.MaxLag(s.Soldiers) < 0.05f &&
                                            Math.Abs(MathUtil.DeltaAngle(Formation.HeadingDeg(s.Command.March.Facing), s.FinalHeading())) < 0.5f);
                if (done) break;
            }
            return t;
        }

        [TestCase(FormationType.Line, 8)]
        [TestCase(FormationType.Line, 12)]
        [TestCase(FormationType.Skirmish, 8)]
        [TestCase(FormationType.Skirmish, 12)]
        public void Escuadra_MarchaPorUnCaminoConEsquinaYFormaEnSuDestino(FormationType type, int count)
        {
            SimSquad squad = Create(type, count, Vec3.Zero, count);
            // 60 m al norte y 40 m al este (esquina de 90°), acabando de cara al norte.
            var path = new List<Vec3> { new Vec3(0f, 0f, 60f), new Vec3(40f, 0f, 60f) };
            squad.Final = new Vec3(0f, 0f, 1f);
            squad.Command.MoveTo(path, squad.Final, squad.Soldiers);
            float t = Run(new[] { squad }, 1f / 30f, 300f);

            SquadCommand c = squad.Command;
            Assert.That((c.March.Anchor - new Vec3(40f, 0f, 60f)).Magnitude, Is.LessThan(0.01f), "llega");
            Assert.That(c.March.Facing.Z, Is.EqualTo(1f).Within(1e-3f), "orientada como se ordenó");
            Assert.That(c.MaxLag(squad.Soldiers), Is.LessThan(0.05f), "cada hombre en su puesto");
            Assert.That(t, Is.LessThan(100f / SquadMarch.MarchSpeedMps * 1.6f), "sin atascarse: " + t.ToString("0.0") + " s");

            // La formación final es la ordenada: mismas distancias entre vecinos.
            Vec3[] expected = Formation.LocalSlots(type, count).Select(s => Formation.ToWorld(c.March.Anchor, c.March.Facing, s)).ToArray();
            foreach (Vec3 e in expected) Assert.That(squad.Soldiers.Min(p => (p - e).Magnitude), Is.LessThan(0.05f));

            // En marcha se mantiene la cohesión.
            Assert.That(squad.Formed, Is.True);
            Assert.That(squad.MaxLagWhileMarching, Is.LessThan(1.5f), "rezagado máximo en marcha, ya formados");
        }

        [Test]
        public void TresEscuadras_DesplieganAlaVezSinMezclarse()
        {
            var squads = new List<SimSquad>
            {
                Create(FormationType.Line, 12, new Vec3(-30f, 0f, 0f), 1),
                Create(FormationType.Skirmish, 10, new Vec3(0f, 0f, 0f), 2),
                Create(FormationType.Line, 8, new Vec3(30f, 0f, 0f), 3),
            };
            var footprints = squads.Select(s => new SquadFootprint(s.Command.March.Anchor, s.Command.March.Facing, s.Soldiers.Count, s.Command.Formation)).ToList();
            SquadDestination[] plan = OrderPlanner.PlanMove(footprints, new Vec3(-60f, 0f, 120f), new Vec3(60f, 0f, 120f));
            for (int i = 0; i < squads.Count; i++)
            {
                squads[i].Final = plan[i].Facing;
                squads[i].Command.MoveTo(new List<Vec3> { plan[i].Anchor }, plan[i].Facing, squads[i].Soldiers);
            }
            Run(squads, 1f / 30f, 400f);

            for (int i = 0; i < squads.Count; i++)
            {
                Assert.That((squads[i].Command.March.Anchor - plan[i].Anchor).Magnitude, Is.LessThan(0.01f));
                Assert.That(squads[i].Command.MaxLag(squads[i].Soldiers), Is.LessThan(0.05f));
            }
            // Entre escuadras vecinas queda al menos el intervalo.
            for (int i = 0; i < squads.Count; i++)
            {
                for (int j = i + 1; j < squads.Count; j++)
                {
                    float closest = squads[i].Soldiers.Min(a => squads[j].Soldiers.Min(b => (a - b).Magnitude));
                    Assert.That(closest, Is.GreaterThanOrEqualTo(Formation.IntervalM(FormationType.Line) - 0.05f));
                }
            }
        }

        [Test]
        public void MediaVuelta_ReasignaPuestosSinCruzarse()
        {
            SimSquad squad = Create(FormationType.Line, 12, Vec3.Zero, 5, scatter: 0f);
            squad.Final = new Vec3(0f, 0f, 1f);
            Run(new[] { squad }, 1f / 30f, 60f); // forma de cara al norte
            List<Vec3> before = squad.Soldiers.ToList();

            // Orden de ir 30 m al sur y mirar al sur: media vuelta en el sitio, sin rueda de 180°.
            squad.Final = new Vec3(0f, 0f, -1f);
            squad.Command.MoveTo(new List<Vec3> { new Vec3(0f, 0f, -30f) }, squad.Final, squad.Soldiers);
            Assert.That(squad.Command.March.Facing.Z, Is.EqualTo(-1f).Within(1e-4f), "ya mira al sur");
            // Los puestos nuevos, ya reasignados, no obligan a nadie a cruzar la formación.
            var slots = Enumerable.Range(0, 12).Select(i => squad.Command.SlotOf(i)).ToList();
            for (int i = 0; i < 12; i++)
            {
                for (int j = i + 1; j < 12; j++)
                {
                    Assert.That(FormationAndOrdersTests.SegmentsCross(before[i], slots[i], before[j], slots[j]), Is.False);
                }
            }
            Run(new[] { squad }, 1f / 30f, 120f);
            Assert.That(squad.Command.March.Anchor.Z, Is.EqualTo(-30f).Within(0.01f));
        }

        [Test]
        public void Guerrilla_CambiaDeFrenteSinRuedaDeMedioMinuto()
        {
            SimSquad squad = Create(FormationType.Skirmish, 12, Vec3.Zero, 6, scatter: 0f);
            squad.Final = new Vec3(0f, 0f, 1f);
            Run(new[] { squad }, 1f / 30f, 60f);
            // Frente de 55 m: una rueda de 90° llevaría ~24 s. Cada hombre va por su lado a su puesto nuevo.
            squad.Final = new Vec3(1f, 0f, 0f);
            squad.Command.MoveTo(new List<Vec3> { new Vec3(50f, 0f, 0f) }, squad.Final, squad.Soldiers);
            float t = Run(new[] { squad }, 1f / 30f, 200f);
            Assert.That(t, Is.LessThan(50f / SquadMarch.MarchSpeedMps + 25f));
            Assert.That(squad.Command.MaxLag(squad.Soldiers), Is.LessThan(0.05f));
        }

        [Test]
        public void Bajas_LosDemasCierranFilas()
        {
            SimSquad squad = Create(FormationType.Line, 12, Vec3.Zero, 7, scatter: 0f);
            squad.Final = new Vec3(0f, 0f, 1f);
            Run(new[] { squad }, 1f / 30f, 60f);
            // Caen tres hombres de la primera fila.
            squad.Soldiers.RemoveAt(0);
            squad.Soldiers.RemoveAt(3);
            squad.Soldiers.RemoveAt(5);
            squad.Command.RemoveSoldier(squad.Soldiers);
            Assert.That(squad.Command.Count, Is.EqualTo(9));
            Run(new[] { squad }, 1f / 30f, 30f);
            Assert.That(squad.Command.MaxLag(squad.Soldiers), Is.LessThan(0.05f));
            Assert.That(squad.Soldiers.Count(s => Math.Abs(s.Z) < 0.1f), Is.EqualTo(5), "la primera fila se rehace con 5");
        }

        [Test]
        public void CambioDeFormacion_DeLineaAGuerrilla()
        {
            SimSquad squad = Create(FormationType.Line, 10, Vec3.Zero, 8, scatter: 0f);
            squad.Final = new Vec3(0f, 0f, 1f);
            Run(new[] { squad }, 1f / 30f, 60f);
            squad.Command.SetFormation(FormationType.Skirmish, squad.Soldiers);
            float t = Run(new[] { squad }, 1f / 30f, 60f);
            Assert.That(squad.Command.MaxLag(squad.Soldiers), Is.LessThan(0.05f));
            float spread = squad.Soldiers.Max(s => s.X) - squad.Soldiers.Min(s => s.X);
            Assert.That(spread, Is.EqualTo(45f).Within(0.1f));
            Assert.That(t, Is.LessThan(20f), "abrirse en guerrilla lleva segundos");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Melee;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Melee
{
    /// <summary>ROADMAP 3.4 — estocada y tajo: trayectoria de la hoja y colisión precisa contra muñecos.</summary>
    public class MeleeCombatTests
    {
        private static readonly Vec3 EyePosition = new Vec3(0f, 1.6f, 0f);
        private static readonly ViewFrame Eye = ViewFrame.LookingAlong(EyePosition, new Vec3(0f, 0f, 1f));

        private static MeleeAttackProfile Bayonet() => MeleeAttackProfile.BayonetThrust(WeaponCatalog.Comblain());
        private static MeleeAttackProfile CorvoSlash() => MeleeAttackProfile.BladeSlash(WeaponCatalog.Corvo());
        private static MeleeAttackProfile CorvoThrust() => MeleeAttackProfile.BladeThrust(WeaponCatalog.Corvo());

        /// <summary>Poste vertical de práctica a <paramref name="distance"/> m, desviado <paramref name="yawDeg"/> grados (positivo: derecha).</summary>
        private static MeleeTargetShape Post(int id, float distance, float yawDeg = 0f, float radius = 0.2f)
        {
            double yaw = yawDeg * MathUtil.Deg2Rad;
            float x = (float)Math.Sin(yaw) * distance, z = (float)Math.Cos(yaw) * distance;
            return new MeleeTargetShape(id, 0, new Capsule(new Vec3(x, 0f, z), new Vec3(x, 2.2f, z), radius));
        }

        /// <summary>Muñeco de paja: torso (cápsula) y cabeza (esfera, daño ×1,5).</summary>
        private static List<MeleeTargetShape> Dummy(int id, float distance)
        {
            return new List<MeleeTargetShape>
            {
                new MeleeTargetShape(id, 0, new Capsule(new Vec3(0f, 0.9f, distance), new Vec3(0f, 1.3f, distance), 0.2f)),
                new MeleeTargetShape(id, 1, Capsule.Sphere(new Vec3(0f, 1.68f, distance), 0.11f), 1.5f),
            };
        }

        private sealed class Outcome
        {
            public readonly List<MeleeHit> Hits = new List<MeleeHit>();
            public float BusySeconds;
        }

        private static Outcome Swing(MeleeAttackProfile profile, IReadOnlyList<MeleeTargetShape> targets, float dt, ViewFrame? eye = null)
        {
            ViewFrame frame = eye ?? Eye;
            var model = new MeleeAttackModel();
            Assert.That(model.Start(profile), Is.True);
            var outcome = new Outcome();
            int frames = 0;
            while (model.IsBusy && frames < 10000)
            {
                outcome.Hits.AddRange(model.Step(dt, frame, frame, targets));
                frames++;
            }
            outcome.BusySeconds = frames * dt;
            return outcome;
        }

        // ------------------------------------------------------------------------------------------
        // Geometría
        // ------------------------------------------------------------------------------------------

        [Test]
        public void SegmentoContraCapsula()
        {
            var post = new Capsule(new Vec3(0f, 0f, 2f), new Vec3(0f, 2f, 2f), 0.2f);
            Assert.That(MeleeGeometry.SegmentHitsCapsule(new Vec3(0f, 1f, 0f), new Vec3(0f, 1f, 1.81f), post, out Vec3 p, out float depth), Is.True);
            Assert.That(p.Z, Is.EqualTo(1.8f).Within(1e-4f), "punto de entrada en la superficie");
            Assert.That(depth, Is.EqualTo(0.01f).Within(1e-4f));
            Assert.That(MeleeGeometry.SegmentHitsCapsule(new Vec3(0f, 1f, 0f), new Vec3(0f, 1f, 1.79f), post, out _, out _), Is.False);
            // Segmento que cruza por delante sin tocar, y por encima del extremo de la cápsula.
            Assert.That(MeleeGeometry.SegmentHitsCapsule(new Vec3(-1f, 1f, 1.75f), new Vec3(1f, 1f, 1.75f), post, out _, out _), Is.False);
            Assert.That(MeleeGeometry.SegmentHitsCapsule(new Vec3(-1f, 2.25f, 2f), new Vec3(1f, 2.25f, 2f), post, out _, out _), Is.False);
            Assert.That(MeleeGeometry.SegmentHitsCapsule(new Vec3(-1f, 2.15f, 2f), new Vec3(1f, 2.15f, 2f), post, out _, out _), Is.True);
            Assert.That(MeleeGeometry.SurfaceDistance(new Vec3(0f, 1f, 0f), post), Is.EqualTo(1.8f).Within(1e-5f));
        }

        [Test]
        public void SegmentosParalelosYDegenerados()
        {
            float d2 = MeleeGeometry.ClosestPointsSegmentSegment(new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 1f),
                new Vec3(0.5f, 0f, 0.2f), new Vec3(0.5f, 0f, 3f), out _, out _);
            Assert.That(d2, Is.EqualTo(0.25f).Within(1e-5f));
            d2 = MeleeGeometry.ClosestPointsSegmentSegment(Vec3.Zero, Vec3.Zero, new Vec3(1f, 1f, 0f), new Vec3(1f, 1f, 0f), out _, out _);
            Assert.That(d2, Is.EqualTo(2f).Within(1e-5f));
        }

        // ------------------------------------------------------------------------------------------
        // Trayectorias y tiempos
        // ------------------------------------------------------------------------------------------

        [Test]
        public void LaPunta_LlegaExactamenteAlAlcanceYNoLoSupera()
        {
            foreach (MeleeAttackProfile profile in new[] { Bayonet(), CorvoSlash(), CorvoThrust() })
            {
                float max = profile.MaxTipDistance(4000);
                Assert.That(max, Is.EqualTo(profile.ReachM).Within(0.003f), profile.Weapon.Id + " " + profile.Name);
            }
            Bayonet().Pose(1f, out _, out Vec3 tip);
            Assert.That(tip.X, Is.EqualTo(0f).Within(1e-5f), "la estocada acaba en el centro de la mira");
            Assert.That(tip.Magnitude, Is.EqualTo(WeaponCatalog.Comblain().MeleeReachM).Within(1e-4f));
        }

        [Test]
        public void LaHoja_TieneLongitudConstanteYLaTrayectoriaEsContinua()
        {
            foreach (MeleeAttackProfile profile in new[] { Bayonet(), CorvoSlash() })
            {
                Vec3 previous = default;
                for (int i = 0; i <= 1000; i++)
                {
                    profile.Pose(-1f + 2f * i / 1000f, out Vec3 hilt, out Vec3 tip);
                    Assert.That((tip - hilt).Magnitude, Is.EqualTo(profile.BladeLengthM).Within(1e-4f));
                    if (i > 0) Assert.That((tip - previous).Magnitude, Is.LessThan(0.02f), "sin saltos");
                    previous = tip;
                }
            }
        }

        [Test]
        public void ElTajo_BarreDeDerechaAIzquierdaYDeArribaAbajo()
        {
            MeleeAttackProfile slash = CorvoSlash();
            slash.Pose(slash.ActiveStroke(0f), out _, out Vec3 start);
            slash.Pose(slash.ActiveStroke(1f), out _, out Vec3 end);
            Assert.That(start.X, Is.GreaterThan(0.3f));
            Assert.That(end.X, Is.LessThan(-0.3f));
            Assert.That(start.Y, Is.GreaterThan(end.Y + 0.4f));
        }

        [TestCase(WeaponCatalog.ComblainId)]
        [TestCase(WeaponCatalog.CorvoId)]
        public void ElCiclo_DuraLoQueDiceLaFicha(string weaponId)
        {
            WeaponSpec weapon = WeaponCatalog.Get(weaponId);
            MeleeAttackProfile profile = weapon.IsFirearm ? Bayonet() : CorvoSlash();
            Assert.That(profile.CycleSeconds, Is.EqualTo(weapon.MeleeCycleSeconds).Within(1e-5f));
            Outcome o = Swing(profile, Array.Empty<MeleeTargetShape>(), 1f / 240f);
            Assert.That(o.Hits, Is.Empty);
            Assert.That(o.BusySeconds, Is.EqualTo(weapon.MeleeCycleSeconds).Within(1f / 240f + MeleeAttackModel.TickSeconds));
        }

        [Test]
        public void UnGolpe_NoSeInterrumpeConOtro()
        {
            var model = new MeleeAttackModel();
            Assert.That(model.Start(Bayonet()), Is.True);
            model.Step(0.1f, Eye, Eye, null);
            Assert.That(model.Start(CorvoSlash()), Is.False);
            Assert.That(model.Profile.Kind, Is.EqualTo(MeleeAttackKind.Thrust));
            Assert.Throws<ArgumentException>(() => MeleeAttackProfile.BayonetThrust(WeaponCatalog.Winchester()), "la carabina no lleva bayoneta");
        }

        // ------------------------------------------------------------------------------------------
        // Colisión precisa contra muñecos (verificación de ROADMAP 3.4)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Frontera del alcance al centímetro: la punta de la bayoneta llega a 1,90 m del ojo, así que toca un poste de
        /// 0,20 m de radio si su eje está a 2,10 m o menos.
        /// </summary>
        [TestCase(2.08f, true)]
        [TestCase(2.095f, true)]
        [TestCase(2.105f, false)]
        [TestCase(2.12f, false)]
        [TestCase(1.2f, true)]
        public void Estocada_FronteraDelAlcanceAlCentimetro(float distance, bool expectHit)
        {
            Outcome o = Swing(Bayonet(), new[] { Post(1, distance) }, 1f / 60f);
            Assert.That(o.Hits.Count, Is.EqualTo(expectHit ? 1 : 0));
            if (!expectHit) return;
            MeleeHit hit = o.Hits[0];
            var axis = new Vec3(0f, hit.Point.Y, distance);
            Assert.That((hit.Point - axis).Magnitude, Is.EqualTo(0.2f).Within(0.002f), "el impacto está en la superficie");
            Assert.That(hit.Damage, Is.EqualTo(WeaponCatalog.Comblain().MeleeDamage));
            Assert.That(hit.Kind, Is.EqualTo(MeleeAttackKind.Thrust));
            Assert.That(hit.Direction.Z, Is.GreaterThan(0.9f), "la hoja avanza hacia el muñeco");
            MeleeAttackProfile p = Bayonet();
            Assert.That(hit.Time, Is.InRange(p.WindupSeconds, p.WindupSeconds + p.ActiveSeconds + 1e-4f), "solo hiere en la fase de golpe");
        }

        [TestCase(0.1f, true)]
        [TestCase(-0.1f, true)]
        [TestCase(0.5f, false)]
        [TestCase(-0.35f, false)]
        public void Estocada_SoloHiereLoQueTieneDelante(float lateral, bool expectHit)
        {
            var post = new MeleeTargetShape(1, 0, new Capsule(new Vec3(lateral, 0f, 1.6f), new Vec3(lateral, 2.2f, 1.6f), 0.2f));
            Assert.That(Swing(Bayonet(), new[] { post }, 1f / 60f).Hits.Count, Is.EqualTo(expectHit ? 1 : 0));
        }

        [Test]
        public void Estocada_ApuntandoALaCabeza_ONoAlCuerpo()
        {
            // Mirando a la cabeza del muñeco (1,68 m) a 1,6 m: la punta entra en la cabeza, con daño ×1,5.
            List<MeleeTargetShape> dummy = Dummy(7, 1.6f);
            ViewFrame atHead = ViewFrame.LookingAlong(EyePosition, new Vec3(0f, 0.1f, 1.6f));
            Outcome head = Swing(Bayonet(), dummy, 1f / 60f, atHead);
            Assert.That(head.Hits.Single().PartId, Is.EqualTo(1));
            Assert.That(head.Hits.Single().Damage, Is.EqualTo(WeaponCatalog.Comblain().MeleeDamage * 1.5f).Within(1e-3f));

            ViewFrame atChest = ViewFrame.LookingAlong(EyePosition, new Vec3(0f, 1.15f - 1.6f, 1.6f));
            Outcome chest = Swing(Bayonet(), dummy, 1f / 60f, atChest);
            Assert.That(chest.Hits.Single().PartId, Is.EqualTo(0), "al pecho");
            Assert.That(chest.Hits.Single().TargetId, Is.EqualTo(7));
        }

        [Test]
        public void Estocada_SeQuedaEnElPrimero_ElTajoAlcanzaDos()
        {
            // Dos muñecos en fila: la bayoneta se clava en el primero y no llega al segundo.
            Outcome thrust = Swing(Bayonet(), new[] { Post(1, 1.6f, radius: 0.15f), Post(2, 2.0f, radius: 0.15f) }, 1f / 60f);
            Assert.That(thrust.Hits.Select(h => h.TargetId), Is.EqualTo(new[] { 1 }));

            // Tres postes en el arco del tajo: corta dos y se detiene.
            Outcome slash = Swing(CorvoSlash(), new[] { Post(1, 0.9f, 30f, 0.12f), Post(2, 0.9f, 0f, 0.12f), Post(3, 0.9f, -30f, 0.12f) }, 1f / 60f);
            Assert.That(slash.Hits.Select(h => h.TargetId), Is.EqualTo(new[] { 1, 2 }), "de derecha a izquierda");
            Assert.That(slash.Hits[1].Time, Is.GreaterThan(slash.Hits[0].Time));
        }

        [Test]
        public void Tajo_AlcanzaAlLadoLoQueLaEstocadaNo()
        {
            MeleeTargetShape left = Post(1, 0.85f, -40f, 0.15f);
            Assert.That(Swing(CorvoSlash(), new[] { left }, 1f / 60f).Hits.Count, Is.EqualTo(1));
            Assert.That(Swing(CorvoThrust(), new[] { left }, 1f / 60f).Hits.Count, Is.EqualTo(0));
            Assert.That(Swing(Bayonet(), new[] { left }, 1f / 60f).Hits.Count, Is.EqualTo(0));
            // Y fuera del alcance del corvo (1,1 m), ni el tajo.
            Assert.That(Swing(CorvoSlash(), new[] { Post(2, 1.45f, -40f, 0.15f) }, 1f / 60f).Hits.Count, Is.EqualTo(0));
        }

        [TestCase(130f)]
        [TestCase(180f)]
        [TestCase(-130f)]
        public void ElTajo_NoAlcanzaLoQueTieneDetras(float yaw)
        {
            Assert.That(Swing(CorvoSlash(), new[] { Post(1, 0.8f, yaw, 0.15f) }, 1f / 60f).Hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void DemasiadoCercaParaLaBayoneta_PeroNoParaElCorvo()
        {
            MeleeTargetShape close = Post(1, 0.45f, 0f, 0.15f);
            Assert.That(Swing(Bayonet(), new[] { close }, 1f / 60f).Hits.Count, Is.EqualTo(0), "la punta ya está más allá del cuerpo");
            Assert.That(Swing(CorvoThrust(), new[] { close }, 1f / 60f).Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void MismoImpacto_A20_60_144y37FPS()
        {
            MeleeTargetShape[] targets = { Post(1, 0.9f, 20f, 0.12f), Post(2, 1.9f) };
            foreach (MeleeAttackProfile profile in new[] { Bayonet(), CorvoSlash() })
            {
                MeleeHit reference = Swing(profile, targets, 1f / 144f).Hits[0];
                foreach (float dt in new[] { 1f / 20f, 1f / 37f, 1f / 60f })
                {
                    MeleeHit hit = Swing(profile, targets, dt).Hits[0];
                    Assert.That(hit.TargetId, Is.EqualTo(reference.TargetId));
                    Assert.That(hit.Time, Is.EqualTo(reference.Time).Within(1e-4f), profile.Name + " dt=" + dt);
                    Assert.That((hit.Point - reference.Point).Magnitude, Is.LessThan(1e-3f));
                }
            }
        }

        [Test]
        public void ElTajo_NoAtraviesaUnPosteFinoNiA20FPS()
        {
            // La punta del tajo va a más de 15 m/s: sin subpasos, a 20 FPS saltaría ~0,8 m por fotograma.
            MeleeAttackProfile slash = CorvoSlash();
            float fastest = 0f;
            for (int i = 1; i <= 200; i++)
            {
                slash.Pose(slash.ActiveStroke((i - 1) / 200f), out _, out Vec3 a);
                slash.Pose(slash.ActiveStroke(i / 200f), out _, out Vec3 b);
                fastest = Math.Max(fastest, (b - a).Magnitude / (slash.ActiveSeconds / 200f));
            }
            Assert.That(fastest, Is.GreaterThan(15f));

            for (float yaw = -30f; yaw <= 30f; yaw += 7.5f)
            {
                Assert.That(Swing(slash, new[] { Post(1, 0.8f, yaw, 0.03f) }, 1f / 20f).Hits.Count, Is.EqualTo(1), "poste a " + yaw + "°");
            }
        }

        [Test]
        public void CadaMunecoRecibeUnSoloGolpePorAtaque()
        {
            // El tajo cruza cabeza y torso del mismo muñeco: un único impacto.
            List<MeleeTargetShape> dummy = Dummy(3, 0.75f);
            ViewFrame down = ViewFrame.LookingAlong(EyePosition, new Vec3(0f, -0.35f, 1f));
            Outcome o = Swing(CorvoSlash(), dummy, 1f / 60f, down);
            Assert.That(o.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void ElImpacto_CongelaElGolpeUnInstante()
        {
            MeleeAttackProfile bayonet = Bayonet();
            Outcome miss = Swing(bayonet, Array.Empty<MeleeTargetShape>(), 1f / 240f);
            Outcome hit = Swing(bayonet, new[] { Post(1, 1.7f) }, 1f / 240f);
            // Con impacto: la hoja se retira desde donde se clavó (antes) pero con la congelación de 70 ms.
            Assert.That(hit.Hits.Count, Is.EqualTo(1));
            float expected = hit.Hits[0].Time + bayonet.HitStopSeconds + bayonet.RecoverySeconds;
            Assert.That(hit.BusySeconds, Is.EqualTo(expected).Within(0.01f));
            Assert.That(hit.BusySeconds, Is.LessThan(miss.BusySeconds + bayonet.HitStopSeconds));
        }

        [Test]
        public void GirarDuranteElTajo_CambiaDondeCorta()
        {
            // Si el jugador gira 90° a la derecha mientras corta, el tajo alcanza el poste que tiene ahora delante.
            MeleeTargetShape right = new MeleeTargetShape(1, 0, new Capsule(new Vec3(0.8f, 0f, 0f), new Vec3(0.8f, 2.2f, 0f), 0.12f));
            var model = new MeleeAttackModel();
            model.Start(CorvoSlash());
            ViewFrame turned = ViewFrame.LookingAlong(EyePosition, new Vec3(1f, 0f, 0f));
            var hits = new List<MeleeHit>();
            model.Step(0.1f, Eye, turned, new[] { right });
            while (model.IsBusy) hits.AddRange(model.Step(1f / 60f, turned, turned, new[] { right }));
            Assert.That(hits.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------------------------------
        // Guardia al acercarse
        // ------------------------------------------------------------------------------------------

        [Test]
        public void AlAcercarse_ElArmaSePoneEnGuardia()
        {
            float reach = WeaponCatalog.Comblain().MeleeReachM;
            float previous = -1f;
            for (float d = 6f; d >= 0.5f; d -= 0.1f)
            {
                float surface = MeleeProximity.NearestInFront(Eye, new[] { Post(1, d) }, 35f, out int id);
                Assert.That(id, Is.EqualTo(1));
                Assert.That(surface, Is.EqualTo(Math.Max(0f, d - 0.2f)).Within(1e-4f));
                float w = MeleeProximity.GuardWeight(surface, reach);
                Assert.That(w, Is.GreaterThanOrEqualTo(previous - 1e-6f), "crece al acercarse");
                previous = w;
            }
            Assert.That(MeleeProximity.GuardWeight(reach, reach), Is.EqualTo(1f));
            Assert.That(MeleeProximity.GuardWeight(reach + 1.5f, reach), Is.EqualTo(0f));
            Assert.That(MeleeProximity.GuardWeight(float.PositiveInfinity, reach), Is.EqualTo(0f));
        }

        [Test]
        public void LaGuardia_IgnoraLoQueNoSeMira()
        {
            MeleeTargetShape behind = Post(1, 1.5f, 180f);
            MeleeTargetShape side = Post(2, 1.5f, 80f);
            MeleeTargetShape ahead = Post(3, 3f, 10f);
            float d = MeleeProximity.NearestInFront(Eye, new[] { behind, side, ahead }, 35f, out int id);
            Assert.That(id, Is.EqualTo(3), "el de delante, aunque esté más lejos");
            Assert.That(d, Is.EqualTo(2.8f).Within(1e-3f));
            Assert.That(MeleeProximity.NearestInFront(Eye, new[] { behind }, 35f, out id), Is.EqualTo(float.PositiveInfinity));
            Assert.That(id, Is.EqualTo(-1));
        }
    }
}

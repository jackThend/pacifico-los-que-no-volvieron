using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Effects;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Effects
{
    /// <summary>ROADMAP 3.3 — humo de pólvora negra: física de la bocanada y prueba de estrés de 20 disparos.</summary>
    public class BlackPowderSmokeTests
    {
        private static readonly Vec3 Eye = new Vec3(0f, 1.6f, 0f);
        private static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);

        /// <summary>Boca del fusil de la escena de Pisagua: encarado, sobre la línea de mira; a la cadera, abajo a la derecha.</summary>
        private static Vec3 Muzzle(bool aimed) => aimed ? Eye + new Vec3(0f, -0.04f, 1.2f) : Eye + new Vec3(0.22f, -0.165f, 1.23f);

        // ------------------------------------------------------------------------------------------
        // Bocanada
        // ------------------------------------------------------------------------------------------

        [Test]
        public void LaMasaDeHumo_EsLaFraccionSolidaDeLaCarga()
        {
            var smoke = new BlackPowderSmokeModel();
            // 4,9 g de pólvora del Comblain → ~2,7 g de residuos sólidos en suspensión.
            Assert.That(smoke.SmokeMassFor(WeaponCatalog.Comblain().PowderChargeG), Is.EqualTo(2.744f).Within(0.01f));
            Assert.That(smoke.SmokeMassFor(0f), Is.EqualTo(0f));
            Assert.That(smoke.Emit(Muzzle(true), Forward, 0f), Is.EqualTo(-1), "sin carga no hay humo");
            Assert.That(smoke.Count, Is.EqualTo(0));
            Assert.That(smoke.Settings.Source.References, Is.Not.Empty);
        }

        [Test]
        public void ElChorro_SeFrenaAUnMetroYLaNubeSeAbre()
        {
            var smoke = new BlackPowderSmokeModel(new BlackPowderSmokeSettings { DirectionJitterDeg = 0f, SpeedJitter = 0f, SizeJitter = 0f });
            var s = smoke.Settings;
            smoke.Emit(Vec3.Zero, Forward, 5f);
            Assert.That(smoke[0].CenterOpticalDepth, Is.GreaterThan(3f), "recién salida, la bocanada es opaca");

            for (int i = 0; i < 60; i++) smoke.Step(1f / 120f);
            SmokePuff p = smoke[0];
            // Recorrido del chorro: v₀·τ = 14 · 0,08 ≈ 1,1 m.
            Assert.That(p.Position.Z, Is.EqualTo(s.JetSpeedMps * s.JetDecaySeconds).Within(0.02f));
            Assert.That(p.Position.Y, Is.EqualTo(s.BuoyancyMps * 0.5f).Within(0.05f), "sube");
            Assert.That(p.RadiusM, Is.InRange(0.5f, 1f), "una nube de alrededor de un metro de diámetro");
        }

        [Test]
        public void LaCargaMayor_HaceUnaNubeMayor()
        {
            var smoke = new BlackPowderSmokeModel(new BlackPowderSmokeSettings { SizeJitter = 0f });
            smoke.Emit(Vec3.Zero, Forward, WeaponCatalog.Winchester().PowderChargeG);
            smoke.Emit(Vec3.Zero, Forward, WeaponCatalog.Chassepot().PowderChargeG);
            smoke.Step(0.5f);
            Assert.That(smoke[1].RadiusM, Is.GreaterThan(smoke[0].RadiusM));
            Assert.That(smoke[1].MassG, Is.GreaterThan(smoke[0].MassG * 2f));
        }

        [Test]
        public void ElViento_SeLlevaElHumo()
        {
            var smoke = new BlackPowderSmokeModel { Wind = new Vec3(3f, 0f, 0f) };
            smoke.Emit(Muzzle(true), Forward, 4.9f);
            for (int i = 0; i < 60; i++) smoke.Step(1f / 60f);
            // Tras el transitorio (τ = 0,08 s) la nube viaja con el aire: ~3 m en 1 s.
            Assert.That(smoke[0].Position.X, Is.EqualTo(3f * (1f - smoke.Settings.JetDecaySeconds)).Within(0.2f));
        }

        [Test]
        public void IgualA30y144FPS()
        {
            SmokePuff Run(float dt)
            {
                var smoke = new BlackPowderSmokeModel(seed: 7) { Wind = new Vec3(1.5f, 0f, -0.5f) };
                smoke.Emit(Muzzle(true), Forward, 5.2f, new Vec3(0.8f, 0f, 1.2f));
                for (float t = 0f; t < 1.5f - 1e-4f; t += dt) smoke.Step(dt);
                return smoke[0];
            }
            // 1,5 s son 45 pasos de 1/30 y 216 de 1/144: el mismo instante.
            SmokePuff a = Run(1f / 30f), b = Run(1f / 144f);
            Assert.That((a.Position - b.Position).Magnitude, Is.LessThan(0.01f));
            Assert.That(a.RadiusM, Is.EqualTo(b.RadiusM).Within(0.005f));
            Assert.That(a.CenterOpticalDepth, Is.EqualTo(b.CenterOpticalDepth).Within(0.005f));
        }

        [Test]
        public void ConLaMismaSemilla_ElHumoEsIdentico()
        {
            var a = new BlackPowderSmokeModel(seed: 3);
            var b = new BlackPowderSmokeModel(seed: 3);
            for (int i = 0; i < 5; i++)
            {
                a.Emit(Muzzle(false), Forward, 5f);
                b.Emit(Muzzle(false), Forward, 5f);
                a.Step(0.1f);
                b.Step(0.1f);
            }
            for (int i = 0; i < a.Count; i++) Assert.That(a[i].Position, Is.EqualTo(b[i].Position));
        }

        [Test]
        public void ElPresupuesto_RetiraLasMasAntiguas()
        {
            var smoke = new BlackPowderSmokeModel(new BlackPowderSmokeSettings { MaxPuffs = 16 });
            var ids = new List<int>();
            for (int i = 0; i < 40; i++) ids.Add(smoke.Emit(new Vec3(i, 1f, 0f), Forward, 5f));
            Assert.That(smoke.Count, Is.EqualTo(16));
            Assert.That(Enumerable.Range(0, smoke.Count).Select(i => smoke[i].Id), Is.EqualTo(ids.Skip(24)), "quedan las 16 más recientes, en orden");
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = smoke[16]);
        }

        [Test]
        public void SeDisipaEnPocosSegundos()
        {
            var smoke = new BlackPowderSmokeModel();
            smoke.Emit(Muzzle(true), Forward, 5.6f);
            float t = 0f;
            while (smoke.Count > 0 && t < 10f)
            {
                smoke.Step(1f / 60f);
                t += 1f / 60f;
            }
            Assert.That(t, Is.InRange(1.5f, 4f), "rapidez cinematográfica: se ve, pero no se queda");
        }

        [Test]
        public void ElOjo_NoVeElHumoQueTieneDetras()
        {
            var smoke = new BlackPowderSmokeModel(new BlackPowderSmokeSettings { DirectionJitterDeg = 0f });
            smoke.Emit(Eye + new Vec3(0f, 0f, -3f), -Forward, 5f);
            smoke.Step(0.3f);
            Assert.That(smoke.Transmittance(Eye, Forward), Is.GreaterThan(0.999f));
            Assert.That(smoke.Transmittance(Eye, -Forward), Is.LessThan(0.9f), "mirando hacia atrás sí se ve");
        }

        [Test]
        public void ElDesvanecimientoCercano_OcultaLoQueTocaLaCamara()
        {
            var smoke = new BlackPowderSmokeModel();
            Assert.That(smoke.NearFade(0.3f), Is.EqualTo(0f));
            Assert.That(smoke.NearFade(5f), Is.EqualTo(1f));
            smoke.Emit(Eye + new Vec3(0f, 0f, 0.3f), Forward, 5f);
            Assert.That(smoke.Transmittance(Eye, Forward), Is.GreaterThan(0.99f));
        }

        // ------------------------------------------------------------------------------------------
        // Prueba de estrés (verificación de ROADMAP 3.3)
        // ------------------------------------------------------------------------------------------

        private sealed class StressResult
        {
            public int Shots;
            public float MinCone = 1f;
            public float MeanCenter;
            /// <summary>Transmitancia en el centro justo antes de cada disparo (a partir del segundo).</summary>
            public readonly List<float> BeforeShot = new List<float>();
            /// <summary>Tiempo que la vista central queda tapada (T &lt; 0,5) tras cada disparo.</summary>
            public readonly List<float> Blocked = new List<float>();
            public float CenterAfter3s;
            public float SecondsUntilClear;
        }

        /// <summary>
        /// 20 disparos a la máxima cadencia que permite el mecanismo (el ciclo real de <see cref="RifleCycleModel"/>,
        /// con las recargas del depósito), sin viento (el peor caso: el humo no se va de delante), a 60 fps.
        /// </summary>
        private static StressResult Stress(string weaponId, bool aimed)
        {
            WeaponSpec weapon = WeaponCatalog.Get(weaponId);
            var smoke = new BlackPowderSmokeModel();
            var rifle = new RifleCycleModel(weapon, 200) { AutoReload = true };
            var result = new StressResult();
            const float dt = 1f / 60f;
            float t = 0f, lastShot = 0f, blocked = 0f, sum = 0f;
            int frames = 0;

            while (t < 120f)
            {
                if (result.Shots < 20 && rifle.CanFire)
                {
                    if (result.Shots > 0)
                    {
                        result.BeforeShot.Add(smoke.Transmittance(Eye, Forward));
                        result.Blocked.Add(blocked);
                    }
                    blocked = 0f;
                    Assert.That(rifle.PullTrigger(), Is.EqualTo(TriggerResult.Fired));
                    smoke.Emit(Muzzle(aimed), Forward, weapon.PowderChargeG);
                    result.Shots++;
                    lastShot = t;
                }
                else if (result.Shots < 20 && rifle.CanReload && rifle.Stage == RifleStage.Ready)
                {
                    rifle.Reload();
                }

                rifle.Step(dt);
                smoke.Step(dt);
                t += dt;

                float center = smoke.Transmittance(Eye, Forward);
                if (center < 0.5f) blocked += dt;
                result.MinCone = Math.Min(result.MinCone, smoke.ViewClarity(Eye, Forward));
                sum += center;
                frames++;

                if (result.Shots == 20)
                {
                    float since = t - lastShot;
                    if (result.SecondsUntilClear == 0f && smoke.Count == 0) result.SecondsUntilClear = since;
                    if (since >= 3f && result.CenterAfter3s == 0f) result.CenterAfter3s = center;
                    if (since >= 3f && smoke.Count == 0) break;
                }
            }
            result.Blocked.Add(blocked);
            result.MeanCenter = sum / frames;
            return result;
        }

        [TestCase(WeaponCatalog.ComblainId, true)]
        [TestCase(WeaponCatalog.ComblainId, false)]
        [TestCase(WeaponCatalog.ChassepotId, true)]
        [TestCase(WeaponCatalog.GrasId, true)]
        [TestCase(WeaponCatalog.RemingtonId, true)]
        [TestCase(WeaponCatalog.WinchesterId, true)]
        [TestCase(WeaponCatalog.WinchesterId, false)]
        public void Estres_20DisparosSeguidos_SinSaturacionVisual(string weaponId, bool aimed)
        {
            StressResult r = Stress(weaponId, aimed);
            bool repeater = WeaponCatalog.Get(weaponId).MagazineCapacity > 1;
            string report = weaponId + (aimed ? " encarado" : " a la cadera") +
                            ": T antes de cada disparo " + string.Join(" ", r.BeforeShot.Select(v => v.ToString("0.00"))) +
                            " | tapado " + r.Blocked.Max().ToString("0.00") + " s | media " + r.MeanCenter.ToString("0.00") +
                            " | cono mín. " + r.MinCone.ToString("0.00");
            TestContext.WriteLine(report);

            Assert.That(r.Shots, Is.EqualTo(20), report);

            // 1) El humo se ve: cada disparo levanta una nube que tapa buena parte del centro de la pantalla.
            Assert.That(r.MinCone, Is.LessThan(0.6f), "sin humo visible no hay efecto: " + report);

            // 2) Pero no ciega: la vista central queda tapada menos de medio segundo por disparo…
            Assert.That(r.Blocked.Max(), Is.LessThan(0.5f), report);

            // 3) …al volver a encarar se ve el blanco (con la carabina de repetición, a 0,45 s entre tiros, algo menos)…
            Assert.That(r.BeforeShot.Min(), Is.GreaterThan(repeater ? 0.65f : 0.9f), report);
            Assert.That(r.MeanCenter, Is.GreaterThan(0.6f), report);

            // 4) …y el humo no se acumula: los últimos disparos se ven igual de bien que los primeros.
            float early = r.BeforeShot.Take(4).Average();
            float late = r.BeforeShot.Skip(r.BeforeShot.Count - 10).Average();
            Assert.That(late, Is.GreaterThan(early - 0.03f), "saturación progresiva: " + report);

            // 5) Tres segundos después del último disparo, vista limpia.
            Assert.That(r.CenterAfter3s, Is.GreaterThan(0.99f), report);
            Assert.That(r.SecondsUntilClear, Is.LessThan(4f), report);
        }

        [Test]
        public void Estres_DescargaDe20FusilesJuntoAlJugador()
        {
            // El jugador en mitad de una línea de 21 tiradores a 0,9 m de intervalo; los otros 20 disparan a la vez.
            var smoke = new BlackPowderSmokeModel();
            for (int k = -10; k <= 10; k++)
            {
                if (k == 0) continue;
                smoke.Emit(Muzzle(false) + new Vec3(k * 0.9f, 0f, 0f), Forward, WeaponCatalog.Comblain().PowderChargeG);
            }
            Assert.That(smoke.Count, Is.EqualTo(20), "cabe en el presupuesto");

            float worst = 1f;
            for (int i = 0; i < 180; i++)
            {
                smoke.Step(1f / 60f);
                worst = Math.Min(worst, smoke.ViewClarity(Eye, Forward));
                if (i == 119) Assert.That(smoke.ViewClarity(Eye, Forward), Is.GreaterThan(0.9f), "a los 2 s ya se ve");
            }
            Assert.That(worst, Is.InRange(0.4f, 0.9f), "la descarga se ve como una cortina, pero no es un muro");
        }
    }
}

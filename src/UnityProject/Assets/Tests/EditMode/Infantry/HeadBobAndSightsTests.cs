using System;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Infantry
{
    /// <summary>ROADMAP 3.1 — cabeceo de cámara realista y sin saltos.</summary>
    public class HeadBobModelTests
    {
        private static void Run(HeadBobModel bob, float speed, float seconds, float dt = 1f / 60f, float aim = 0f, Action perFrame = null)
        {
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                bob.Step(speed, true, aim, 0f, dt);
                perFrame?.Invoke();
            }
        }

        [Test]
        public void EnReposo_NoHayCabeceo()
        {
            var bob = new HeadBobModel();
            Run(bob, 0f, 3f);
            Assert.That(bob.OffsetX, Is.EqualTo(0f));
            Assert.That(bob.OffsetY, Is.EqualTo(0f));
            Assert.That(bob.RollDeg, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Caminando_OscilaDentroDeLaAmplitud_UnValleporPaso()
        {
            var bob = new HeadBobModel();
            float minY = 0f, maxAbsX = 0f;
            Run(bob, 2.6f, 1f); // estabiliza la intensidad
            float phaseStart = bob.Phase;
            Run(bob, 2.6f, 3f, perFrame: () =>
            {
                minY = Math.Min(minY, bob.OffsetY);
                maxAbsX = Math.Max(maxAbsX, Math.Abs(bob.OffsetX));
            });
            Assert.That(minY, Is.LessThan(-0.005f));
            Assert.That(-minY, Is.LessThanOrEqualTo(bob.VerticalAmplitude + 1e-4f));
            Assert.That(maxAbsX, Is.LessThanOrEqualTo(bob.LateralAmplitude + 1e-4f));

            // 3 s a 2,6 m/s = 7,8 m en pasos de 0,944 m → 8,26 pasos = 8,26 medios ciclos (≈2,75 pasos/s).
            float steps = (bob.Phase - phaseStart) / (float)Math.PI;
            Assert.That(steps, Is.EqualTo(7.8f / bob.StepLengthAt(2.6f)).Within(0.05f));
            Assert.That(steps / 3f, Is.InRange(2.5f, 3.2f), "cadencia de marcha humana");
        }

        [Test]
        public void Carrera_CadenciaRealista()
        {
            var bob = new HeadBobModel();
            float stepsPerSecond = 5.4f / bob.StepLengthAt(5.4f);
            Assert.That(stepsPerSecond, Is.InRange(3.2f, 4f));
        }

        /// <summary>
        /// «Sin jitter» significa sin discontinuidades: el desplazamiento por fotograma cambia gradualmente
        /// (segunda diferencia pequeña), incluso cuando el soldado se detiene en seco o arranca a correr.
        /// </summary>
        [Test]
        public void SinSaltosEntreFotogramas_A60FPS_NiAlArrancarOFrenar()
        {
            var bob = new HeadBobModel();
            const float dt = 1f / 60f;
            float prevY = 0f, prevDeltaY = 0f, maxDelta = 0f, maxSecond = 0f;
            float[] speeds = { 0f, 5.4f, 5.4f, 0f, 2.6f, 5.4f, 0f };
            foreach (float speed in speeds)
            {
                for (int i = 0; i < 60; i++)
                {
                    bob.Step(speed, true, 0f, 0f, dt);
                    float delta = bob.OffsetY - prevY;
                    maxDelta = Math.Max(maxDelta, Math.Abs(delta));
                    maxSecond = Math.Max(maxSecond, Math.Abs(delta - prevDeltaY));
                    prevDeltaY = delta;
                    prevY = bob.OffsetY;
                }
            }
            Assert.That(maxDelta, Is.LessThan(0.01f), "menos de 1 cm por fotograma");
            Assert.That(maxSecond, Is.LessThan(0.005f), "sin cambios bruscos de dirección del movimiento");
        }

        [Test]
        public void AlFrenar_ElCabeceoSeApagaSuavemente()
        {
            var bob = new HeadBobModel();
            Run(bob, 5.4f, 2f);
            Run(bob, 0f, 1f);
            Assert.That(bob.Intensity, Is.LessThan(0.001f));
            Assert.That(Math.Abs(bob.OffsetY), Is.LessThan(1e-3f));
        }

        [Test]
        public void Apuntar_ReduceElCabeceo()
        {
            var free = new HeadBobModel();
            var aiming = new HeadBobModel();
            float maxFree = 0f, maxAim = 0f;
            Run(free, 2.6f, 3f, perFrame: () => maxFree = Math.Max(maxFree, Math.Abs(free.OffsetX)));
            Run(aiming, 2.6f, 3f, aim: 1f, perFrame: () => maxAim = Math.Max(maxAim, Math.Abs(aiming.OffsetX)));
            Assert.That(maxAim, Is.LessThan(maxFree * 0.3f));
        }

        [Test]
        public void Aterrizaje_HundeLaCamaraYSeRecupera_IgualACualquierTasa()
        {
            float MinOffset(float dt, out float after)
            {
                var bob = new HeadBobModel();
                bob.Land(-4f);
                float min = 0f;
                int steps = (int)Math.Round(1.5f / dt);
                for (int i = 0; i < steps; i++)
                {
                    bob.Step(0f, true, 0f, 0f, dt);
                    min = Math.Min(min, bob.OffsetY);
                }
                after = bob.OffsetY;
                return min;
            }

            float dip60 = MinOffset(1f / 60f, out float rest60);
            float dip30 = MinOffset(1f / 30f, out _);
            Assert.That(dip60, Is.LessThan(-0.02f));
            Assert.That(dip60, Is.GreaterThanOrEqualTo(-new HeadBobModel().MaxLandingDip - 1e-3f));
            Assert.That(dip30, Is.EqualTo(dip60).Within(0.01f), "el muelle se integra en subpasos fijos");
            Assert.That(Math.Abs(rest60), Is.LessThan(1e-3f), "vuelve a la posición de reposo");
        }
    }

    /// <summary>ROADMAP 3.1 — miras de época: alza graduada con balística real y encare.</summary>
    public class SightsAndBallisticsTests
    {
        [Test]
        public void Calibracion_ElGrasPierde20msEnLosPrimeros25m()
        {
            var gras = WeaponCatalog.Gras();
            var s = SmallArmsBallistics.Sample(25f, 0.1f, gras.MuzzleVelocityMps, SmallArmsBallistics.DragFactor(gras));
            Assert.That(s.Speed, Is.EqualTo(430f).Within(4f), "dato histórico: 450 → 430 m/s a 25 m");
        }

        [Test]
        public void Gras_AlcanceMaximoConElevacionDe25a35Grados()
        {
            // Dato de los ensayos de Versalles: el alcance máximo se obtenía con 25–35° de elevación.
            var gras = WeaponCatalog.Gras();
            float k = SmallArmsBallistics.DragFactor(gras);
            float RangeAt(float elevation)
            {
                // Mayor distancia a la que la bala aún está por encima del suelo (búsqueda por bisección).
                float lo = 0f, hi = 6000f;
                for (int i = 0; i < 30; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    var s = SmallArmsBallistics.Sample(mid, elevation, gras.MuzzleVelocityMps, k, sightHeightM: 0f);
                    if (s.Reached && s.Height >= 0f) lo = mid;
                    else hi = mid;
                }
                return lo;
            }

            float bestAngle = 0f, bestRange = 0f;
            for (float a = 15f; a <= 45f; a += 2.5f)
            {
                float r = RangeAt(a);
                if (r > bestRange) { bestRange = r; bestAngle = a; }
            }
            Assert.That(bestAngle, Is.InRange(25f, 37.5f));
            Assert.That(bestRange, Is.GreaterThan(1.5f * gras.MaxSightRangeM), "el alza no llega al límite del alcance");
        }

        [Test]
        public void SinRozamiento_CoincideConLaParabolaAnalitica()
        {
            const float v = 400f, elevation = 2f, distance = 1000f;
            var s = SmallArmsBallistics.Sample(distance, elevation, v, dragFactor: 0f, sightHeightM: 0f);
            double theta = elevation * Math.PI / 180.0;
            double expected = distance * Math.Tan(theta) - 9.81 * distance * distance / (2 * v * v * Math.Cos(theta) * Math.Cos(theta));
            Assert.That(s.Height, Is.EqualTo((float)expected).Within(0.01f));
        }

        [Test]
        public void ConRozamiento_HaceFaltaMasElevacionQueEnElVacio()
        {
            var chassepot = WeaponCatalog.Chassepot();
            SmallArmsBallistics.TrySolveElevation(600f, chassepot.MuzzleVelocityMps, SmallArmsBallistics.DragFactor(chassepot), out float withDrag);
            SmallArmsBallistics.TrySolveElevation(600f, chassepot.MuzzleVelocityMps, 0f, out float vacuum);
            Assert.That(withDrag, Is.GreaterThan(vacuum * 1.3f));
        }

        [Test]
        public void Alza_CubreDesde100mHastaElAlcanceDeLaFicha()
        {
            foreach (var weapon in WeaponCatalog.All().Where(w => w.IsFirearm))
            {
                var ladder = new SightLadder(weapon);
                Assert.That(ladder.RangeAt(0), Is.EqualTo(100f), weapon.Id);
                Assert.That(ladder.MaxRangeM, Is.EqualTo(weapon.MaxSightRangeM).Within(0.5f), weapon.Id);
                for (int i = 1; i < ladder.Count; i++)
                {
                    Assert.That(ladder.ElevationAt(i), Is.GreaterThan(ladder.ElevationAt(i - 1)), weapon.Id + " graduación " + i);
                }
            }
        }

        [Test]
        public void Alza_CruzaLaLineaDeMiraALaDistanciaGraduada()
        {
            var ladder = new SightLadder(WeaponCatalog.Comblain());
            ladder.SetRange(400f);
            Assert.That(ladder.RangeM, Is.EqualTo(400f));
            Assert.That(ladder.ImpactOffsetAt(400f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(ladder.ImpactOffsetAt(600f), Is.LessThan(-1f), "con el alza en 400, a 600 m se tira muy bajo");
        }

        [Test]
        public void Alza_EmpiezaEnLaDeCombate_YSeRecorreConLimites()
        {
            var ladder = new SightLadder(WeaponCatalog.Remington());
            Assert.That(ladder.RangeM, Is.EqualTo(SightLadder.DefaultBattleSightM));
            Assert.That(ladder.Lower(), Is.True);
            Assert.That(ladder.Lower(), Is.False, "no hay graduación por debajo de 100 m");
            while (ladder.Raise()) { }
            Assert.That(ladder.RangeM, Is.EqualTo(ladder.MaxRangeM));
        }

        [Test]
        public void Alza_ElevacionesPlausiblesParaLaPolvoraNegra()
        {
            // Sin rozamiento, 1.800 m pedirían ~2,5°; con la bala roma de pólvora negra hacen falta varios grados más.
            var ladder = new SightLadder(WeaponCatalog.Gras());
            ladder.SetRange(1800f);
            Assert.That(ladder.ElevationDeg, Is.InRange(5f, 10f));
        }

        [Test]
        public void Alza_ReutilizaLaTablaDeElevacionesDelMismoFusil()
        {
            var first = new SightLadder(WeaponCatalog.Chassepot());
            int tables = SightLadder.CachedTables;
            var second = new SightLadder(WeaponCatalog.Chassepot());
            Assert.That(SightLadder.CachedTables, Is.EqualTo(tables), "empuñar de nuevo el mismo fusil no recalcula");
            Assert.That(second.ElevationAt(5), Is.EqualTo(first.ElevationAt(5)));

            var modified = WeaponCatalog.Chassepot();
            modified.MuzzleVelocityMps = 380f;
            var third = new SightLadder(modified);
            Assert.That(third.ElevationAt(5), Is.GreaterThan(first.ElevationAt(5)), "otra ficha, otra tabla");
        }

        [Test]
        public void ArmaBlanca_NoTieneAlza()
        {
            Assert.Throws<ArgumentException>(() => new SightLadder(WeaponCatalog.Corvo()));
        }

        [Test]
        public void Encare_DuraSegunElPesoDelFusil()
        {
            var chassepot = new IronSightModel(WeaponCatalog.Chassepot());
            var winchester = new IronSightModel(WeaponCatalog.Winchester());
            Assert.That(chassepot.AimTimeSeconds, Is.GreaterThan(winchester.AimTimeSeconds));

            for (int i = 0; i < 60; i++) chassepot.Step(true, 1f / 60f, 1f, false, false);
            Assert.That(chassepot.IsFullyAimed, Is.True);
            Assert.That(chassepot.FovMultiplier, Is.EqualTo(1f / IronSightModel.ZoomFactor).Within(1e-4f));
            for (int i = 0; i < 60; i++) chassepot.Step(false, 1f / 60f, 1f, false, false);
            Assert.That(chassepot.Progress, Is.EqualTo(0f));
        }

        [Test]
        public void Deriva_CreceConLaFatigaYElMovimiento_YMenguaAgachado()
        {
            float Amplitude(float stamina, bool moving, bool crouched)
            {
                var sights = new IronSightModel(WeaponCatalog.Gras());
                for (int i = 0; i < 180; i++) sights.Step(true, 1f / 60f, stamina, moving, crouched);
                return sights.SwayAmplitudeDeg;
            }

            float rested = Amplitude(1f, false, false);
            Assert.That(Amplitude(0.1f, false, false), Is.GreaterThan(rested * 2f));
            Assert.That(Amplitude(1f, true, false), Is.GreaterThan(rested * 1.5f));
            Assert.That(Amplitude(1f, false, true), Is.LessThan(rested));
        }

        [Test]
        public void Deriva_EsContinuaAunqueCambieLaFatiga()
        {
            var sights = new IronSightModel(WeaponCatalog.Gras());
            float previous = 0f, previousDelta = 0f, maxSecond = 0f;
            for (int i = 0; i < 600; i++)
            {
                float stamina = i < 300 ? 1f : 0f; // cambio brusco de fatiga en el fotograma 300
                sights.Step(true, 1f / 60f, stamina, false, false);
                float delta = sights.SwayYawDeg - previous;
                if (i > 1) maxSecond = Math.Max(maxSecond, Math.Abs(delta - previousDelta));
                previousDelta = delta;
                previous = sights.SwayYawDeg;
            }
            // Si la fase dependiera de t·ω, el cambio de ritmo produciría un salto de décimas de grado.
            Assert.That(maxSecond, Is.LessThan(0.003f));
        }
    }
}

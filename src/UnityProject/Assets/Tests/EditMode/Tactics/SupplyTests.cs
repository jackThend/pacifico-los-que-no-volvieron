using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Tactics
{
    /// <summary>ROADMAP 4.3 — agua (caramayolas), cartuchos y carros de vituallas.</summary>
    public class SupplyTests
    {
        /// <summary>Settings sin compresión de tiempo: las pruebas razonan en horas reales de sol.</summary>
        private static SupplySettings RealTime() => new SupplySettings { TimeScale = 1f };

        private static void Hours(SquadSupply s, float hours, Exertion exertion, float heat, Action<float> each = null)
        {
            for (float t = 0f; t < hours * 3600f; t += 10f)
            {
                s.Step(10f, exertion, heat);
                each?.Invoke(t);
            }
        }

        [Test]
        public void LaCaramayola_DuraPocoMarchandoAlSol()
        {
            var s = new SquadSupply(12, RealTime());
            float emptyAt = -1f;
            Hours(s, 4f, Exertion.Marching, 1f, t => { if (emptyAt < 0f && s.WaterPerManLiters <= 1e-4f) emptyAt = t / 3600f; });
            // 1,2 L/h de sudor: la caramayola (1 L) más el umbral de sed (0,4 L) se agotan en ~1,2 h.
            Assert.That(emptyAt, Is.EqualTo((1f + 0.4f) / 1.2f).Within(0.1f));
            // Sin agua, el déficit sigue creciendo al ritmo del sudor: a las 4 h, ~3,4 L (≈5 % del peso).
            Assert.That(s.DeficitPerManLiters, Is.EqualTo(4f * 1.2f - 1f).Within(0.1f));
            Assert.That(s.DehydrationPercent, Is.InRange(4.5f, 6.5f));
        }

        [Test]
        public void LaCamanchaca_Alivia()
        {
            var sun = new SquadSupply(12, RealTime());
            var fog = new SquadSupply(12, RealTime());
            Hours(sun, 2.5f, Exertion.Marching, 1f);
            Hours(fog, 2.5f, Exertion.Marching, 0.4f);
            Assert.That(fog.DehydrationPercent, Is.LessThan(sun.DehydrationPercent * 0.5f));
            Assert.That(fog.WaterPerManLiters, Is.GreaterThan(0f), "con niebla aún queda agua");
        }

        [Test]
        public void ConAgua_NoSeBebeMasDeLoQueElCuerpoAbsorbe()
        {
            // Combatiendo al sol se suda más (1,5 L/h) de lo que se absorbe (1,2 L/h): aun con agua, el déficit crece.
            var s = new SquadSupply(12, new SupplySettings { TimeScale = 1f, CanteenLiters = 50f });
            Hours(s, 2f, Exertion.Fighting, 1f);
            // Hasta llegar al umbral de sed (0,4 L, a las 0,27 h) no se bebe; después el déficit crece a 1,5 − 1,2 L/h.
            float thirstAt = 0.4f / 1.5f;
            Assert.That(s.DeficitPerManLiters, Is.EqualTo(0.4f + (2f - thirstAt) * (1.5f - 1.2f)).Within(0.02f));
        }

        [Test]
        public void Efectividad_CaeConLaDeshidratacion()
        {
            Assert.That(SquadSupply.EffectivenessAt(0f), Is.EqualTo(1f));
            Assert.That(SquadSupply.EffectivenessAt(2f), Is.EqualTo(1f), "hasta el 2 %, sin efecto");
            Assert.That(SquadSupply.EffectivenessAt(3f), Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(SquadSupply.EffectivenessAt(5f), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(SquadSupply.EffectivenessAt(20f), Is.EqualTo(0.15f));
            float previous = 2f;
            for (float p = 0f; p <= 12f; p += 0.1f)
            {
                float e = SquadSupply.EffectivenessAt(p);
                Assert.That(e, Is.LessThanOrEqualTo(previous + 1e-6f));
                previous = e;
            }
        }

        /// <summary>
        /// Verificación del roadmap: dos escuadras iguales combaten cuatro horas (lo que duró la batalla de Tacna, de
        /// media mañana a primera hora de la tarde) bajo el sol salitrero; a una la
        /// abastece un carro, a la otra no. Sin agua, la segunda marcha más despacio, acierta mucho menos y llega al
        /// umbral del golpe de calor.
        /// </summary>
        [Test]
        public void SinAgua_LasTropasPierdenEfectividadBajoElSol()
        {
            WeaponSpec comblain = WeaponCatalog.Comblain();
            var supplied = new SquadSupply(12, RealTime(), seed: 1);
            var dry = new SquadSupply(12, RealTime(), seed: 1);
            var cart = new SupplyCartModel();
            var fireSupplied = new SquadFireControl(comblain, 12, 7);
            var fireDry = new SquadFireControl(comblain, 12, 7);
            int hitsSupplied = 0, hitsDry = 0, collapsed = 0;

            for (float t = 0f; t < 4f * 3600f; t += 1f)
            {
                supplied.Step(1f, Exertion.Fighting, 1f);
                collapsed += dry.Step(1f, Exertion.Fighting, 1f);
                cart.Resupply(supplied, 1f);
                if (cart.NeedsReload) cart.Reload(60f); // el carro va y vuelve del depósito

                fireSupplied.SetSoldiers(supplied.Men);
                fireDry.SetSoldiers(dry.Men);
                VolleyResult a = fireSupplied.Step(1f, supplied.HasAmmo, 200f, Posture.Standing, 0f, supplied.DispersionScale, supplied.RateFactor);
                VolleyResult b = fireDry.Step(1f, dry.HasAmmo, 200f, Posture.Standing, 0f, dry.DispersionScale, dry.RateFactor);
                supplied.ConsumeCartridges(a.Shots);
                dry.ConsumeCartridges(b.Shots);
                cart.Resupply(supplied, 0f);
                // Se cuentan los aciertos de la última hora, cuando la sed ya ha hecho efecto.
                if (t >= 3f * 3600f)
                {
                    hitsSupplied += a.Hits;
                    hitsDry += b.Hits;
                }
                // Los cartuchos no deciden esta prueba: se reponen a ambas.
                dry.ReceiveCartridges(b.Shots);
            }

            Assert.That(supplied.Effectiveness, Is.GreaterThan(0.9f), "abastecida, sigue en forma");
            Assert.That(dry.Effectiveness, Is.LessThan(0.4f), "sin agua, " + dry.DehydrationPercent.ToString("0.0") + " % del peso perdido");
            Assert.That(dry.SpeedFactor, Is.LessThan(supplied.SpeedFactor * 0.75f));
            Assert.That(hitsDry, Is.LessThan(hitsSupplied * 0.5f), "aciertos en la última hora: " + hitsDry + " frente a " + hitsSupplied);
            Assert.That(dry.DehydrationPercent, Is.GreaterThan(7f), "al borde del golpe de calor (lo comprueba GolpeDeCalor_SoloPasadoEl7Porciento)");
            Assert.That(dry.Men + collapsed, Is.EqualTo(12));
            Assert.That(supplied.Men, Is.EqualTo(12));
        }

        [Test]
        public void GolpeDeCalor_SoloPasadoEl7Porciento()
        {
            var s = new SquadSupply(12, RealTime());
            int collapsed = 0;
            for (float t = 0f; t < 10f * 3600f && s.DehydrationPercent < 6.9f; t += 10f) collapsed += s.Step(10f, Exertion.Marching, 1f);
            Assert.That(collapsed, Is.EqualTo(0));
            for (float t = 0f; t < 2f * 3600f; t += 10f) collapsed += s.Step(10f, Exertion.Marching, 1f);
            Assert.That(collapsed, Is.GreaterThan(0));
            Assert.That(s.Men, Is.EqualTo(12 - collapsed));
        }

        [Test]
        public void LaCompresionDelTiempo_AceleraLaSed()
        {
            var game = new SquadSupply(12); // 15 horas de fisiología por hora de juego
            for (int i = 0; i < 15 * 60; i++) game.Step(1f, Exertion.Marching, 1f); // 15 min de juego
            var real = new SquadSupply(12, RealTime());
            Hours(real, 3.75f, Exertion.Marching, 1f);
            Assert.That(game.DeficitPerManLiters, Is.EqualTo(real.DeficitPerManLiters).Within(0.05f));
        }

        // ------------------------------------------------------------------------------------------
        // Munición
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Cartuchos_SeGastanYSeEconomizan()
        {
            var s = new SquadSupply(10);
            Assert.That(s.Cartridges, Is.EqualTo(1000));
            float full = s.RateFactor;
            Assert.That(s.ConsumeCartridges(850), Is.EqualTo(850));
            Assert.That(s.RateFactor, Is.EqualTo(full * s.Settings.LowAmmoRateFactor).Within(1e-5f), "por debajo del 20 %, economía de fuego");
            Assert.That(s.ConsumeCartridges(500), Is.EqualTo(150), "no se dispara lo que no hay");
            Assert.That(s.HasAmmo, Is.False);
            s.SetMen(5);
            Assert.That(s.CartridgesNeeded, Is.EqualTo(500), "la dotación es por hombre vivo");
        }

        // ------------------------------------------------------------------------------------------
        // Carro de vituallas
        // ------------------------------------------------------------------------------------------

        [Test]
        public void ElCarro_SaciaLaSedYLlenaLasCaramayolas()
        {
            var s = new SquadSupply(12, RealTime());
            Hours(s, 3f, Exertion.Marching, 1f);
            s.ConsumeCartridges(900);
            var cart = new SupplyCartModel();
            float before = cart.WaterLiters;
            float t = 0f;
            while ((s.WaterNeededLiters > 0.01f || s.CartridgesNeeded > 0) && t < 600f)
            {
                cart.Resupply(s, 0.5f);
                t += 0.5f;
            }
            Assert.That(s.DeficitPerManLiters, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(s.WaterPerManLiters, Is.EqualTo(s.Settings.CanteenLiters).Within(1e-4f));
            Assert.That(s.Cartridges, Is.EqualTo(1200));
            // 12 hombres × (1 L de caramayola + ~2,6 L de déficit) ≈ 43 L, a 1 L/s.
            Assert.That(before - cart.WaterLiters, Is.EqualTo(12f * (1f + (3f * 1.2f - 1f))).Within(1f));
            Assert.That(t, Is.LessThan(60f), "en menos de un minuto");
            Assert.That(cart.Cartridges, Is.EqualTo(SupplyCartModel.CartridgeCapacity - 900));
        }

        [Test]
        public void ElCarroVacio_VuelveAlDeposito()
        {
            var cart = new SupplyCartModel();
            var thirsty = new List<SquadSupply>();
            for (int i = 0; i < 30; i++)
            {
                var s = new SquadSupply(12, RealTime());
                Hours(s, 3f, Exertion.Fighting, 1f);
                thirsty.Add(s);
            }
            foreach (SquadSupply s in thirsty)
            {
                for (int k = 0; k < 120; k++) cart.Resupply(s, 0.5f);
            }
            Assert.That(cart.NeedsReload, Is.True);
            Assert.That(SupplyDispatcher.Choose(cart, Vec3.Zero, new[] { new SupplyRequest(1, Vec3.Zero, 0f, 0f, 5f) }), Is.EqualTo(SupplyDispatcher.GoToDepot));
            for (int k = 0; k < 600 && cart.NeedsReload; k++) cart.Reload(0.5f);
            Assert.That(cart.NeedsReload, Is.False);
        }

        [Test]
        public void Despacho_LaMasNecesitadaYNoDemasiadoLejos()
        {
            var cart = new SupplyCartModel();
            var requests = new List<SupplyRequest>
            {
                new SupplyRequest(1, new Vec3(0f, 0f, 50f), 0.9f, 0.9f, 0f),    // casi llena: no hace falta
                new SupplyRequest(2, new Vec3(0f, 0f, 80f), 0.2f, 0.5f, 1f),    // sedienta y cerca
                new SupplyRequest(3, new Vec3(0f, 0f, 600f), 0.1f, 0.4f, 1.5f), // más sedienta, pero lejísimos
            };
            Assert.That(SupplyDispatcher.Choose(cart, Vec3.Zero, requests), Is.EqualTo(2));
            Assert.That(SupplyDispatcher.Choose(cart, Vec3.Zero, new[] { requests[0] }), Is.EqualTo(SupplyDispatcher.Wait), "nadie la necesita: espera");
            requests[1] = new SupplyRequest(2, new Vec3(0f, 0f, 80f), 1f, 1f, 0f);
            Assert.That(SupplyDispatcher.Choose(cart, Vec3.Zero, requests), Is.EqualTo(3), "si solo una la necesita, va aunque esté lejos");
        }

        [Test]
        public void Fuentes()
        {
            var settings = new SupplySettings();
            Assert.That(settings.Source.References.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(settings.Source.IsEstimated(nameof(SupplySettings.TimeScale)), Is.True);
        }
    }
}

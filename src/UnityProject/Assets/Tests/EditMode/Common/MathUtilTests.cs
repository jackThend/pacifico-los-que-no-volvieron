using NUnit.Framework;
using Pacifico.Core.Common;

namespace Pacifico.Tests.Common
{
    public class MathUtilTests
    {
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(180f, 180f)]
        [TestCase(-180f, 180f)]
        [TestCase(720f, 0f)]
        public void WrapAngle180_NormalizaAlRango(float input, float expected)
        {
            Assert.That(MathUtil.WrapAngle180(input), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void DeltaAngle_TomaElCaminoMasCorto()
        {
            Assert.That(MathUtil.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(MathUtil.DeltaAngle(10f, 350f), Is.EqualTo(-20f).Within(1e-4f));
        }

        [Test]
        public void MoveTowardsAngle_NoSobrepasaElObjetivo()
        {
            Assert.That(MathUtil.MoveTowardsAngle(350f, 10f, 5f), Is.EqualTo(355f).Within(1e-4f));
            Assert.That(MathUtil.MoveTowardsAngle(350f, 10f, 50f), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void SmoothDamp_LlegaAlObjetivoSinSobrepasarlo_ConCualquierPaso()
        {
            foreach (float dt in new[] { 1f / 144f, 1f / 60f, 1f / 20f })
            {
                float value = 0f, velocity = 0f, max = 0f;
                for (float t = 0f; t < 3f; t += dt)
                {
                    value = MathUtil.SmoothDamp(value, 1f, ref velocity, 0.3f, dt);
                    max = System.Math.Max(max, value);
                }
                Assert.That(value, Is.EqualTo(1f).Within(1e-3f), "dt = " + dt);
                Assert.That(max, Is.LessThanOrEqualTo(1f + 1e-5f), "dt = " + dt);
            }
        }

        [Test]
        public void Conversiones_DeUnidadesDeEpoca()
        {
            Assert.That(Units.InchesToMillimeters(4.5f), Is.EqualTo(114.3f).Within(1e-3f));
            Assert.That(Units.KnotsToMetersPerSecond(12f), Is.EqualTo(6.1733f).Within(1e-3f));
            Assert.That(Units.PoundsToKilograms(300f), Is.EqualTo(136.08f).Within(1e-2f));
        }
    }
}

using NUnit.Framework;
using Pacifico.Core.Art;

namespace Pacifico.Tests.Art
{
    /// <summary>Colocación de los modelos y texturas CC0: escala, orientación y proyección de texturas.</summary>
    public class ModelFittingTests
    {
        [Test]
        public void Escala_DaLaAlturaPedida()
        {
            Assert.That(ModelFitting.ScaleToHeight(7f, 42f), Is.EqualTo(6f).Within(1e-5f));
            Assert.That(ModelFitting.ScaleToHeight(0f, 42f), Is.EqualTo(1f), "un modelo plano no se escala");
        }

        [TestCase(0f, 1f, 1f, 0f, 90f)]
        [TestCase(1f, 0f, -1f, 0f, 180f)]
        [TestCase(0f, 1f, -1f, 0f, -90f)]
        [TestCase(0f, 1f, 0f, 1f, 0f)]
        [TestCase(1f, 1f, 0f, 1f, -45f)]
        public void Giro_LlevaUnaDireccionALaOtra(float fx, float fz, float tx, float tz, float expected)
        {
            float yaw = ModelFitting.YawToFace(fx, fz, tx, tz);
            Assert.That(yaw == -180f ? 180f : yaw, Is.EqualTo(expected).Within(1e-3f));
        }

        [Test]
        public void Giro_SinDireccionNoGira()
        {
            Assert.That(ModelFitting.YawToFace(0f, 0f, 1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Proyeccion_UsaElPlanoDeLaCaraYRepiteCadaTantosMetros()
        {
            // Cara superior: X y Z.
            ModelFitting.BoxUv(8f, 40f, -4f, 0f, 1f, 0f, 4f, out float u, out float v);
            Assert.That(u, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(v, Is.EqualTo(-1f).Within(1e-5f));
            // Cara que mira al mar (-X): Z y altura.
            ModelFitting.BoxUv(1400f, 20f, 100f, -1f, 0f, 0f, 10f, out u, out v);
            Assert.That(u, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(v, Is.EqualTo(2f).Within(1e-5f));
            // Proa (+Z): X y altura.
            ModelFitting.BoxUv(3f, 6f, 30f, 0f, 0.2f, 0.9f, 3f, out u, out v);
            Assert.That(u, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(v, Is.EqualTo(2f).Within(1e-5f));
        }
    }
}

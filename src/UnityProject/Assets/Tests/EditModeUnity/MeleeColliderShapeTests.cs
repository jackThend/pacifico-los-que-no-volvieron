using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Melee;
using Pacifico.Infantry;
using UnityEngine;

namespace Pacifico.Tests.Unity
{
    /// <summary>
    /// ROADMAP 3.4 — la forma con la que se calcula el contacto es la del colisionador del muñeco, con su escala y su
    /// giro (se ejecuta en el Test Runner de Unity).
    /// </summary>
    public class MeleeColliderShapeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void CapsulaEscalada_ComoLaDelMunecoDePisagua()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _go.transform.position = new Vector3(2f, 1.15f, 5f);
            _go.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            Assert.That(MeleeController.TryGetCapsule(_go.GetComponent<Collider>(), out Capsule c), Is.True);
            Assert.That(c.Radius, Is.EqualTo(0.2f).Within(1e-5f));
            // Altura 0,7 m: el eje va de 0,8 + r a 1,5 − r.
            Assert.That(c.A.Y, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(c.B.Y, Is.EqualTo(1.3f).Within(1e-4f));
            Assert.That(c.A.X, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void EsferaYCajaGirada()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.transform.position = new Vector3(0f, 1.68f, 0f);
            _go.transform.localScale = Vector3.one * 0.22f;
            Assert.That(MeleeController.TryGetCapsule(_go.GetComponent<Collider>(), out Capsule sphere), Is.True);
            Assert.That(sphere.Radius, Is.EqualTo(0.11f).Within(1e-5f));
            Assert.That((sphere.A - new Vec3(0f, 1.68f, 0f)).Magnitude, Is.LessThan(1e-5f));
            Object.DestroyImmediate(_go);

            // Tablón de 2 m tumbado y girado 90°: la cápsula sigue su eje mayor (ahora el Z del mundo).
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _go.transform.localScale = new Vector3(2f, 0.2f, 0.3f);
            _go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.That(MeleeController.TryGetCapsule(_go.GetComponent<Collider>(), out Capsule plank), Is.True);
            Assert.That(plank.Radius, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(System.Math.Abs(plank.B.Z - plank.A.Z), Is.EqualTo(1.8f).Within(1e-4f));
            Assert.That(System.Math.Abs(plank.B.X - plank.A.X), Is.LessThan(1e-4f));
        }
    }
}

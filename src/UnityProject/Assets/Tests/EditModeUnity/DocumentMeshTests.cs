using NUnit.Framework;
using Pacifico.Core.Narrative;
using Pacifico.Narrative;
using UnityEngine;

namespace Pacifico.Tests.Unity
{
    /// <summary>ROADMAP 5.1 — malla del documento del visor (se ejecuta en el Test Runner de Unity).</summary>
    public class DocumentMeshTests
    {
        [Test]
        public void LaMalla_TieneLasMedidasDelDocumento()
        {
            DocumentShape shape = DocumentShape.For(DocumentForm.Letter, 0.75f);
            Mesh mesh = DocumentMeshBuilder.Build(shape);
            try
            {
                Assert.That(mesh.subMeshCount, Is.EqualTo(2), "anverso y reverso");
                Assert.That(mesh.bounds.size.x, Is.EqualTo(shape.WidthM).Within(1e-4f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(shape.HeightM).Within(1e-4f));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(shape.BendM + shape.ThicknessM).Within(1e-4f), "curvatura más grosor");
                // El anverso mira a la cámara del visor (−Z).
                Vector3[] normals = mesh.normals;
                int[] front = mesh.GetTriangles(0);
                Assert.That(normals[front[0]].z, Is.LessThan(-0.9f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}

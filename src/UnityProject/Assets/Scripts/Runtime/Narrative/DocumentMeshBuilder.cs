using System.Collections.Generic;
using Pacifico.Core.Narrative;
using UnityEngine;

namespace Pacifico.Narrative
{
    /// <summary>
    /// Malla de un documento (ROADMAP 5.1): pliego curvado con anverso (facsímil), reverso y cantos, generada al vuelo
    /// con las medidas de <see cref="DocumentShape"/>. El anverso mira a −Z (hacia la cámara del visor).
    /// Submalla 0: anverso; submalla 1: reverso y cantos.
    /// </summary>
    public static class DocumentMeshBuilder
    {
        public static Mesh Build(DocumentShape shape, int columns = 24, int rows = 32)
        {
            float w = shape.WidthM, h = shape.HeightM, t = Mathf.Max(0.0002f, shape.ThicknessM);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var front = new List<int>();
            var back = new List<int>();

            // El papel se curva a lo ancho: los bordes laterales se levantan hacia la cámara (z negativa).
            float Curl(float u) => -shape.BendM * (2f * u - 1f) * (2f * u - 1f);

            int stride = columns + 1;
            // Anverso (z = −t/2 + curvatura).
            for (int j = 0; j <= rows; j++)
            {
                for (int i = 0; i <= columns; i++)
                {
                    float u = i / (float)columns, v = j / (float)rows;
                    vertices.Add(new Vector3((u - 0.5f) * w, (v - 0.5f) * h, -t * 0.5f + Curl(u)));
                    uvs.Add(new Vector2(u, v));
                }
            }
            int backStart = vertices.Count;
            // Reverso: la imagen del reverso se lee sin espejo al voltear el documento sobre su eje vertical.
            for (int j = 0; j <= rows; j++)
            {
                for (int i = 0; i <= columns; i++)
                {
                    float u = i / (float)columns, v = j / (float)rows;
                    vertices.Add(new Vector3((u - 0.5f) * w, (v - 0.5f) * h, t * 0.5f + Curl(u)));
                    uvs.Add(new Vector2(1f - u, v));
                }
            }
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    // Anverso hacia −Z: sentido horario visto desde −Z (convención de Unity).
                    front.Add(a); front.Add(c); front.Add(b);
                    front.Add(b); front.Add(c); front.Add(d);
                    back.Add(backStart + a); back.Add(backStart + b); back.Add(backStart + c);
                    back.Add(backStart + b); back.Add(backStart + d); back.Add(backStart + c);
                }
            }

            // Cantos (se notan en las placas de daguerrotipo y en la cartulina de las fotografías).
            AddEdge(vertices, uvs, back, backStart, columns, 0, 1);                // inferior
            AddEdge(vertices, uvs, back, backStart, columns, rows * stride, 1);    // superior
            AddEdge(vertices, uvs, back, backStart, rows, 0, stride);              // izquierdo
            AddEdge(vertices, uvs, back, backStart, rows, columns, stride);        // derecho

            var mesh = new Mesh { name = "Documento" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(back, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Tira de cuadriláteros entre el borde del anverso y el del reverso, con las dos caras: así se ve el canto se
        /// mire desde donde se mire (son unos pocos triángulos).
        /// </summary>
        private static void AddEdge(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int backStart, int count, int first, int step)
        {
            int start = vertices.Count;
            for (int k = 0; k <= count; k++)
            {
                int index = first + k * step;
                vertices.Add(vertices[index]);
                vertices.Add(vertices[backStart + index]);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
            }
            for (int k = 0; k < count; k++)
            {
                int a = start + k * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(d);
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(c); triangles.Add(d); triangles.Add(b);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Pacifico.Core.Common;

namespace Pacifico.Core.Narrative
{
    /// <summary>Soporte físico del documento: decide su tamaño, grosor y cómo se curva.</summary>
    public enum DocumentForm
    {
        /// <summary>Carta manuscrita: pliego de ~27 cm de alto, papel fino ligeramente curvado.</summary>
        Letter = 0,
        /// <summary>Daguerrotipo o ambrotipo en su estuche: placa rígida de «sexto de placa» (7 × 8,3 cm).</summary>
        Daguerreotype = 1,
        /// <summary>Plano o mapa militar: pliego grande, casi plano.</summary>
        Map = 2,
        /// <summary>Fotografía de estudio (carte de visite, 6,4 × 10,5 cm) montada en cartulina.</summary>
        Photograph = 3,
    }

    /// <summary>Medidas del objeto en metros.</summary>
    public struct DocumentShape
    {
        public float WidthM;
        public float HeightM;
        public float ThicknessM;
        /// <summary>Flecha de la curvatura del papel en el centro (0 para placas rígidas).</summary>
        public float BendM;

        /// <summary>
        /// Medidas de un documento de la forma dada con la proporción <paramref name="aspect"/> (ancho/alto) de su
        /// facsímil. Las placas y fotografías tienen tamaño histórico fijo y se ajustan a la proporción de la imagen.
        /// </summary>
        public static DocumentShape For(DocumentForm form, float aspect)
        {
            aspect = aspect > 0.05f && aspect < 20f ? aspect : 21f / 27f;
            switch (form)
            {
                case DocumentForm.Daguerreotype:
                    return Fit(0.083f, aspect, 0.012f, 0f);
                case DocumentForm.Photograph:
                    return Fit(0.105f, aspect, 0.001f, 0.001f);
                case DocumentForm.Map:
                    return Fit(0.6f, aspect, 0.0003f, 0.004f);
                default:
                    return Fit(0.27f, aspect, 0.0002f, 0.006f);
            }
        }

        /// <summary>El lado mayor mide <paramref name="longSide"/>.</summary>
        private static DocumentShape Fit(float longSide, float aspect, float thickness, float bend)
        {
            float w = aspect >= 1f ? longSide : longSide * aspect;
            float h = aspect >= 1f ? longSide / aspect : longSide;
            return new DocumentShape { WidthM = w, HeightM = h, ThicknessM = thickness, BendM = bend };
        }
    }

    /// <summary>
    /// Estado del visor 3D de documentos (ROADMAP 5.1), sin dependencias del motor. El documento se gira arrastrando
    /// (y se voltea para ver el reverso), se acerca hacia el punto bajo el cursor sin que la vista se salga nunca del
    /// papel, se desplaza y alterna con su transcripción. Todas las transiciones se suavizan con un muelle
    /// críticamente amortiguado, igual a cualquier tasa de fotogramas.
    /// <para>
    /// Coordenadas: el zoom 1 encaja el documento entero en la vista; el desplazamiento (<see cref="PanX"/>,
    /// <see cref="PanY"/>) es el punto del documento en el centro de la vista, en fracciones de su tamaño (−0,5 a 0,5).
    /// </para>
    /// </summary>
    public sealed class DocumentViewerModel
    {
        public const float MinZoom = 1f;
        public const float MaxZoom = 6f;
        public const float DegreesPerPixel = 0.35f;
        public const float MaxPitchDeg = 65f;
        /// <summary>Tiempo de suavizado del giro, el zoom y el desplazamiento.</summary>
        public const float SmoothSeconds = 0.12f;
        public const float TranscriptionFadeSeconds = 0.18f;

        private float _yawVelocity, _pitchVelocity, _zoomVelocity, _panXVelocity, _panYVelocity, _transcriptionVelocity;

        public DocumentViewerModel(DocumentShape shape)
        {
            Shape = shape;
            Reset(immediate: true);
        }

        public DocumentShape Shape { get; }

        // Objetivos (lo que pide el jugador) y valores suavizados (lo que se dibuja).
        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; }
        public float TargetZoom { get; private set; }
        public float TargetPanX { get; private set; }
        public float TargetPanY { get; private set; }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Zoom { get; private set; }
        public float PanX { get; private set; }
        public float PanY { get; private set; }

        public bool ShowTranscription { get; private set; }
        /// <summary>0: solo el facsímil; 1: transcripción plenamente visible.</summary>
        public float TranscriptionBlend { get; private set; }

        /// <summary>Se ve el reverso cuando el documento ha girado más de 90° sobre su eje vertical.</summary>
        public bool ShowingReverse => Math.Abs(MathUtil.WrapAngle180(Yaw)) > 90f;

        // ------------------------------------------------------------------------------------------
        // Mandos
        // ------------------------------------------------------------------------------------------

        /// <summary>Arrastre con el ratón: gira el documento (horizontal: sobre su eje vertical; vertical: lo inclina).</summary>
        public void Rotate(float dxPixels, float dyPixels)
        {
            TargetYaw += dxPixels * DegreesPerPixel;
            TargetPitch = MathUtil.Clamp(TargetPitch - dyPixels * DegreesPerPixel, -MaxPitchDeg, MaxPitchDeg);
        }

        /// <summary>Da la vuelta al documento (anverso ↔ reverso), por el camino más corto.</summary>
        public void Flip()
        {
            float nearest = (float)Math.Round(TargetYaw / 180f) * 180f;
            TargetYaw = nearest + 180f;
            TargetPitch = 0f;
        }

        /// <summary>
        /// Acerca o aleja manteniendo fijo el punto del documento bajo el cursor. <paramref name="cursorX"/> y
        /// <paramref name="cursorY"/> son la posición del cursor en la vista, de −0,5 a 0,5.
        /// </summary>
        public void ZoomAt(float steps, float cursorX, float cursorY)
        {
            float before = TargetZoom;
            float after = MathUtil.Clamp(before * (float)Math.Pow(1.2, steps), MinZoom, MaxZoom);
            if (Math.Abs(after - before) < 1e-6f) return;
            // Punto del documento bajo el cursor: pan + cursor / zoom. Se conserva al cambiar el zoom.
            float docX = TargetPanX + cursorX / before, docY = TargetPanY + cursorY / before;
            TargetZoom = after;
            TargetPanX = docX - cursorX / after;
            TargetPanY = docY - cursorY / after;
            ClampPan();
        }

        /// <summary>Desplaza la vista (fracciones de la vista, como el arrastre con el botón derecho).</summary>
        public void Pan(float dxView, float dyView)
        {
            TargetPanX -= dxView / TargetZoom;
            TargetPanY -= dyView / TargetZoom;
            ClampPan();
        }

        public void ToggleTranscription() => ShowTranscription = !ShowTranscription;

        public void Reset(bool immediate = false)
        {
            TargetYaw = TargetPitch = TargetPanX = TargetPanY = 0f;
            TargetZoom = 1f;
            if (!immediate) return;
            Yaw = Pitch = PanX = PanY = 0f;
            Zoom = 1f;
            _yawVelocity = _pitchVelocity = _zoomVelocity = _panXVelocity = _panYVelocity = 0f;
        }

        /// <summary>
        /// Con zoom z se ve 1/z del documento: el centro de la vista puede ir hasta ±(0,5 − 0,5/z) sin que asome el
        /// fondo. Con zoom 1, el documento queda centrado.
        /// </summary>
        public static float PanLimit(float zoom) => Math.Max(0f, 0.5f - 0.5f / Math.Max(1f, zoom));

        private void ClampPan()
        {
            float limit = PanLimit(TargetZoom);
            TargetPanX = MathUtil.Clamp(TargetPanX, -limit, limit);
            TargetPanY = MathUtil.Clamp(TargetPanY, -limit, limit);
        }

        // ------------------------------------------------------------------------------------------

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            Yaw = MathUtil.SmoothDamp(Yaw, TargetYaw, ref _yawVelocity, SmoothSeconds, dt);
            Pitch = MathUtil.SmoothDamp(Pitch, TargetPitch, ref _pitchVelocity, SmoothSeconds, dt);
            Zoom = MathUtil.SmoothDamp(Zoom, TargetZoom, ref _zoomVelocity, SmoothSeconds, dt);
            PanX = MathUtil.SmoothDamp(PanX, TargetPanX, ref _panXVelocity, SmoothSeconds, dt);
            PanY = MathUtil.SmoothDamp(PanY, TargetPanY, ref _panYVelocity, SmoothSeconds, dt);
            // Aun en pleno suavizado, la vista no se sale del documento (el límite depende del zoom actual).
            float limit = PanLimit(Zoom);
            PanX = MathUtil.Clamp(PanX, -limit, limit);
            PanY = MathUtil.Clamp(PanY, -limit, limit);
            TranscriptionBlend = MathUtil.SmoothDamp(TranscriptionBlend, ShowTranscription ? 1f : 0f, ref _transcriptionVelocity, TranscriptionFadeSeconds, dt);
        }
    }

    /// <summary>
    /// Maquetación de la transcripción conmutable (ROADMAP 5.1): ajuste de líneas por palabras respetando los
    /// párrafos y paginación, para mostrarla junto al facsímil sin salirse del panel.
    /// </summary>
    public static class TranscriptionLayout
    {
        /// <summary>Parte el texto en líneas de como mucho <paramref name="maxChars"/> caracteres. Entre párrafos queda una línea en blanco.</summary>
        public static List<string> Wrap(string text, int maxChars)
        {
            if (maxChars < 8) throw new ArgumentOutOfRangeException(nameof(maxChars));
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            string[] paragraphs = text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.None);
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) lines.Add(string.Empty);
                // Los saltos de línea internos (firmas, encabezados) se conservan como líneas propias.
                foreach (string raw in paragraphs[p].Split('\n'))
                {
                    WrapLine(raw.Trim(), maxChars, lines);
                }
            }
            return lines;
        }

        private static void WrapLine(string line, int maxChars, List<string> lines)
        {
            if (line.Length == 0)
            {
                lines.Add(string.Empty);
                return;
            }
            var current = new StringBuilder();
            foreach (string word in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = word;
                // Una palabra más larga que la línea (improbable en castellano) se corta.
                while (w.Length > maxChars)
                {
                    if (current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                    }
                    lines.Add(w.Substring(0, maxChars));
                    w = w.Substring(maxChars);
                }
                if (current.Length == 0) current.Append(w);
                else if (current.Length + 1 + w.Length <= maxChars) current.Append(' ').Append(w);
                else
                {
                    lines.Add(current.ToString());
                    current.Clear().Append(w);
                }
            }
            if (current.Length > 0) lines.Add(current.ToString());
        }

        /// <summary>Agrupa las líneas en páginas, sin empezar una página con una línea en blanco.</summary>
        public static List<List<string>> Paginate(IReadOnlyList<string> lines, int linesPerPage)
        {
            if (linesPerPage < 1) throw new ArgumentOutOfRangeException(nameof(linesPerPage));
            var pages = new List<List<string>>();
            var page = new List<string>();
            foreach (string line in lines)
            {
                if (page.Count == 0 && line.Length == 0) continue;
                page.Add(line);
                if (page.Count == linesPerPage)
                {
                    pages.Add(page);
                    page = new List<string>();
                }
            }
            if (page.Count > 0) pages.Add(page);
            return pages;
        }

        /// <summary>Texto completo de un coleccionable para la transcripción: encabezado, cuerpo y firma.</summary>
        public static string FullText(CollectibleRecord record)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(record.DateLabel)) sb.Append(record.DateLabel).Append("\n\n");
            if (!string.IsNullOrEmpty(record.Attribution)) sb.Append(record.Attribution).Append("\n\n");
            sb.Append(record.Body);
            if (!string.IsNullOrEmpty(record.Signature)) sb.Append("\n\n").Append(record.Signature);
            return sb.ToString();
        }
    }
}

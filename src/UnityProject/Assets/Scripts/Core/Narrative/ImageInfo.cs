using System;

namespace Pacifico.Core.Narrative
{
    /// <summary>
    /// Dimensiones de una imagen JPEG o PNG leyendo solo su cabecera, sin decodificarla ni depender del motor. El visor
    /// de documentos las usa para dar al facsímil su proporción real (una carta no es un cuadrado).
    /// </summary>
    public static class ImageInfo
    {
        /// <summary>Devuelve false si el formato no es JPEG ni PNG o la cabecera está dañada.</summary>
        public static bool TryReadSize(byte[] data, out int width, out int height)
        {
            width = height = 0;
            if (data == null || data.Length < 24) return false;

            // PNG: firma de 8 bytes y el bloque IHDR con ancho y alto en big-endian.
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                width = BigEndian32(data, 16);
                height = BigEndian32(data, 20);
                return width > 0 && height > 0;
            }

            // JPEG: se recorren los segmentos hasta un SOF (C0–CF salvo C4, C8 y CC).
            if (data[0] != 0xFF || data[1] != 0xD8) return false;
            int i = 2;
            while (i + 9 < data.Length)
            {
                if (data[i] != 0xFF)
                {
                    i++;
                    continue;
                }
                byte marker = data[i + 1];
                if (marker == 0xFF)
                {
                    i++; // relleno
                    continue;
                }
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2; // marcadores sin longitud
                    continue;
                }
                int length = (data[i + 2] << 8) | data[i + 3];
                if (length < 2) return false;
                bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof)
                {
                    height = (data[i + 5] << 8) | data[i + 6];
                    width = (data[i + 7] << 8) | data[i + 8];
                    return width > 0 && height > 0;
                }
                if (marker == 0xDA) return false; // empieza la imagen comprimida sin haber encontrado el SOF
                i += 2 + length;
            }
            return false;
        }

        private static int BigEndian32(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

        /// <summary>
        /// Proporción ancho/alto teniendo en cuenta que una foto de cámara puede venir girada por EXIF. Aquí solo se
        /// lee la geometría almacenada; si no se conoce, se devuelve la de una carta (21 × 27 cm).
        /// </summary>
        public static float AspectOrLetter(byte[] data)
        {
            return TryReadSize(data, out int w, out int h) ? w / (float)h : 21f / 27f;
        }

        public static bool TryReadSize(string path, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;
            // Basta con los primeros 256 KB: las cabeceras EXIF de las cámaras caben de sobra.
            using (var stream = System.IO.File.OpenRead(path))
            {
                var buffer = new byte[Math.Min(stream.Length, 256 * 1024)];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read < buffer.Length) Array.Resize(ref buffer, read);
                return TryReadSize(buffer, out width, out height);
            }
        }
    }
}

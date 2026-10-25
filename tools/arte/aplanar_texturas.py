"""Genera la version «aplanada» del color de las texturas de suelo de Poly Haven.

Las fotos de suelo tienen zonas algo mas claras u oscuras dentro de cada repeticion; vistas de lejos, esas
variaciones se repiten como una cuadricula o en franjas. Aqui se les quita la variacion de baja frecuencia (filtro
gaussiano periodico por FFT, la textura es repetible) conservando el detalle fino y el color medio. ArtLibrary usa
<id>_diffplano_2k.jpg si existe.

Uso: python tools/arte/aplanar_texturas.py <id> [<id> ...]   (desde la raiz del repositorio; necesita numpy y matplotlib)
"""
import os
import sys

import matplotlib.image as mpimg
import numpy as np

ROOT = os.path.join("src", "UnityProject", "Assets", "Art", "CC0", "PolyHaven")

for texture_id in sys.argv[1:]:
    source = mpimg.imread(os.path.join(ROOT, texture_id, texture_id + "_diff_2k.jpg")).astype(np.float32) / 255.0
    h, w, _ = source.shape
    sigma = w / 10.0
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    gauss = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    flat = np.empty_like(source[..., :3])
    for c in range(3):
        low = np.real(np.fft.ifft2(np.fft.fft2(source[..., c]) * gauss))
        flat[..., c] = source[..., c] - low + source[..., c].mean()
    out = os.path.join(ROOT, texture_id, texture_id + "_diffplano_2k.jpg")
    mpimg.imsave(out, np.clip(flat, 0.0, 1.0), pil_kwargs={"quality": 92})
    print(out)

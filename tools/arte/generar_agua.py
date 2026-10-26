"""Genera las texturas del mar: normales de oleaje y ruido de espuma, ambas repetibles sin costuras.

El oleaje sale de un espectro de Phillips (el modelo clasico de olas generadas por el viento) transformado con FFT:
la superficie resultante es periodica, asi que la textura se repite sin cortes. La espuma es ruido filtrado por FFT.
Las texturas son obra propia del proyecto (sin licencia externa).

Uso: python tools/arte/generar_agua.py   (desde la raiz del repositorio; necesita numpy y matplotlib)
"""
import os

import matplotlib.image as mpimg
import numpy as np

OUT = os.path.join("src", "UnityProject", "Assets", "Art", "Generado", "Agua")
N = 1024
rng = np.random.default_rng(1879)


def ocean_height(size_m, wind_mps, wind_dir, seed_noise):
    g = 9.81
    k = 2.0 * np.pi * np.fft.fftfreq(N, d=size_m / N)
    kx, ky = np.meshgrid(k, k)
    kk = np.sqrt(kx ** 2 + ky ** 2)
    kk[0, 0] = 1e-6
    L = wind_mps ** 2 / g
    w = np.array([np.cos(wind_dir), np.sin(wind_dir)])
    cos_term = (kx * w[0] + ky * w[1]) / kk
    phillips = np.exp(-1.0 / (kk * L) ** 2) / kk ** 4 * np.abs(cos_term) ** 1.5
    phillips *= np.exp(-(kk * 0.02) ** 2)  # sin ondas mas pequenas que unos centimetros
    phillips[0, 0] = 0.0
    spectrum = seed_noise * np.sqrt(phillips / 2.0)
    return np.real(np.fft.ifft2(spectrum))


def normal_map(height, strength):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    n = np.dstack([-dx * strength, -dy * strength, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


os.makedirs(OUT, exist_ok=True)
noise = rng.normal(size=(N, N)) + 1j * rng.normal(size=(N, N))
height = ocean_height(size_m=40.0, wind_mps=9.0, wind_dir=0.35, seed_noise=noise)
height /= np.abs(height).max()
nmap = normal_map(height, strength=N / 12.0)
mpimg.imsave(os.path.join(OUT, "mar_normales.png"), np.clip(nmap, 0, 1))

# Espuma: ruido blanco filtrado en dos escalas, normalizado a 0-1 (gris).
f = np.fft.fftfreq(N)
fx, fy = np.meshgrid(f, f)
r = np.sqrt(fx ** 2 + fy ** 2)
white = np.fft.fft2(rng.normal(size=(N, N)))
foam = np.real(np.fft.ifft2(white * (np.exp(-(r / 0.02) ** 2) + 0.5 * np.exp(-(r / 0.08) ** 2))))
foam = (foam - foam.min()) / (foam.max() - foam.min())
mpimg.imsave(os.path.join(OUT, "mar_espuma.png"), np.dstack([foam] * 3))
print("Texturas del mar en", OUT)

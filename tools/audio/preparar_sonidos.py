"""Prepara los sonidos del juego a partir de las grabaciones originales de Sonniss (GameAudioGDC 2015-2019).

    python tools/audio/preparar_sonidos.py <carpeta con los .wav originales> <src/UnityProject/Assets/Resources/Sonido>

Para cada grabación de la tabla RECETAS:
  * «golpes»: corta los golpes sueltos de una toma con varios (un disparo, un cañonazo) por la envolvente: cada uno
    desde su arranque hasta que se apaga, con un fundido al final;
  * «entero»: el archivo completo recortado de silencios;
  * «bucle»: un tramo de N segundos cuyo final se funde con el principio, para repetirse sin corte.
Todo pasa a 44,1 kHz, mono (golpes y efectos, que suenan en 3D) o estéreo (ambientes), normalizado y en OGG Vorbis.
Las tomas «AmbiX»/«FuMa» (ambisónicas) usan solo el canal W (omnidireccional), en mono.
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

RATE = 44100

# (archivo que empieza por…, grupo, modo, parámetros)
RECETAS = [
    # Fusiles: tomas reales con varios disparos; se recortan disparos sueltos.
    ('gun_rifle_sniper_shot_01', 'Fusil', 'golpes', dict(max_s=2.2, n=2)),
    ('EFX EXT .22 Long Rifle Shots 01 A', 'Fusil', 'golpes', dict(max_s=1.6, n=3, gain_db=4)),
    ('EFX EXT .50 Cal Pistol Shots 02 A', 'Fusil', 'golpes', dict(max_s=2.0, n=3)),
    ('gun_revolver_pistol_shot_04', 'Fusil', 'golpes', dict(max_s=1.8, n=1)),
    ('Enfield_L85_rifle', 'Fusil', 'golpes', dict(max_s=1.2, n=3)),
    ('gun_semi_auto_rifle_cock_02', 'Cerrojo', 'entero', dict(max_s=1.5)),
    ('Latchlocker', 'Cerrojo', 'entero', dict(max_s=1.0)),
    # Silbido y chasquido de balas.
    ('bullet_flyby_fast_05', 'Silbido', 'entero', dict(max_s=1.5)),
    ('bullet_flyby_fast_06', 'Silbido', 'entero', dict(max_s=1.5)),
    ('warfare_t3_mg_whizzes', 'Silbido', 'golpes', dict(max_s=1.2, n=4, thresh_db=-14)),
    # Cañones.
    ('warfare_t1b_cannon_firing_forest_distant', 'Canon', 'golpes', dict(max_s=6.0, n=3)),
    ('Howitzer,M101,C3,105 mm,Distant,Right Side,Shot,Explode', 'Canon', 'entero', dict(max_s=6.0)),
    ('Howitzer,M101,C1,105 mm,Distant', 'CanonNaval', 'entero', dict(max_s=7.0)),
    ('Howitzer,M101,C3,105 mm,Medium Distant', 'CanonNaval', 'entero', dict(max_s=7.0)),
    ('Distant Blast 05', 'CanonNaval', 'entero', dict(max_s=7.0)),
    # Explosiones.
    ('EFX EXT Mortar Explosion 08', 'Explosion', 'entero', dict(max_s=6.0)),
    ('explosion_large_08', 'Explosion', 'entero', dict(max_s=6.0)),
    ('explosion_large_no_tail_03', 'Explosion', 'entero', dict(max_s=4.0)),
    ('explosion_med_long_tail_01', 'Explosion', 'entero', dict(max_s=6.0)),
    ('explosion_large_07', 'Explosion', 'entero', dict(max_s=6.0)),
    ('Explosion 8', 'Explosion', 'entero', dict(max_s=6.0)),
    ('explosion_far_distant_02', 'Explosion', 'entero', dict(max_s=6.0)),
    ('Distant Blast 17', 'Explosion', 'entero', dict(max_s=6.0)),
    ('Distant Blast 26', 'Explosion', 'entero', dict(max_s=6.0)),
    ('Distant Blast 38', 'Explosion', 'entero', dict(max_s=6.0)),
    # Piques de las granadas en el agua.
    ('Glacial Lake - Large Size Rock Splashes', 'Pique', 'golpes', dict(max_s=3.0, n=3)),
    ('underwater_splash_large_06', 'Pique', 'entero', dict(max_s=3.5)),
    ('Splash_Dive_Fienup_002', 'Pique', 'entero', dict(max_s=3.0)),
    ('SFX Large Wave Splash on Rocks 21', 'Pique', 'entero', dict(max_s=4.0)),
    # Impactos.
    ('bullet_impact_metal_heavy_08', 'ImpactoBlindaje', 'entero', dict(max_s=3.0)),
    ('metal, heavy creaks, shipwreck, ship scraping', 'ImpactoBlindaje', 'golpes', dict(max_s=3.0, n=2)),
    ('bullet_impact_concrete_brick_01', 'ImpactoBala', 'entero', dict(max_s=1.2)),
    ('Bullet Impact 22', 'ImpactoBala', 'entero', dict(max_s=1.2)),
    ('mud_splat_heavy_03', 'ImpactoBala', 'entero', dict(max_s=1.0)),
    ('impact_gritty_grit_sandy_dirt', 'ImpactoBala', 'entero', dict(max_s=1.0)),
    ('bullet_impact_body_thump_02', 'ImpactoCuerpo', 'entero', dict(max_s=1.0)),
    ('EFX SD GORE Punch 02 B', 'ImpactoCuerpo', 'entero', dict(max_s=1.0)),
    # Bayoneta.
    ('Melee_Sword_Attack_04', 'Bayoneta', 'entero', dict(max_s=1.5)),
    ('Melee_Deep_Wound_05', 'Bayoneta', 'entero', dict(max_s=1.5)),
    ('gore - large metal blade wet stab slash and remove from flesh - hard - 1', 'Bayoneta', 'entero', dict(max_s=1.5)),
    ('gore - large metal blade wet stab slash and remove from flesh - hard - 4', 'Bayoneta', 'entero', dict(max_s=1.5)),
    # Voces de la batalla.
    ('EFX EXT GROUP Agony Scream 02 A', 'Grito', 'golpes', dict(max_s=3.5, n=4, thresh_db=-16, min_gap_s=1.0)),
    ('EFX EXT GROUP Battle End Agony Moans 02 A', 'Grito', 'golpes', dict(max_s=4.0, n=2, thresh_db=-16, min_gap_s=1.5)),
    ('EFX EXT GROUP Battle Cry 03 A', 'GritoCarga', 'entero', dict(max_s=12.0)),
    ('EFX EXT GROUP Battle Approach 01 A', 'GritoCarga', 'entero', dict(max_s=12.0)),
    # Pasos y correaje.
    ('PM_SDNG_Single_Step_Footstep_46', 'Paso', 'entero', dict(max_s=0.6)),
    ('PM_SDNG_Single_Step_Footstep_19', 'Paso', 'entero', dict(max_s=0.6)),
    ('FS Ground Civilian Walk N03', 'Paso', 'entero', dict(max_s=0.6)),
    ('FS Ground Civilian Walk N05', 'Paso', 'entero', dict(max_s=0.6)),
    ('S23_SFX_Footsteps_Gravel_Loafers', 'Paso', 'golpes', dict(max_s=0.5, n=4, thresh_db=-14, min_gap_s=0.3)),
    ('foley_soldier_gear_equipment', 'Correaje', 'entero', dict(max_s=1.5)),
    # Caballos.
    ('G4F SFX06 - HORSES - Galloping outside in sand', 'Galope', 'bucle', dict(seconds=8.0, mono=True)),
    ('G4F SFX06 - HORSES - Snort 03', 'Caballo', 'entero', dict(max_s=2.0)),
    ('G4F SFX06 - HORSES - Whinny distant 01', 'Caballo', 'entero', dict(max_s=3.0)),
    # Buques: agua contra el casco, máquina de vapor, crujidos.
    ('W 04 Water_19_Splashing, Sailing, Ship', 'Estela', 'bucle', dict(seconds=20.0, mono=True)),
    ('Boat,Bow Wash,Ferry', 'Estela', 'bucle', dict(seconds=14.0, mono=True)),
    ('Train,Steam,4-6-4,Royal Hudson', 'Maquina', 'bucle', dict(seconds=12.0, mono=True)),
    ('Steam Blast,Tube or Pipe,Distant', 'Vapor', 'entero', dict(max_s=4.0)),
    ('BOAT_042-PU011_Fast_Pulley', 'Crujido', 'golpes', dict(max_s=3.0, n=3, thresh_db=-16, min_gap_s=1.0)),
    # Ambientes (estéreo, en bucle).
    ('209300 - Distant Ocean Waves', 'Amb_Mar', 'bucle', dict(seconds=50.0)),
    ('SNW_45_Sea Waves_Middle Distance', 'Amb_Mar', 'bucle', dict(seconds=50.0)),
    ('Ocean Waves Surf On Rocks Close', 'Amb_Olas', 'bucle', dict(seconds=50.0)),
    ('Coast 2014 Coast Mono 1', 'Amb_Olas', 'bucle', dict(seconds=40.0)),
    ('205300 - Crazy Harbor Seagulls', 'Amb_Gaviotas', 'bucle', dict(seconds=45.0)),
    ('DA 03 Dock Ambience_09_ Boat, Berth, Creak, Port', 'Amb_Puerto', 'bucle', dict(seconds=45.0)),
    ('wind_desert_01_60_loop', 'Amb_Desierto', 'bucle', dict(seconds=55.0)),
    ('Desert Soundscape - Wind', 'Amb_Desierto', 'bucle', dict(seconds=55.0)),
    ('Desert canyon quiet, light birds and wind', 'Amb_Viento', 'bucle', dict(seconds=50.0)),
    ('Wind,Ext,Breeze,Grass,Tall,Rustle', 'Amb_Pasto', 'bucle', dict(seconds=50.0)),
    ('Wind,Ext,Breeze,Reeds,Rustle', 'Amb_Canas', 'bucle', dict(seconds=50.0)),
    ('Crickets quiet lime hill 1', 'Amb_Grillos', 'bucle', dict(seconds=40.0)),
    ('EFX EXT GROUP Battle Steady Fighting 03 A', 'Amb_Batalla', 'bucle', dict(seconds=45.0)),
]


def load(path):
    data, rate = sf.read(path, always_2d=True, dtype='float64')
    name = os.path.basename(path)
    if 'AmbiX' in name or 'FuMa' in name or data.shape[1] > 2:
        data = data[:, :1]  # W
    if rate != RATE:
        g = np.gcd(rate, RATE)
        data = resample_poly(data, RATE // g, rate // g, axis=0)
    return data


def to_channels(x, mono):
    if mono:
        return x.mean(axis=1, keepdims=True)
    if x.shape[1] == 1:
        return np.repeat(x, 2, axis=1)
    return x[:, :2]


def envelope_db(x, win=256):
    e = np.sqrt(np.convolve((x ** 2).mean(axis=1), np.ones(win) / win, mode='same') + 1e-12)
    return 20 * np.log10(e / (e.max() + 1e-12) + 1e-12)


def fade(x, fin_s=0.003, fout_s=0.25):
    n = len(x)
    a = min(n, int(fin_s * RATE))
    b = min(n, int(fout_s * RATE))
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)[:, None]
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)[:, None] ** 2
    return x


def normalize(x, peak_db=-1.0, gain_db=0.0):
    p = np.abs(x).max()
    if p <= 0:
        return x
    return x * (10 ** (peak_db / 20) / p)


def trim(x, floor_db=-50):
    env = envelope_db(x)
    idx = np.where(env > floor_db)[0]
    if len(idx) == 0:
        return x
    start = max(0, idx[0] - int(0.005 * RATE))
    return x[start:idx[-1] + 1]


def hits(x, max_s, n, thresh_db=-20, min_gap_s=0.25):
    env = envelope_db(x)
    above = env > thresh_db
    onsets = np.where(above & ~np.roll(above, 1))[0]
    out, last = [], -10 ** 9
    for o in onsets:
        if o - last < min_gap_s * RATE:
            continue
        last = o
        start = max(0, o - int(0.01 * RATE))
        seg = x[start:start + int(max_s * RATE)].copy()
        # Cortar donde empieza el siguiente golpe, para que no se cuele.
        nxt = [p for p in onsets if p > o + min_gap_s * RATE]
        if nxt and nxt[0] - start < len(seg):
            seg = seg[:nxt[0] - start - int(0.01 * RATE)]
        if len(seg) < int(0.08 * RATE):
            continue
        out.append(seg)
    # Los más fuertes primero.
    out.sort(key=lambda s: -np.abs(s).max())
    return out[:n]


def loop(x, seconds, xfade_s=3.0):
    need = int((seconds + xfade_s) * RATE)
    if len(x) < need:
        seconds = max(2.0, len(x) / RATE - xfade_s)
        need = int((seconds + xfade_s) * RATE)
    # Tramo de en medio (los extremos de las tomas suelen tener manipulación del micro).
    start = max(0, (len(x) - need) // 2)
    seg = x[start:start + need].copy()
    xf = int(xfade_s * RATE)
    body = seg[:len(seg) - xf].copy()
    tail = seg[len(seg) - xf:]
    t = np.linspace(0, 1, xf)[:, None]
    body[:xf] = body[:xf] * np.sqrt(t) + tail * np.sqrt(1 - t)
    return body


def main():
    src, dest = sys.argv[1], sys.argv[2]
    files = os.listdir(src)
    counts = {}
    for prefix, group, mode, p in RECETAS:
        match = [f for f in files if f.startswith(prefix)]
        if not match:
            print('FALTA', prefix)
            continue
        x = load(os.path.join(src, match[0]))
        mono = p.get('mono', mode != 'bucle')
        x = to_channels(x, mono)
        if mode == 'golpes':
            pieces = [fade(s, fout_s=min(0.4, len(s) / RATE / 3)) for s in
                      hits(x, p['max_s'], p['n'], p.get('thresh_db', -20), p.get('min_gap_s', 0.25))]
        elif mode == 'entero':
            s = trim(x)[:int(p['max_s'] * RATE)].copy()
            pieces = [fade(s, fout_s=min(0.4, len(s) / RATE / 3))]
        else:
            pieces = [loop(x, p['seconds'])]
        os.makedirs(os.path.join(dest, group), exist_ok=True)
        for s in pieces:
            s = normalize(s, -1.0 if mode != 'bucle' else -3.0)
            counts[group] = counts.get(group, 0) + 1
            out = os.path.join(dest, group, '{}_{:02d}.ogg'.format(group, counts[group]))
            # Por bloques: libsndfile desborda la pila si se le da un archivo Vorbis largo de una vez.
            data = s.astype(np.float32)
            with sf.SoundFile(out, 'w', RATE, data.shape[1], format='OGG', subtype='VORBIS') as f:
                for i in range(0, len(data), 8192):
                    f.write(data[i:i + 8192])
        print('{:<16} {:<8} {} -> {} clip(s)'.format(group, mode, match[0][:60], len(pieces)))
    total = 0
    for root, _, fs in os.walk(dest):
        total += sum(os.path.getsize(os.path.join(root, f)) for f in fs if f.endswith('.ogg'))
    print('Total: {:.1f} MB'.format(total / 2 ** 20))


main()

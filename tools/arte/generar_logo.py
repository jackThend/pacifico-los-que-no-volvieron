"""Genera el logo, el icono del juego y las imagenes del instalador.

Emblema: medallon de hierro con anillo de laton, un ancla (la guerra en el mar) cruzada con dos fusiles (la guerra
en tierra). Sin banderas ni estrellas: el juego cuenta la guerra desde ambos bandos. Letras: Engravers MT (titulo,
grabado de imprenta del siglo XIX) y Georgia cursiva (subtitulo), ambas fuentes de Windows; la imagen resultante se
dibuja aqui y no lleva la fuente dentro.

Salidas:
  src/UnityProject/Assets/Art/Logo/Icono.png        1024x1024, icono del juego en Unity (PlayerSettings)
  src/UnityProject/Assets/Art/Logo/Logo.png         logo horizontal sobre papel
  tools/instalador/recursos/Pacifico.ico            icono de Windows (16 a 256 px) para el instalador y accesos
  tools/instalador/recursos/Asistente_*.bmp         franja lateral del asistente de instalacion (100 % a 200 %)
  tools/instalador/recursos/Asistente_chico_*.bmp   imagen pequena de la cabecera del asistente

Uso: python tools/arte/generar_logo.py   (desde la raiz del repositorio; necesita Pillow y numpy)
"""
import math
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

FONTS = "C:/Windows/Fonts/"
TITLE_FONT = FONTS + "ENGR.TTF"
SUB_FONT = FONTS + "georgiai.ttf"
SMALL_FONT = FONTS + "georgia.ttf"

UNITY_LOGO = os.path.join("src", "UnityProject", "Assets", "Art", "Logo")
SETUP = os.path.join("tools", "instalador", "recursos")

IRON = (31, 38, 44)
IRON_LIGHT = (58, 68, 76)
BRASS_DARK = (112, 80, 38)
BRASS = (186, 146, 82)
BRASS_LIGHT = (236, 206, 140)
INK = (43, 33, 24)
PAPER = (222, 204, 168)

rng = np.random.default_rng(1879)


def noise(size, strength, blur=0.0):
    """Ruido gris centrado en 128 para dar grano de metal o de papel."""
    n = rng.normal(128, strength, (size[1], size[0])).clip(0, 255).astype(np.uint8)
    img = Image.fromarray(n, "L")
    return img.filter(ImageFilter.GaussianBlur(blur)) if blur else img


def grain(img, strength, blur=0.0):
    """Mezcla ruido en modo luz suave conservando el canal alfa."""
    rgb = img.convert("RGB")
    n = noise(img.size, strength, blur).convert("RGB")
    out = ImageChops.soft_light(rgb, n)
    if img.mode == "RGBA":
        out.putalpha(img.getchannel("A"))
    return out


def gradient(size, top, bottom, angle=0.0):
    """Degradado lineal entre dos colores (angulo en grados, 0 = de arriba abajo)."""
    w, h = size
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    a = math.radians(angle)
    t = ((x - w / 2) * math.sin(a) + (y - h / 2) * math.cos(a)) / (0.5 * (abs(w * math.sin(a)) + abs(h * math.cos(a))) + 1e-6)
    t = ((t + 1) / 2).clip(0, 1)[..., None]
    c = np.array(top, np.float32) * (1 - t) + np.array(bottom, np.float32) * t
    return Image.fromarray(c.astype(np.uint8), "RGB")


def dilate(mask, radius):
    """Engrosa una mascara (difuminado y umbral: mucho mas rapido que MaxFilter con radios grandes)."""
    return mask.filter(ImageFilter.GaussianBlur(max(1.0, radius / 2))).point(lambda v: 255 if v > 24 else 0)


def brass_fill(mask, outline=0.012):
    """Pinta una forma (mascara L) como laton con brillo diagonal y un filo oscuro alrededor."""
    size = mask.size
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    edge = dilate(mask, size[0] * outline / 2)
    out.paste(Image.new("RGBA", size, BRASS_DARK + (255,)), (0, 0), edge)
    metal = gradient(size, BRASS_LIGHT, BRASS_DARK, angle=35)
    metal = ImageChops.multiply(metal, Image.new("RGB", size, (255, 255, 255)))
    metal = grain(metal, 18, 0.6)
    # Biselado: la parte superior izquierda de cada trazo brilla un poco mas.
    shade = ImageChops.subtract(mask, mask.transform(size, Image.AFFINE, (1, 0, -size[0] * 0.004, 0, 1, -size[0] * 0.004)))
    light = Image.new("RGB", size, BRASS_LIGHT)
    metal.paste(light, (0, 0), shade.filter(ImageFilter.GaussianBlur(size[0] * 0.002)))
    out.paste(metal, (0, 0), mask)
    return out


def rotated_polygon(points, center, angle):
    a = math.radians(angle)
    cx, cy = center
    return [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in points]


def rifle_mask(size, angle):
    """Fusil de un solo tiro visto de lado: canon, cajon de mecanismo y culata. Coordenadas en fraccion del lado."""
    s = size[0]
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    c = (s / 2, s / 2)
    L = s * 0.39
    t = s * 0.0105
    # Boca del canon hacia -x, culata hacia +x; la parte de abajo del arma es +y.
    parts = [
        [(-L, -t), (L * 0.22, -t), (L * 0.22, t), (-L, t)],                                   # canon
        [(-L * 0.98, -t * 2.6), (-L * 0.94, -t * 2.6), (-L * 0.94, -t), (-L * 0.98, -t)],     # mira
        [(-L * 0.66, t * 0.4), (L * 0.14, t * 0.4), (L * 0.14, t * 2.6), (-L * 0.62, t * 2.0)],  # caja delantera
        [(L * 0.08, -t * 1.9), (L * 0.34, -t * 1.9), (L * 0.36, t * 2.6), (L * 0.08, t * 2.6)],  # mecanismo
        [(L * 0.26, -t * 1.9), (L * 0.30, -t * 3.6), (L * 0.34, -t * 3.4), (L * 0.33, -t * 1.9)],  # martillo
        [(L * 0.34, -t * 1.3), (L * 0.56, -t * 0.6), (L * 0.56, t * 2.4), (L * 0.36, t * 2.6)],  # garganta
        [(L * 0.56, -t * 0.6), (L * 1.0, -t * 1.4), (L * 1.0, t * 5.4), (L * 0.56, t * 2.4)],    # culata
    ]
    for poly in parts:
        d.polygon(rotated_polygon(poly, c, angle), fill=255)
    for band in (-0.52, -0.20):
        x = L * band
        d.polygon(rotated_polygon([(x, -t * 1.4), (x + t * 1.3, -t * 1.4), (x + t * 1.3, t * 2.7), (x, t * 2.7)], c, angle), fill=255)
    # Guardamonte: un arco pequeno bajo el mecanismo
    gx, gy = rotated_polygon([(L * 0.27, t * 3.6)], c, angle)[0]
    r = t * 1.6
    d.ellipse((gx - r, gy - r, gx + r, gy + r), outline=255, width=max(2, int(t * 0.55)))
    return m


def anchor_mask(size):
    s = size[0]
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    cx = s / 2
    w = s * 0.055
    top, bottom = s * 0.25, s * 0.74
    d.rounded_rectangle((cx - w / 2, top, cx + w / 2, bottom), radius=w * 0.3, fill=255)
    # Argolla
    r_out, r_in = s * 0.072, s * 0.040
    ry = top - r_out * 0.55
    d.ellipse((cx - r_out, ry - r_out, cx + r_out, ry + r_out), fill=255)
    d.ellipse((cx - r_in, ry - r_in, cx + r_in, ry + r_in), fill=0)
    # Cepo
    sy, sw, sh = s * 0.335, s * 0.20, s * 0.038
    d.rounded_rectangle((cx - sw, sy - sh / 2, cx + sw, sy + sh / 2), radius=sh / 2, fill=255)
    for side in (-1, 1):
        bx = cx + side * sw
        d.ellipse((bx - sh * 0.75, sy - sh * 0.75, bx + sh * 0.75, sy + sh * 0.75), fill=255)
    # Brazos (arco) y unas
    R = s * 0.215
    acy = bottom - R * 0.98
    aw = s * 0.05
    box = (cx - R, acy - R, cx + R, acy + R)
    d.arc(box, start=18, end=162, fill=255, width=int(aw))
    for ang in (18, 162):
        a = math.radians(ang)
        px, py = cx + (R - aw / 2) * math.cos(a), acy + (R - aw / 2) * math.sin(a)
        out = 1 if ang < 90 else -1
        fluke = [
            (px - out * aw * 0.9, py + aw * 0.6),
            (px + out * aw * 1.3, py + aw * 0.2),
            (px + out * aw * 0.55, py - aw * 2.6),
        ]
        d.polygon(fluke, fill=255)
    # Corona (punta inferior)
    d.polygon([(cx - w * 1.1, bottom - w * 0.4), (cx + w * 1.1, bottom - w * 0.4), (cx, bottom + w * 1.1)], fill=255)
    return m


_cache = {}


def medallion(size=2048, rim_text=True):
    """Medallon completo con fondo transparente (se dibuja una vez por variante)."""
    key = (size, rim_text)
    if key not in _cache:
        _cache[key] = draw_medallion(size, rim_text)
    return _cache[key].copy()


def draw_medallion(size, rim_text):
    s = size
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    disc = Image.new("L", (s, s), 0)
    ImageDraw.Draw(disc).ellipse((s * 0.02, s * 0.02, s * 0.98, s * 0.98), fill=255)
    # Anillo de laton
    img = Image.alpha_composite(img, brass_fill(disc, outline=0.004))
    # Campo de hierro
    inner = Image.new("L", (s, s), 0)
    ImageDraw.Draw(inner).ellipse((s * 0.085, s * 0.085, s * 0.915, s * 0.915), fill=255)
    iron = grain(gradient((s, s), IRON_LIGHT, IRON, angle=20), 14, 0.8)
    vignette = Image.new("L", (s, s), 0)
    ImageDraw.Draw(vignette).ellipse((s * 0.2, s * 0.15, s * 0.8, s * 0.75), fill=90)
    iron = Image.composite(ImageChops.screen(iron, Image.new("RGB", (s, s), (40, 44, 48))), iron, vignette.filter(ImageFilter.GaussianBlur(s * 0.12)))
    img.paste(iron, (0, 0), inner)
    # Filete interior
    line = Image.new("L", (s, s), 0)
    ImageDraw.Draw(line).ellipse((s * 0.11, s * 0.11, s * 0.89, s * 0.89), outline=255, width=max(2, int(s * 0.006)))
    img = Image.alpha_composite(img, brass_fill(line, outline=0.001))
    # Remaches del anillo
    rivets = Image.new("L", (s, s), 0)
    rd = ImageDraw.Draw(rivets)
    for k in range(16):
        a = 2 * math.pi * k / 16 + math.pi / 16
        rx, ry = s / 2 + s * 0.4525 * math.cos(a), s / 2 + s * 0.4525 * math.sin(a)
        r = s * 0.011
        rd.ellipse((rx - r, ry - r, rx + r, ry + r), fill=255)
    if not rim_text:
        img = Image.alpha_composite(img, brass_fill(rivets, outline=0.006))
    # Fusiles cruzados detras del ancla
    shadow_layers = []
    # Bocas hacia arriba y culatas abajo; el segundo fusil es el reflejo del primero.
    one = rifle_mask((s, s), 42)
    rifles = ImageChops.lighter(one, one.transpose(Image.FLIP_LEFT_RIGHT))
    anchor = anchor_mask((s, s))
    full = ImageChops.lighter(rifles, anchor)
    shadow = full.transform((s, s), Image.AFFINE, (1, 0, -s * 0.012, 0, 1, -s * 0.016)).filter(ImageFilter.GaussianBlur(s * 0.01))
    sh = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sh.paste(Image.new("RGBA", (s, s), (5, 8, 10, 200)), (0, 0), ImageChops.multiply(shadow, inner))
    img = Image.alpha_composite(img, sh)
    img = Image.alpha_composite(img, brass_fill(rifles, outline=0.008))
    # El ancla se separa de los fusiles con un borde de hierro
    gap = dilate(anchor, s * 0.008)
    gap_layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gap_layer.paste(Image.new("RGBA", (s, s), IRON + (255,)), (0, 0), ImageChops.multiply(gap, rifles))
    img = Image.alpha_composite(img, gap_layer)
    img = Image.alpha_composite(img, brass_fill(anchor, outline=0.008))
    if rim_text:
        img = Image.alpha_composite(img, rim_lettering(s))
    return img


def rim_lettering(s):
    """Letras grabadas sobre el anillo: PACIFICO arriba, 1879 · 1884 abajo."""
    layer = Image.new("L", (s, s), 0)
    font = ImageFont.truetype(TITLE_FONT, int(s * 0.042))
    radius = s * 0.4525

    def write(text, center_angle, spacing, upright):
        widths = [font.getlength(ch) for ch in text]
        total = sum(widths) + spacing * (len(text) - 1)
        arc = total / radius
        a = center_angle - arc / 2 if upright else center_angle + arc / 2
        for ch, wch in zip(text, widths):
            step = (wch + spacing) / radius
            mid = a + (wch / 2) / radius * (1 if upright else -1)
            glyph = Image.new("L", (int(s * 0.08), int(s * 0.08)), 0)
            gd = ImageDraw.Draw(glyph)
            gd.text((glyph.width / 2, glyph.height / 2), ch, font=font, fill=255, anchor="mm")
            rot = -math.degrees(mid) - 90 if upright else -math.degrees(mid) + 90
            glyph = glyph.rotate(rot, resample=Image.BICUBIC)
            gx = s / 2 + radius * math.cos(mid) - glyph.width / 2
            gy = s / 2 + radius * math.sin(mid) - glyph.height / 2
            layer.paste(255, (int(gx), int(gy)), glyph)
            a += step if upright else -step

    write("PACÍFICO", -math.pi / 2, s * 0.012, True)
    # Engravers MT no trae el punto medio: las fechas van a cada lado de un rombo dibujado.
    half = (sum(font.getlength(ch) for ch in "1879") + s * 0.008 * 3) / 2
    offset = (half + s * 0.055) / radius
    write("1879", math.pi / 2 + offset, s * 0.008, False)
    write("1884", math.pi / 2 - offset, s * 0.008, False)
    dots = ImageDraw.Draw(layer)
    for ang in (math.radians(180), 0, math.pi / 2):
        x, y = s / 2 + radius * math.cos(ang), s / 2 + radius * math.sin(ang)
        r = s * 0.012
        dots.polygon([(x - r, y), (x, y - r * 1.4), (x + r, y), (x, y + r * 1.4)], fill=255)
    out = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (s, s), (46, 32, 14, 235)), (0, 0), layer)
    return out


def paper(size):
    w, h = size
    base = grain(gradient(size, (232, 216, 182), (206, 186, 146), angle=10), 22, 1.2)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((x - w / 2) / (w / 2)) ** 2 + ((y - h / 2) / (h / 2)) ** 2)
    v = (1 - 0.35 * np.clip(d - 0.55, 0, 1) ** 1.5)[..., None]
    return Image.fromarray((np.asarray(base, np.float32) * v).clip(0, 255).astype(np.uint8), "RGB")


def rule(draw, cx, y, half, width, color):
    draw.line((cx - half, y, cx - width * 6, y), fill=color, width=width)
    draw.line((cx + width * 6, y, cx + half, y), fill=color, width=width)
    r = width * 3.5
    draw.polygon([(cx - r, y), (cx, y - r), (cx + r, y), (cx, y + r)], fill=color)


def horizontal_logo(scale=2):
    w, h = 1600 * scale, 560 * scale
    img = paper((w, h)).convert("RGBA")
    med = medallion(2048, rim_text=False).resize((440 * scale, 440 * scale), Image.LANCZOS)
    img.alpha_composite(med, (60 * scale, 60 * scale))
    d = ImageDraw.Draw(img)
    cx = 560 * scale + (w - 560 * scale) / 2 - 20 * scale
    title = ImageFont.truetype(TITLE_FONT, 150 * scale)
    d.text((cx, 230 * scale), "PACÍFICO", font=title, fill=INK, anchor="ms")
    rule(d, cx, 285 * scale, 430 * scale, 3 * scale, INK)
    sub = ImageFont.truetype(SUB_FONT, 76 * scale)
    d.text((cx, 380 * scale), "Los que no volvieron", font=sub, fill=INK, anchor="ms")
    small = ImageFont.truetype(SMALL_FONT, 34 * scale)
    d.text((cx, 455 * scale), "GUERRA DEL PACÍFICO  ·  1879 – 1884", font=small, fill=(92, 70, 48), anchor="ms")
    return img.resize((1600, 560), Image.LANCZOS).convert("RGB")


def wizard_side(width, height):
    """Franja vertical del asistente: hierro oscuro, medallon y titulo en laton."""
    k = 4
    W, H = width * k, height * k
    img = grain(gradient((W, H), (40, 46, 52), (16, 20, 24)), 16, 1.0).convert("RGBA")
    m = int(W * 0.86)
    med = medallion(2048).resize((m, m), Image.LANCZOS)
    img.alpha_composite(med, ((W - m) // 2, int(H * 0.10)))
    text = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(text)
    d.text((W / 2, H * 0.66), "PACÍFICO", font=ImageFont.truetype(TITLE_FONT, int(W * 0.15)), fill=255, anchor="ms")
    d.line((W * 0.18, H * 0.685, W * 0.82, H * 0.685), fill=255, width=max(2, k))
    d.text((W / 2, H * 0.74), "Los que no volvieron", font=ImageFont.truetype(SUB_FONT, int(W * 0.085)), fill=255, anchor="ms")
    d.text((W / 2, H * 0.93), "1879 – 1884", font=ImageFont.truetype(SMALL_FONT, int(W * 0.07)), fill=255, anchor="ms")
    img = Image.alpha_composite(img, brass_fill(text, outline=0.002))
    return img.resize((width, height), Image.LANCZOS).convert("RGB")


def wizard_small(size):
    k = 4
    S = size * k
    img = Image.new("RGBA", (S, S), (255, 255, 255, 255))
    med = medallion(2048, rim_text=False).resize((S, S), Image.LANCZOS)
    img.alpha_composite(med)
    return img.resize((size, size), Image.LANCZOS).convert("RGB")


def main():
    os.makedirs(UNITY_LOGO, exist_ok=True)
    os.makedirs(SETUP, exist_ok=True)
    big = medallion(2048)
    icon = big.resize((1024, 1024), Image.LANCZOS)
    icon.save(os.path.join(UNITY_LOGO, "Icono.png"))
    # Para tamanos pequenos el anillo sin letras se lee mejor.
    plain = medallion(2048, rim_text=False)
    sizes = [256, 128, 64, 48, 32, 24, 16]
    frames = [(icon if s >= 128 else plain).resize((s, s), Image.LANCZOS) for s in sizes]
    frames[0].save(os.path.join(SETUP, "Pacifico.ico"), sizes=[(s, s) for s in sizes], append_images=frames[1:])
    horizontal_logo().save(os.path.join(UNITY_LOGO, "Logo.png"))
    # Inno Setup elige la imagen segun la escala de pantalla: 100, 125, 150, 175 y 200 %.
    for pct in (100, 125, 150, 175, 200):
        f = pct / 100
        wizard_side(round(164 * f), round(314 * f)).save(os.path.join(SETUP, f"Asistente_{pct}.bmp"))
        wizard_small(round(55 * f)).save(os.path.join(SETUP, f"Asistente_chico_{pct}.bmp"))
    print("Logo, icono e imagenes del instalador generados.")


if __name__ == "__main__":
    main()

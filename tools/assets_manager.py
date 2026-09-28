#!/usr/bin/env python3
"""Gestor del manifiesto de assets de «Pacífico: Los que no volvieron».

Los assets pesados (>50MB, texturas 4K, paquetes de audio, builds) no se
versionan en Git: se describen en `assets_manifest.json` con su hash SHA-256,
tamaño, licencia y enlace de descarga (Google Drive o URL CC0 directa) y se
descargan bajo demanda a `assets_cache/`.

Comandos:
  status             Muestra el estado de sincronización de cada asset.
  sync [--only ID]   Descarga los assets remotos que falten o estén corruptos.
  add PATH --id ID --source {repo,cache,gdrive,url} [--url URL] [--license L]
                     Registra (o actualiza) un asset calculando hash y tamaño.
  scan-repo DIR      Registra todos los archivos de DIR como assets 'repo'.
  test               Autoprueba sin red: registra, verifica y descarga
                     (vía file://) assets temporales y reporta los hashes.

Solo usa la biblioteca estándar de Python.
"""
import argparse
import hashlib
import json
import os
import shutil
import sys
import tempfile
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_MANIFEST = os.path.join(ROOT, "assets_manifest.json")
DEFAULT_CACHE = os.path.join(ROOT, "assets_cache")
MANIFEST_VERSION = 1
SOURCES = ("repo", "cache", "gdrive", "url")
REMOTE_SOURCES = ("gdrive", "url")
CHUNK = 1024 * 1024

# Estados de sincronización
OK = "OK"
MISSING = "FALTA"
MISMATCH = "HASH_DISTINTO"
NO_HASH = "SIN_HASH"


def sha256_of(path):
    digest = hashlib.sha256()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(CHUNK), b""):
            digest.update(block)
    return digest.hexdigest()


def gdrive_download_url(file_id):
    query = urllib.parse.urlencode({"export": "download", "id": file_id, "confirm": "t"})
    return f"https://drive.google.com/uc?{query}"


class Manifest:
    def __init__(self, path, cache_dir):
        self.path = path
        self.cache_dir = cache_dir
        self.base_dir = os.path.dirname(os.path.abspath(path))
        self.data = {"version": MANIFEST_VERSION, "cache_dir": "assets_cache", "assets": []}
        if os.path.exists(path):
            with open(path, encoding="utf-8") as fh:
                self.data = json.load(fh)

    @property
    def assets(self):
        return self.data["assets"]

    def save(self):
        self.assets.sort(key=lambda a: a["id"])
        with open(self.path, "w", encoding="utf-8") as fh:
            json.dump(self.data, fh, indent=2, ensure_ascii=False)
            fh.write("\n")

    def find(self, asset_id):
        return next((a for a in self.assets if a["id"] == asset_id), None)

    def local_path(self, asset):
        """Ruta en disco: relativa a la raíz para 'repo', a la caché para el resto."""
        if asset["source"] == "repo":
            return os.path.join(self.base_dir, asset["path"])
        return os.path.join(self.cache_dir, asset["path"])

    def remote_url(self, asset):
        if asset["source"] == "gdrive":
            return gdrive_download_url(asset["gdrive_id"])
        if asset["source"] == "url":
            return asset["url"]
        return None

    def status_of(self, asset):
        path = self.local_path(asset)
        if not os.path.isfile(path):
            return MISSING, None
        if not asset.get("sha256"):
            return NO_HASH, sha256_of(path)
        actual = sha256_of(path)
        return (OK if actual == asset["sha256"] else MISMATCH), actual

    def register(self, file_path, asset_id, source, url=None, license_name=None, gdrive_id=None):
        if source not in SOURCES:
            raise ValueError(f"Fuente desconocida: {source}")
        abs_path = os.path.abspath(file_path)
        if source == "repo":
            rel = os.path.relpath(abs_path, self.base_dir)
        else:
            rel = os.path.basename(abs_path)
        entry = self.find(asset_id) or {"id": asset_id}
        entry.update({
            "path": rel.replace(os.sep, "/"),
            "source": source,
            "sha256": sha256_of(abs_path),
            "size": os.path.getsize(abs_path),
        })
        if license_name:
            entry["license"] = license_name
        if source == "url":
            if not url:
                raise ValueError("La fuente 'url' requiere --url")
            entry["url"] = url
        if source == "gdrive":
            if not gdrive_id:
                raise ValueError("La fuente 'gdrive' requiere --gdrive-id")
            entry["gdrive_id"] = gdrive_id
        if entry not in self.assets:
            self.assets.append(entry)
        return entry

    def download(self, asset):
        url = self.remote_url(asset)
        if not url:
            raise RuntimeError(f"{asset['id']}: fuente '{asset['source']}' no es descargable")
        dest = self.local_path(asset)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        fd, tmp = tempfile.mkstemp(dir=os.path.dirname(dest), suffix=".part")
        try:
            with os.fdopen(fd, "wb") as out, urllib.request.urlopen(url, timeout=60) as resp:
                shutil.copyfileobj(resp, out, CHUNK)
            actual = sha256_of(tmp)
            if asset.get("sha256") and actual != asset["sha256"]:
                raise RuntimeError(
                    f"{asset['id']}: hash descargado {actual[:12]} != esperado {asset['sha256'][:12]}"
                )
            os.replace(tmp, dest)
        finally:
            if os.path.exists(tmp):
                os.remove(tmp)
        return dest


def print_status(manifest):
    counts = {}
    for asset in manifest.assets:
        state, actual = manifest.status_of(asset)
        counts[state] = counts.get(state, 0) + 1
        shown = (actual or asset.get("sha256") or "-")[:12]
        print(f"  [{state:<13}] {asset['id']:<48} {asset['source']:<6} {asset.get('size', 0):>11} B  sha256:{shown}")
    summary = ", ".join(f"{k}={v}" for k, v in sorted(counts.items())) or "manifiesto vacío"
    print(f"Resumen: {len(manifest.assets)} assets ({summary})")
    return counts


def cmd_status(manifest, _args):
    counts = print_status(manifest)
    return 0 if set(counts) <= {OK} else 1


def cmd_sync(manifest, args):
    failures = 0
    for asset in manifest.assets:
        if args.only and asset["id"] != args.only:
            continue
        state, _ = manifest.status_of(asset)
        if state == OK:
            continue
        if asset["source"] not in REMOTE_SOURCES:
            print(f"  [OMITIDO] {asset['id']}: {state} y sin fuente remota ({asset['source']})")
            failures += 1
            continue
        try:
            manifest.download(asset)
            print(f"  [DESCARGADO] {asset['id']}")
        except Exception as exc:  # noqa: BLE001 - reportar y seguir con el resto
            print(f"  [ERROR] {exc}")
            failures += 1
    return 1 if failures else 0


def cmd_add(manifest, args):
    entry = manifest.register(args.path, args.id, args.source, args.url, args.license, args.gdrive_id)
    manifest.save()
    print(f"Registrado {entry['id']} sha256:{entry['sha256'][:12]} ({entry['size']} B)")
    return 0


def cmd_scan_repo(manifest, args):
    root = os.path.abspath(args.dir)
    added = 0
    for dirpath, _dirs, files in os.walk(root):
        for name in sorted(files):
            if name.startswith(".") or name.endswith(".md"):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, manifest.base_dir).replace(os.sep, "/")
            asset_id = os.path.splitext(rel)[0].replace("/", ".")
            manifest.register(full, asset_id, "repo", license_name=args.license)
            added += 1
    manifest.save()
    print(f"Registrados {added} archivos de {os.path.relpath(root, manifest.base_dir)}")
    return 0


def cmd_test(_manifest, _args):
    """Autoprueba aislada: no toca el manifiesto real ni requiere red."""
    work = tempfile.mkdtemp(prefix="pacifico_assets_")
    try:
        remote = os.path.join(work, "remoto")
        os.makedirs(remote)
        sample = os.path.join(remote, "textura_salitre.bin")
        with open(sample, "wb") as fh:
            fh.write(os.urandom(256 * 1024))
        local = os.path.join(work, "cartas.txt")
        with open(local, "w", encoding="utf-8") as fh:
            fh.write("Carta de Abraham Quiroz, 1879\n")

        manifest = Manifest(os.path.join(work, "assets_manifest.json"), os.path.join(work, "assets_cache"))
        manifest.register(local, "test.carta", "repo", license_name="PD")
        url_entry = manifest.register(sample, "test.textura", "url",
                                      url="file://" + urllib.request.pathname2url(sample),
                                      license_name="CC0")
        manifest.save()
        checks = []

        checks.append(("hash registrado coincide", url_entry["sha256"] == sha256_of(sample)))
        checks.append(("asset remoto falta antes de sync", manifest.status_of(url_entry)[0] == MISSING))
        print("Estado inicial:")
        print_status(manifest)

        rc = cmd_sync(manifest, argparse.Namespace(only=None))
        checks.append(("sync termina sin errores", rc == 0))
        checks.append(("asset remoto OK tras sync", manifest.status_of(url_entry)[0] == OK))

        with open(manifest.local_path(url_entry), "ab") as fh:
            fh.write(b"corrupto")
        checks.append(("detecta corrupción", manifest.status_of(url_entry)[0] == MISMATCH))
        cmd_sync(manifest, argparse.Namespace(only="test.textura"))
        checks.append(("repara corrupción", manifest.status_of(url_entry)[0] == OK))

        reloaded = Manifest(manifest.path, manifest.cache_dir)
        checks.append(("manifiesto persiste", len(reloaded.assets) == 2))
        checks.append(("URL de Google Drive", "id=ABC123" in gdrive_download_url("ABC123")))

        print("Estado final:")
        print_status(manifest)
        print("Autoprueba:")
        for label, passed in checks:
            print(f"  [{'OK' if passed else 'FALLO'}] {label}")
        return 0 if all(p for _, p in checks) else 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST)
    parser.add_argument("--cache", default=DEFAULT_CACHE)
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("status").set_defaults(func=cmd_status)

    p_sync = sub.add_parser("sync")
    p_sync.add_argument("--only")
    p_sync.set_defaults(func=cmd_sync)

    p_add = sub.add_parser("add")
    p_add.add_argument("path")
    p_add.add_argument("--id", required=True)
    p_add.add_argument("--source", required=True, choices=SOURCES)
    p_add.add_argument("--url")
    p_add.add_argument("--gdrive-id")
    p_add.add_argument("--license")
    p_add.set_defaults(func=cmd_add)

    p_scan = sub.add_parser("scan-repo")
    p_scan.add_argument("dir")
    p_scan.add_argument("--license")
    p_scan.set_defaults(func=cmd_scan_repo)

    sub.add_parser("test").set_defaults(func=cmd_test)
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    manifest = Manifest(args.manifest, args.cache)
    return args.func(manifest, args)


if __name__ == "__main__":
    sys.exit(main())

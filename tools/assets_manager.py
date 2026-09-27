#!/usr/bin/env python3
"""
Gestor de assets de «Pacífico: Los que no volvieron» (ROADMAP 0.2, AGENTS.md §4.B).

Mantiene `assets_manifest.json` en la raíz del repositorio: una ficha por asset con ruta,
tamaño, SHA-256, licencia y origen de descarga. Así los assets pesados (> 50 MB, texturas 4K,
bancos de sonido, builds) pueden vivir fuera de git —en `assets_cache/` o Google Drive— y
descargarse bajo demanda con verificación de integridad.

Comandos:
  scan      Indexa los archivos del Archivo Histórico (y los ya registrados) calculando hashes.
  status    Informa del estado de sincronización de cada asset (OK, FALTA, MODIFICADO...).
  verify    Igual que status, pero termina con código 1 si hay problemas (para CI).
  sync      Descarga los assets que faltan o no coinciden, desde su origen declarado.
  add       Registra un archivo nuevo (local, http, Google Drive o Wikimedia Commons).
  push      Sube los assets de caché a Google Drive con rclone (si está configurado).
  test      Autodiagnóstico sin red: valida el manifiesto y reporta hashes y estado.

Solo usa la biblioteca estándar de Python 3.8+.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, Iterable, List, Optional

SCHEMA_VERSION = 1
REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_MANIFEST = REPO_ROOT / "assets_manifest.json"
DEFAULT_CACHE_DIR = "assets_cache"
ARCHIVE_DIR = "Archivo_Historico"

# AGENTS.md §4.B: ningún binario individual > 50 MB en git sin LFS.
GIT_SIZE_LIMIT_BYTES = 50 * 1024 * 1024

USER_AGENT = "PacificoHistoryGame-AssetsManager/1.0 (+https://github.com/jackThend/pacifico-los-que-no-volvieron)"
CHUNK_SIZE = 1024 * 1024

LICENSE_CHECK_COMMONS = "Por verificar en la ficha de Wikimedia Commons (source.page)"

STORAGE_GIT = "git"
STORAGE_CACHE = "cache"
VALID_STORAGES = (STORAGE_GIT, STORAGE_CACHE)

SOURCE_TYPES = ("local", "http", "gdrive", "wikimedia")

# Extensiones consideradas assets (se ignoran .md, .py, etc. al escanear el archivo histórico).
ASSET_EXTENSIONS = {
    ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".exr", ".hdr",
    ".wav", ".ogg", ".mp3", ".flac",
    ".fbx", ".obj", ".glb", ".gltf", ".blend",
    ".mp4", ".webm", ".pdf", ".zip",
}

STATUS_OK = "OK"
STATUS_MISSING = "FALTA"
STATUS_MODIFIED = "MODIFICADO"
STATUS_UNHASHED = "SIN_HASH"
STATUS_OVERSIZE = "EXCEDE_50MB_EN_GIT"
PROBLEM_STATUSES = {STATUS_MISSING, STATUS_MODIFIED, STATUS_OVERSIZE}


class ManifestError(Exception):
    """Manifiesto mal formado o inconsistente."""


# ---------------------------------------------------------------------------
# Utilidades
# ---------------------------------------------------------------------------

def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(CHUNK_SIZE), b""):
            digest.update(chunk)
    return digest.hexdigest()


def human_size(num_bytes: Optional[int]) -> str:
    if num_bytes is None:
        return "-"
    size = float(num_bytes)
    for unit in ("B", "KB", "MB", "GB"):
        if size < 1024 or unit == "GB":
            return f"{size:.0f} {unit}" if unit == "B" else f"{size:.1f} {unit}"
        size /= 1024
    return f"{size:.1f} GB"


def to_posix(path: Path) -> str:
    return path.as_posix()


def looks_like_html(path: Path) -> bool:
    """Detecta respuestas HTML (páginas de error, de login o de aviso) servidas con código 200."""
    with path.open("rb") as handle:
        head = handle.read(512).lstrip().lower()
    return head.startswith(b"<!doctype html") or head.startswith(b"<html")


def gdrive_download_url(file_id: str) -> str:
    return "https://drive.google.com/uc?export=download&id=" + urllib.parse.quote(file_id)


def wikimedia_download_url(commons_title: str) -> str:
    """URL estable de descarga del original en Wikimedia Commons (redirige al archivo)."""
    name = commons_title[len("File:"):] if commons_title.startswith("File:") else commons_title
    return "https://commons.wikimedia.org/wiki/Special:FilePath/" + urllib.parse.quote(name.replace(" ", "_"))


def wikimedia_page_url(commons_title: str) -> str:
    """Ficha del archivo en Commons, donde consta su licencia y autoría."""
    title = commons_title if commons_title.startswith("File:") else "File:" + commons_title
    return "https://commons.wikimedia.org/wiki/" + urllib.parse.quote(title.replace(" ", "_"), safe=":")


def resolve_source_url(source: Dict) -> Optional[str]:
    kind = source.get("type")
    if kind == "http":
        return source.get("url")
    if kind == "gdrive":
        return gdrive_download_url(source["file_id"]) if source.get("file_id") else None
    if kind == "wikimedia":
        return wikimedia_download_url(source["title"]) if source.get("title") else None
    return None


def load_wikimedia_sources(repo_root: Path) -> Dict[str, str]:
    """Relaciona 'Archivo_Historico/<cat>/<archivo>' con su título en Commons,
    reutilizando la tabla de download_historical_archive.py (fuente única de verdad)."""
    script = repo_root / "download_historical_archive.py"
    if not script.exists():
        return {}
    spec = importlib.util.spec_from_file_location("_pacifico_archive_downloader", script)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)  # el script protege main() con __name__ == "__main__"
    mapping: Dict[str, str] = {}
    for category, items in getattr(module, "ASSETS", {}).items():
        for commons_title, local_name in items:
            mapping[f"{ARCHIVE_DIR}/{category}/{local_name}"] = commons_title
    return mapping


# ---------------------------------------------------------------------------
# Modelo del manifiesto
# ---------------------------------------------------------------------------

@dataclass
class AssetReport:
    entry: Dict
    status: str
    actual_size: Optional[int] = None
    actual_sha256: Optional[str] = None
    detail: str = ""


class Manifest:
    def __init__(self, path: Path, data: Optional[Dict] = None):
        self.path = path
        self.data = data or {
            "schema_version": SCHEMA_VERSION,
            "description": "Manifiesto de assets de «Pacífico: Los que no volvieron». Generado y mantenido por tools/assets_manager.py.",
            "cache_dir": DEFAULT_CACHE_DIR,
            "git_size_limit_bytes": GIT_SIZE_LIMIT_BYTES,
            "assets": [],
        }

    # -- carga / guardado ---------------------------------------------------
    @classmethod
    def load(cls, path: Path) -> "Manifest":
        if not path.exists():
            return cls(path)
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise ManifestError(f"{path}: JSON inválido ({exc})") from exc
        manifest = cls(path, data)
        errors = manifest.validate()
        if errors:
            raise ManifestError("Manifiesto inválido:\n  - " + "\n  - ".join(errors))
        return manifest

    def save(self) -> None:
        self.data["assets"].sort(key=lambda a: a["path"])
        text = json.dumps(self.data, ensure_ascii=False, indent=2) + "\n"
        # Escritura atómica: nunca dejar un manifiesto a medio escribir.
        fd, tmp = tempfile.mkstemp(dir=str(self.path.parent), prefix=".manifest-", suffix=".json")
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(text)
            os.replace(tmp, self.path)
        except BaseException:
            if os.path.exists(tmp):
                os.unlink(tmp)
            raise

    # -- acceso ---------------------------------------------------------------
    @property
    def assets(self) -> List[Dict]:
        return self.data.setdefault("assets", [])

    @property
    def cache_dir(self) -> str:
        return self.data.get("cache_dir", DEFAULT_CACHE_DIR)

    def find(self, rel_path: str) -> Optional[Dict]:
        return next((a for a in self.assets if a["path"] == rel_path), None)

    def upsert(self, entry: Dict) -> Dict:
        existing = self.find(entry["path"])
        if existing is None:
            self.assets.append(entry)
            return entry
        existing.update({k: v for k, v in entry.items() if v is not None})
        return existing

    # -- validación -------------------------------------------------------------
    def validate(self) -> List[str]:
        errors: List[str] = []
        if self.data.get("schema_version") != SCHEMA_VERSION:
            errors.append(f"schema_version debe ser {SCHEMA_VERSION}")
        seen_paths = set()
        seen_names = set()
        for index, asset in enumerate(self.assets):
            where = f"assets[{index}]"
            for field in ("name", "path", "storage", "source"):
                if field not in asset:
                    errors.append(f"{where}: falta el campo '{field}'")
            if "path" in asset:
                path = asset["path"]
                where = f"assets[{index}] ({path})"
                if path in seen_paths:
                    errors.append(f"{where}: ruta duplicada")
                seen_paths.add(path)
                if path.startswith("/") or ".." in Path(path).parts or "\\" in path:
                    errors.append(f"{where}: la ruta debe ser relativa al repositorio, con '/' y sin '..'")
            if "name" in asset:
                if asset["name"] in seen_names:
                    errors.append(f"{where}: nombre duplicado '{asset['name']}'")
                seen_names.add(asset["name"])
            storage = asset.get("storage")
            if storage is not None and storage not in VALID_STORAGES:
                errors.append(f"{where}: storage '{storage}' no válido {VALID_STORAGES}")
            if storage == STORAGE_CACHE and "path" in asset and not asset["path"].startswith(self.cache_dir + "/"):
                errors.append(f"{where}: los assets de caché deben vivir bajo '{self.cache_dir}/'")
            sha = asset.get("sha256")
            if sha is not None and not re.fullmatch(r"[0-9a-f]{64}", sha):
                errors.append(f"{where}: sha256 mal formado")
            size = asset.get("size_bytes")
            if size is not None and (not isinstance(size, int) or size < 0):
                errors.append(f"{where}: size_bytes debe ser un entero >= 0")
            if storage == STORAGE_GIT and isinstance(size, int) and size > self.data.get("git_size_limit_bytes", GIT_SIZE_LIMIT_BYTES):
                errors.append(f"{where}: {human_size(size)} supera el límite de git; usa storage 'cache'")
            source = asset.get("source")
            if isinstance(source, dict):
                kind = source.get("type")
                if kind not in SOURCE_TYPES:
                    errors.append(f"{where}: source.type '{kind}' no válido {SOURCE_TYPES}")
                elif kind == "http" and not source.get("url"):
                    errors.append(f"{where}: source http requiere 'url'")
                elif kind == "gdrive" and not source.get("file_id"):
                    errors.append(f"{where}: source gdrive requiere 'file_id'")
                elif kind == "wikimedia" and not source.get("title"):
                    errors.append(f"{where}: source wikimedia requiere 'title'")
                if storage == STORAGE_CACHE and kind == "local":
                    errors.append(f"{where}: un asset de caché necesita un origen descargable (http, gdrive o wikimedia)")
            elif "source" in asset:
                errors.append(f"{where}: source debe ser un objeto")
        return errors


# ---------------------------------------------------------------------------
# Operaciones
# ---------------------------------------------------------------------------

def inspect_asset(entry: Dict, repo_root: Path, git_limit: int = GIT_SIZE_LIMIT_BYTES) -> AssetReport:
    path = repo_root / entry["path"]
    if not path.is_file():
        return AssetReport(entry, STATUS_MISSING, detail="no existe en disco")
    size = path.stat().st_size
    digest = sha256_of(path)
    if entry.get("storage") == STORAGE_GIT and size > git_limit:
        return AssetReport(entry, STATUS_OVERSIZE, size, digest, "mover a assets_cache/ o Git LFS")
    expected = entry.get("sha256")
    if not expected:
        return AssetReport(entry, STATUS_UNHASHED, size, digest, "ejecuta 'scan' para registrar el hash")
    if digest != expected or (entry.get("size_bytes") is not None and size != entry["size_bytes"]):
        return AssetReport(entry, STATUS_MODIFIED, size, digest, "el contenido no coincide con el manifiesto")
    return AssetReport(entry, STATUS_OK, size, digest)


def inspect_all(manifest: Manifest, repo_root: Path) -> List[AssetReport]:
    limit = manifest.data.get("git_size_limit_bytes", GIT_SIZE_LIMIT_BYTES)
    return [inspect_asset(entry, repo_root, limit) for entry in sorted(manifest.assets, key=lambda a: a["path"])]


def iter_archive_files(repo_root: Path) -> Iterable[Path]:
    archive = repo_root / ARCHIVE_DIR
    if not archive.is_dir():
        return []
    return sorted(p for p in archive.rglob("*") if p.is_file() and p.suffix.lower() in ASSET_EXTENSIONS)


def scan(manifest: Manifest, repo_root: Path, rehash: bool = False) -> Dict[str, int]:
    """Registra los archivos del Archivo Histórico y actualiza hashes de los assets presentes."""
    counters = {"nuevos": 0, "actualizados": 0, "sin_cambios": 0}
    wikimedia = load_wikimedia_sources(repo_root)
    for file_path in iter_archive_files(repo_root):
        rel = to_posix(file_path.relative_to(repo_root))
        entry = manifest.find(rel)
        if entry is None:
            title = wikimedia.get(rel)
            source = {"type": "wikimedia", "title": title} if title else {"type": "local"}
            entry = manifest.upsert({
                "name": file_path.stem,
                "path": rel,
                "category": file_path.parent.name,
                "storage": STORAGE_GIT,
                # No se presume la licencia: algunas fotos del archivo son modernas (réplicas,
                # monumentos) y suelen estar bajo CC BY-SA. Se verifica en la ficha de Commons.
                "license": LICENSE_CHECK_COMMONS if title else "Por determinar",
                "source": source,
            })
            if title:
                source["page"] = wikimedia_page_url(title)
            counters["nuevos"] += 1
        elif entry.get("sha256") and not rehash:
            counters["sin_cambios"] += 1
            continue
        else:
            counters["actualizados"] += 1
        entry["size_bytes"] = file_path.stat().st_size
        entry["sha256"] = sha256_of(file_path)
    # Assets registrados fuera del archivo (p. ej. caché) que están presentes pero sin hash.
    for entry in manifest.assets:
        path = repo_root / entry["path"]
        if path.is_file() and not entry.get("sha256"):
            entry["size_bytes"] = path.stat().st_size
            entry["sha256"] = sha256_of(path)
            counters["actualizados"] += 1
    return counters


Downloader = Callable[[str, Path], None]


def http_download(url: str, destination: Path) -> None:
    """Descarga en streaming a un archivo temporal. Maneja el aviso de antivirus de Google Drive."""
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        content_type = response.headers.get("Content-Type", "")
        if "drive.google.com" in url and content_type.startswith("text/html"):
            page = response.read().decode("utf-8", errors="replace")
            confirm_url = _gdrive_confirm_url(page, url)
            if confirm_url is None:
                raise IOError("Google Drive devolvió una página HTML sin enlace de confirmación (¿archivo privado?)")
            return http_download(confirm_url, destination)
        with destination.open("wb") as handle:
            shutil.copyfileobj(response, handle, CHUNK_SIZE)


def _gdrive_confirm_url(page: str, original_url: str) -> Optional[str]:
    """Archivos grandes de Drive muestran un formulario de confirmación; se reconstruye su URL."""
    form = re.search(r'<form[^>]+id="download-form"[^>]+action="([^"]+)"', page)
    if form:
        action = form.group(1).replace("&amp;", "&")
        fields = dict(re.findall(r'<input type="hidden" name="([^"]+)" value="([^"]*)"', page))
        return action + ("&" if "?" in action else "?") + urllib.parse.urlencode(fields)
    token = re.search(r"confirm=([0-9A-Za-z_-]+)", page)
    if token:
        return original_url + "&confirm=" + token.group(1)
    return None


def sync(manifest: Manifest, repo_root: Path, downloader: Downloader = http_download,
         dry_run: bool = False, include_git: bool = False, log: Callable[[str], None] = print) -> Dict[str, int]:
    """Descarga los assets FALTA/MODIFICADO. Por defecto solo los de caché: los de git se
    restauran con git. Cada descarga se verifica contra su SHA-256 antes de colocarse."""
    counters = {"descargados": 0, "omitidos": 0, "fallidos": 0, "al_dia": 0}
    for report in inspect_all(manifest, repo_root):
        entry = report.entry
        if report.status in (STATUS_OK, STATUS_UNHASHED, STATUS_OVERSIZE):
            counters["al_dia"] += 1
            continue
        if entry.get("storage") == STORAGE_GIT and not include_git:
            log(f"  [OMITIDO] {entry['path']}: asset versionado en git (usa 'git checkout' o --include-git)")
            counters["omitidos"] += 1
            continue
        url = resolve_source_url(entry.get("source", {}))
        if not url:
            log(f"  [OMITIDO] {entry['path']}: sin origen descargable")
            counters["omitidos"] += 1
            continue
        if dry_run:
            log(f"  [SIMULADO] {entry['path']} <- {url}")
            counters["omitidos"] += 1
            continue
        target = repo_root / entry["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        partial = target.with_name(target.name + ".part")
        try:
            downloader(url, partial)
            digest = sha256_of(partial)
            expected = entry.get("sha256")
            if expected and digest != expected:
                raise IOError(f"hash inesperado {digest[:12]}… (esperado {expected[:12]}…)")
            if not expected and looks_like_html(partial) and target.suffix.lower() not in (".html", ".htm"):
                # Sin hash de referencia, una página de error o de inicio de sesión (HTTP 200) no debe
                # aceptarse como el asset ni quedar registrada como su huella.
                raise IOError("el servidor devolvió una página HTML en lugar del archivo")
            os.replace(partial, target)
            entry["size_bytes"] = target.stat().st_size
            if not expected:
                entry["sha256"] = digest
                log(f"  [AVISO] {entry['path']}: sin hash previo; se registra el de esta descarga (primer uso)")
            log(f"  [DESCARGADO] {entry['path']} ({human_size(target.stat().st_size)})")
            counters["descargados"] += 1
        except Exception as exc:  # noqa: BLE001 — se informa y se continúa con el resto
            if partial.exists():
                partial.unlink()
            log(f"  [ERROR] {entry['path']}: {exc}")
            counters["fallidos"] += 1
    return counters


def add_asset(manifest: Manifest, repo_root: Path, rel_path: str, source: Dict, name: Optional[str] = None,
              license_name: str = "Por determinar", category: Optional[str] = None,
              storage: Optional[str] = None, description: Optional[str] = None) -> Dict:
    path = repo_root / rel_path
    rel = to_posix(Path(rel_path))
    size = path.stat().st_size if path.is_file() else None
    if storage is None:
        storage = STORAGE_CACHE if rel.startswith(manifest.cache_dir + "/") else STORAGE_GIT
    if storage == STORAGE_GIT and size is not None and size > GIT_SIZE_LIMIT_BYTES:
        raise ManifestError(f"{rel} pesa {human_size(size)}: colócalo en {manifest.cache_dir}/ (AGENTS.md §4.B)")
    entry = {
        "name": name or Path(rel).stem,
        "path": rel,
        "category": category or Path(rel).parent.name,
        "storage": storage,
        "license": license_name,
        "source": source,
    }
    if description:
        entry["description"] = description
    if size is not None:
        entry["size_bytes"] = size
        entry["sha256"] = sha256_of(path)
    manifest.upsert(entry)
    errors = manifest.validate()
    if errors:
        raise ManifestError("\n".join(errors))
    return entry


def push_to_gdrive(manifest: Manifest, repo_root: Path, remote: str, dry_run: bool = False) -> int:
    """Sube la caché local a Google Drive usando rclone (configurado con `rclone config`)."""
    rclone = shutil.which("rclone")
    if rclone is None:
        raise ManifestError("rclone no está instalado. Ver https://rclone.org/drive/ para configurarlo.")
    cache = repo_root / manifest.cache_dir
    if not cache.is_dir():
        raise ManifestError(f"No existe la caché {cache}")
    command = [rclone, "copy", str(cache), remote, "--checksum", "--progress"]
    if dry_run:
        command.append("--dry-run")
    return subprocess.call(command)


# ---------------------------------------------------------------------------
# Informe
# ---------------------------------------------------------------------------

def print_report(reports: List[AssetReport], show_hashes: bool = False, out=None) -> Dict[str, int]:
    out = out or sys.stdout  # resolver en tiempo de llamada (permite redirect_stdout)
    totals: Dict[str, int] = {}
    for report in reports:
        totals[report.status] = totals.get(report.status, 0) + 1
        line = f"  [{report.status:<10}] {report.entry['path']}  ({human_size(report.actual_size or report.entry.get('size_bytes'))})"
        if show_hashes and report.actual_sha256:
            line += f"  sha256={report.actual_sha256}"
        if report.detail and report.status != STATUS_OK:
            line += f"  — {report.detail}"
        print(line, file=out)
    total_bytes = sum(r.actual_size or 0 for r in reports)
    summary = ", ".join(f"{k}: {v}" for k, v in sorted(totals.items()))
    print(f"\nResumen: {len(reports)} assets ({human_size(total_bytes)} presentes) — {summary or 'vacío'}", file=out)
    return totals


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def build_source(args: argparse.Namespace) -> Dict:
    if args.gdrive_id:
        return {"type": "gdrive", "file_id": args.gdrive_id}
    if args.url:
        return {"type": "http", "url": args.url}
    if args.wikimedia:
        return {"type": "wikimedia", "title": args.wikimedia}
    return {"type": "local"}


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Gestor de assets y manifiesto (assets_manifest.json).")
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST, help="ruta del manifiesto")
    parser.add_argument("--root", type=Path, default=REPO_ROOT, help="raíz del repositorio")
    sub = parser.add_subparsers(dest="command", required=True)

    p_scan = sub.add_parser("scan", help="indexa el Archivo Histórico y calcula hashes")
    p_scan.add_argument("--rehash", action="store_true", help="recalcula también los hashes ya registrados")

    p_status = sub.add_parser("status", help="estado de sincronización")
    p_status.add_argument("--hashes", action="store_true", help="muestra los SHA-256")

    sub.add_parser("verify", help="como status, con código de salida 1 si hay problemas")

    p_sync = sub.add_parser("sync", help="descarga assets que faltan o no coinciden")
    p_sync.add_argument("--dry-run", action="store_true")
    p_sync.add_argument("--include-git", action="store_true", help="descarga también assets versionados en git")

    p_add = sub.add_parser("add", help="registra un asset")
    p_add.add_argument("path", help="ruta relativa al repositorio (p. ej. assets_cache/audio/canon_300lb.wav)")
    p_add.add_argument("--name")
    p_add.add_argument("--license", default="Por determinar")
    p_add.add_argument("--category")
    p_add.add_argument("--description")
    p_add.add_argument("--storage", choices=VALID_STORAGES)
    origin = p_add.add_mutually_exclusive_group()
    origin.add_argument("--gdrive-id", help="ID del archivo en Google Drive (compartido por enlace)")
    origin.add_argument("--url", help="URL HTTP(S) de descarga directa")
    origin.add_argument("--wikimedia", help="título en Commons, p. ej. 'File:Huascar.jpg'")

    p_push = sub.add_parser("push", help="sube assets_cache/ a Google Drive con rclone")
    p_push.add_argument("--remote", default=os.environ.get("PACIFICO_GDRIVE_REMOTE"),
                        help="destino rclone, p. ej. 'gdrive:Pacifico/assets_cache' (o PACIFICO_GDRIVE_REMOTE)")
    p_push.add_argument("--dry-run", action="store_true")

    sub.add_parser("test", help="autodiagnóstico sin red: valida el manifiesto y reporta hashes")

    args = parser.parse_args(argv)
    root = args.root.resolve()

    try:
        manifest = Manifest.load(args.manifest)
    except ManifestError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    if args.command == "scan":
        counters = scan(manifest, root, rehash=args.rehash)
        manifest.save()
        print(f"Manifiesto actualizado ({args.manifest.name}): " + ", ".join(f"{k}={v}" for k, v in counters.items()))
        return 0

    if args.command in ("status", "verify"):
        totals = print_report(inspect_all(manifest, root), show_hashes=getattr(args, "hashes", False))
        problems = sum(v for k, v in totals.items() if k in PROBLEM_STATUSES)
        return 1 if args.command == "verify" and problems else 0

    if args.command == "sync":
        counters = sync(manifest, root, dry_run=args.dry_run, include_git=args.include_git)
        if counters["descargados"]:
            manifest.save()
        print("Sincronización: " + ", ".join(f"{k}={v}" for k, v in counters.items()))
        return 1 if counters["fallidos"] else 0

    if args.command == "add":
        try:
            entry = add_asset(manifest, root, args.path, build_source(args), name=args.name,
                              license_name=args.license, category=args.category,
                              storage=args.storage, description=args.description)
        except (ManifestError, OSError) as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 2
        manifest.save()
        print(f"Registrado: {entry['path']} ({entry['storage']}, {entry['source']['type']})")
        return 0

    if args.command == "push":
        if not args.remote:
            print("ERROR: indica --remote o define PACIFICO_GDRIVE_REMOTE", file=sys.stderr)
            return 2
        try:
            return push_to_gdrive(manifest, root, args.remote, dry_run=args.dry_run)
        except ManifestError as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 2

    if args.command == "test":
        print(f"Manifiesto: {args.manifest} (schema v{manifest.data.get('schema_version')}) — válido")
        print(f"Caché local: {root / manifest.cache_dir}")
        reports = inspect_all(manifest, root)
        totals = print_report(reports, show_hashes=True)
        problems = sum(v for k, v in totals.items() if k in PROBLEM_STATUSES)
        pending_cache = [r for r in reports if r.entry.get("storage") == STORAGE_CACHE and r.status == STATUS_MISSING]
        if pending_cache:
            print(f"Assets de caché pendientes de 'sync': {len(pending_cache)}")
        git_problems = problems - len(pending_cache)
        print("RESULTADO: " + ("OK" if git_problems == 0 else f"{git_problems} problema(s) en assets versionados"))
        return 0 if git_problems == 0 else 1

    parser.error(f"comando desconocido {args.command}")
    return 2


if __name__ == "__main__":
    sys.exit(main())

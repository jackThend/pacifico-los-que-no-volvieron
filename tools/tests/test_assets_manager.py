"""Pruebas de tools/assets_manager.py. Ejecutar: python -m unittest discover -s tools/tests -v"""

import hashlib
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import assets_manager as am  # noqa: E402


def write(path: Path, content: bytes) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return hashlib.sha256(content).hexdigest()


class FakeRepo:
    """Repositorio mínimo en un directorio temporal, con un downloader simulado."""

    def __init__(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.manifest_path = self.root / "assets_manifest.json"
        (self.root / "download_historical_archive.py").write_text(
            'ASSETS = {"01_Barcos": [("File:Huascar.jpg", "01_Huascar.jpg")]}\n'
            'if __name__ == "__main__":\n    raise SystemExit("no debe ejecutarse")\n',
            encoding="utf-8",
        )
        self.remote_files = {}
        self.downloads = []

    def downloader(self, url, destination):
        self.downloads.append(url)
        if url not in self.remote_files:
            raise IOError("404")
        destination.write_bytes(self.remote_files[url])

    def manifest(self):
        return am.Manifest.load(self.manifest_path)

    def close(self):
        self._tmp.cleanup()


class ScanTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()
        self.addCleanup(self.repo.close)

    def test_scan_indexa_archivo_historico_con_hash_y_origen_wikimedia(self):
        digest = write(self.repo.root / "Archivo_Historico/01_Barcos/01_Huascar.jpg", b"huascar")
        write(self.repo.root / "Archivo_Historico/01_Barcos/notas.md", b"no es un asset")
        manifest = self.repo.manifest()

        counters = am.scan(manifest, self.repo.root)
        manifest.save()

        self.assertEqual(counters["nuevos"], 1)
        entry = self.repo.manifest().find("Archivo_Historico/01_Barcos/01_Huascar.jpg")
        self.assertEqual(entry["sha256"], digest)
        self.assertEqual(entry["size_bytes"], 7)
        self.assertEqual(entry["storage"], "git")
        self.assertEqual(entry["source"], {"type": "wikimedia", "title": "File:Huascar.jpg",
                                           "page": "https://commons.wikimedia.org/wiki/File:Huascar.jpg"})
        self.assertEqual(entry["license"], am.LICENSE_CHECK_COMMONS)
        self.assertIsNone(self.repo.manifest().find("Archivo_Historico/01_Barcos/notas.md"))

    def test_scan_es_idempotente(self):
        write(self.repo.root / "Archivo_Historico/01_Barcos/01_Huascar.jpg", b"huascar")
        manifest = self.repo.manifest()
        am.scan(manifest, self.repo.root)
        counters = am.scan(manifest, self.repo.root)
        self.assertEqual(counters, {"nuevos": 0, "actualizados": 0, "sin_cambios": 1})

    def test_archivo_sin_entrada_en_commons_queda_como_local(self):
        write(self.repo.root / "Archivo_Historico/02_Fotos/retrato.png", b"x")
        manifest = self.repo.manifest()
        am.scan(manifest, self.repo.root)
        self.assertEqual(manifest.find("Archivo_Historico/02_Fotos/retrato.png")["source"], {"type": "local"})


class StatusTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()
        self.addCleanup(self.repo.close)
        self.asset = self.repo.root / "Archivo_Historico/01_Barcos/01_Huascar.jpg"
        write(self.asset, b"original")
        self.manifest = self.repo.manifest()
        am.scan(self.manifest, self.repo.root)

    def status_of(self, path):
        reports = {r.entry["path"]: r for r in am.inspect_all(self.manifest, self.repo.root)}
        return reports[path].status

    def test_ok_modificado_y_falta(self):
        rel = "Archivo_Historico/01_Barcos/01_Huascar.jpg"
        self.assertEqual(self.status_of(rel), am.STATUS_OK)
        self.asset.write_bytes(b"alterado")
        self.assertEqual(self.status_of(rel), am.STATUS_MODIFIED)
        self.asset.unlink()
        self.assertEqual(self.status_of(rel), am.STATUS_MISSING)

    def test_asset_en_git_sobre_el_limite_se_marca(self):
        self.manifest.data["git_size_limit_bytes"] = 4
        self.assertEqual(self.status_of("Archivo_Historico/01_Barcos/01_Huascar.jpg"), am.STATUS_OVERSIZE)

    def test_cli_verify_devuelve_1_si_falta_un_asset(self):
        self.manifest.save()
        self.asset.unlink()
        with redirect_stdout(io.StringIO()):
            code = am.main(["--manifest", str(self.repo.manifest_path), "--root", str(self.repo.root), "verify"])
        self.assertEqual(code, 1)

    def test_cli_test_reporta_hashes(self):
        self.manifest.save()
        out = io.StringIO()
        with redirect_stdout(out):
            code = am.main(["--manifest", str(self.repo.manifest_path), "--root", str(self.repo.root), "test"])
        self.assertEqual(code, 0)
        self.assertIn("sha256=" + hashlib.sha256(b"original").hexdigest(), out.getvalue())
        self.assertIn("RESULTADO: OK", out.getvalue())


class SyncTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()
        self.addCleanup(self.repo.close)
        self.manifest = self.repo.manifest()
        self.content = b"sonido de canon Armstrong de 300 libras"
        self.url = am.gdrive_download_url("ABC123")
        self.manifest.upsert({
            "name": "canon_300lb",
            "path": "assets_cache/audio/canon_300lb.wav",
            "storage": "cache",
            "license": "CC0",
            "source": {"type": "gdrive", "file_id": "ABC123"},
            "sha256": hashlib.sha256(self.content).hexdigest(),
            "size_bytes": len(self.content),
        })

    def test_descarga_asset_de_cache_y_verifica_hash(self):
        self.repo.remote_files[self.url] = self.content
        counters = am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, log=lambda _: None)
        self.assertEqual(counters["descargados"], 1)
        self.assertEqual((self.repo.root / "assets_cache/audio/canon_300lb.wav").read_bytes(), self.content)

    def test_hash_incorrecto_no_deja_archivo_corrupto(self):
        self.repo.remote_files[self.url] = b"contenido corrupto"
        counters = am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, log=lambda _: None)
        self.assertEqual(counters["fallidos"], 1)
        target = self.repo.root / "assets_cache/audio/canon_300lb.wav"
        self.assertFalse(target.exists())
        self.assertFalse(target.with_name(target.name + ".part").exists())

    def test_sin_hash_previo_rechaza_paginas_html(self):
        entry = self.manifest.find("assets_cache/audio/canon_300lb.wav")
        del entry["sha256"]
        del entry["size_bytes"]
        self.repo.remote_files[self.url] = b"<!DOCTYPE html><html><body>Inicia sesion</body></html>"
        counters = am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, log=lambda _: None)
        self.assertEqual(counters["fallidos"], 1)
        self.assertNotIn("sha256", entry, "no se registra la huella de una respuesta basura")
        self.assertFalse((self.repo.root / "assets_cache/audio/canon_300lb.wav").exists())

    def test_sin_hash_previo_registra_la_huella_de_la_primera_descarga(self):
        entry = self.manifest.find("assets_cache/audio/canon_300lb.wav")
        del entry["sha256"]
        self.repo.remote_files[self.url] = self.content
        am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, log=lambda _: None)
        self.assertEqual(entry["sha256"], hashlib.sha256(self.content).hexdigest())

    def test_dry_run_no_descarga(self):
        self.repo.remote_files[self.url] = self.content
        am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, dry_run=True, log=lambda _: None)
        self.assertEqual(self.repo.downloads, [])

    def test_assets_de_git_no_se_descargan_por_defecto(self):
        self.manifest.upsert({
            "name": "huascar", "path": "Archivo_Historico/01_Barcos/01_Huascar.jpg", "storage": "git",
            "source": {"type": "wikimedia", "title": "File:Huascar.jpg"},
        })
        am.sync(self.manifest, self.repo.root, downloader=self.repo.downloader, log=lambda _: None)
        self.assertNotIn(am.wikimedia_download_url("File:Huascar.jpg"), self.repo.downloads)


class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()
        self.addCleanup(self.repo.close)

    def test_add_rechaza_archivo_grande_en_git(self):
        path = self.repo.root / "Assets/textura.png"
        path.parent.mkdir(parents=True)
        with path.open("wb") as handle:
            handle.truncate(am.GIT_SIZE_LIMIT_BYTES + 1)  # archivo disperso: no ocupa disco real
        with self.assertRaises(am.ManifestError):
            am.add_asset(self.repo.manifest(), self.repo.root, "Assets/textura.png", {"type": "local"})

    def test_add_en_cache_requiere_origen_descargable(self):
        write(self.repo.root / "assets_cache/tex/arena.png", b"arena")
        with self.assertRaises(am.ManifestError):
            am.add_asset(self.repo.manifest(), self.repo.root, "assets_cache/tex/arena.png", {"type": "local"})

    def test_manifiesto_invalido_se_rechaza_al_cargar(self):
        self.repo.manifest_path.write_text(json.dumps({
            "schema_version": 1,
            "assets": [
                {"name": "a", "path": "../fuera.png", "storage": "git", "source": {"type": "local"}},
                {"name": "a", "path": "x.png", "storage": "nube", "source": {"type": "ftp"}, "sha256": "zz"},
            ],
        }), encoding="utf-8")
        with self.assertRaises(am.ManifestError) as ctx:
            self.repo.manifest()
        message = str(ctx.exception)
        for fragment in ("sin '..'", "nombre duplicado", "storage 'nube'", "source.type 'ftp'", "sha256 mal formado"):
            self.assertIn(fragment, message)

    def test_confirmacion_de_google_drive(self):
        page = ('<form id="download-form" action="https://drive.usercontent.google.com/download" method="get">'
                '<input type="hidden" name="id" value="ABC"><input type="hidden" name="confirm" value="t"></form>')
        url = am._gdrive_confirm_url(page, am.gdrive_download_url("ABC"))
        self.assertEqual(url, "https://drive.usercontent.google.com/download?id=ABC&confirm=t")

    def test_url_de_wikimedia(self):
        self.assertEqual(am.wikimedia_download_url("File:Monitor Huáscar.jpg"),
                         "https://commons.wikimedia.org/wiki/Special:FilePath/Monitor_Hu%C3%A1scar.jpg")


class RepositoryManifestTests(unittest.TestCase):
    """El manifiesto real del repositorio debe ser válido y coincidir con los archivos versionados."""

    def test_manifiesto_del_repositorio(self):
        if not am.DEFAULT_MANIFEST.exists():
            self.skipTest("assets_manifest.json aún no generado")
        manifest = am.Manifest.load(am.DEFAULT_MANIFEST)
        problems = [r for r in am.inspect_all(manifest, am.REPO_ROOT)
                    if r.entry["storage"] == "git" and r.status != am.STATUS_OK]
        self.assertEqual(problems, [], "\n".join(f"{r.entry['path']}: {r.status}" for r in problems))


if __name__ == "__main__":
    unittest.main()

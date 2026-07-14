import csv
import importlib.util
import os
import pathlib
import unittest
from unittest import mock


MODULE_PATH = pathlib.Path(__file__).resolve().parents[1] / "gen_character_meshy.py"


def load_module():
    spec = importlib.util.spec_from_file_location("gen_character_meshy", MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {MODULE_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MeshyCharacterGeneratorTests(unittest.TestCase):
    def test_module_import_does_not_require_api_key(self):
        with mock.patch.dict(os.environ, {}, clear=True):
            module = load_module()

        self.assertIsNone(module.api_key())

    def test_image_data_uri_uses_file_content_type_not_extension(self):
        import tempfile

        module = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            disguised_webp = pathlib.Path(tmp) / "sheet.png"
            disguised_webp.write_bytes(b"RIFF\x04\x00\x00\x00WEBP")

            uri = module.image_data_uri(disguised_webp)

        self.assertTrue(uri.startswith("data:image/webp;base64,"))
    def test_multi_view_request_uses_meshy_6_multi_image_endpoint(self):
        import tempfile

        module = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            views = []
            for index in range(4):
                path = pathlib.Path(tmp) / f"view-{index}.png"
                path.write_bytes(b"\x89PNG\r\n\x1a\n")
                views.append(path)

            kind, payload = module.build_mesh_request(views)

        self.assertEqual(kind, "multi-image-to-3d")
        self.assertEqual(payload["ai_model"], "meshy-6")
        self.assertEqual(len(payload["image_urls"]), 4)
        self.assertNotIn("image_url", payload)

    def test_shape_only_request_skips_texture_and_remesh(self):
        import tempfile

        module = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            view = pathlib.Path(tmp) / "front.png"
            view.write_bytes(b"\x89PNG\r\n\x1a\n")

            kind, payload = module.build_mesh_request([view], textured=False)

        self.assertEqual(kind, "image-to-3d")
        self.assertFalse(payload["should_texture"])
        self.assertFalse(payload["should_remesh"])
        self.assertFalse(payload["remove_lighting"])

    def test_cli_accepts_repeated_views_and_shape_only_dry_run(self):
        module = load_module()

        args = module.parse_args([
            "char.mc",
            "--view", "front.png",
            "--view", "right.png",
            "--shape-only",
            "--dry-run",
        ])

        self.assertEqual(args.char_id, "char.mc")
        self.assertEqual(args.views, ["front.png", "right.png"])
        self.assertTrue(args.shape_only)
        self.assertTrue(args.dry_run)

    def test_legacy_single_image_cli_preserves_t_pose_default(self):
        module = load_module()

        args = module.parse_args(["char.mc", "sheet.png"])

        self.assertEqual(args.views, ["sheet.png"])
        self.assertEqual(args.pose_mode, "t-pose")
        self.assertFalse(args.shape_only)

    def test_dry_run_needs_no_api_key_and_redacts_embedded_images(self):
        import contextlib
        import io
        import tempfile

        module = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            views = []
            for name in ("front", "back"):
                path = pathlib.Path(tmp) / f"{name}.png"
                path.write_bytes(b"\x89PNG\r\n\x1a\n")
                views.append(str(path))
            args = module.parse_args([
                "char.mc", "--view", views[0], "--view", views[1],
                "--shape-only", "--dry-run",
            ])
            output = io.StringIO()
            with mock.patch.dict(os.environ, {}, clear=True), contextlib.redirect_stdout(output):
                result = module.run(args)

        rendered = output.getvalue()
        self.assertEqual(result, 0)
        self.assertIn('"endpoint": "/multi-image-to-3d"', rendered)
        self.assertIn("<embedded image 1>", rendered)
        self.assertNotIn("base64,", rendered)

    def test_script_entrypoint_runs_dry_run_cli(self):
        import subprocess
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            view = pathlib.Path(tmp) / "front.png"
            view.write_bytes(b"\x89PNG\r\n\x1a\n")
            result = subprocess.run(
                [
                    "python3", str(MODULE_PATH), "char.mc",
                    "--view", str(view), "--shape-only", "--dry-run",
                ],
                capture_output=True,
                text=True,
                env={},
                check=False,
            )

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('"endpoint": "/image-to-3d"', result.stdout)

    def test_provenance_row_matches_documented_csv_schema(self):
        import tempfile

        module = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            (root / "docs").mkdir()
            old_cwd = os.getcwd()
            os.chdir(tmp)
            try:
                module.append_provenance(
                    "char.mc",
                    [("meshy_model.fbx", "meshy multi-image-to-3d meshy-6", "task-1", 20)],
                )
            finally:
                os.chdir(old_cwd)

            with (root / "docs" / "art-provenance.csv").open(newline="") as handle:
                row = next(csv.reader(handle))

        self.assertEqual(len(row), 7)
        self.assertEqual(
            row[0:3],
            ["char.mc/meshy_model.fbx", "3d_model", "meshy multi-image-to-3d meshy-6"],
        )
        self.assertEqual(row[4], "task-1; 20 credits")
        self.assertEqual(row[5], "paid-tier commercial")
        self.assertEqual(row[6], "")


if __name__ == "__main__":
    unittest.main()

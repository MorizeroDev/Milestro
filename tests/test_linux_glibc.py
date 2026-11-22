import importlib.util
import contextlib
import io
from pathlib import Path
import subprocess
import unittest
from unittest import mock


SPEC = importlib.util.spec_from_file_location(
    "check_linux_glibc", Path(__file__).resolve().parents[1] / "scripts/check_linux_glibc.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class LinuxGlibcTests(unittest.TestCase):
    def test_accepts_ubuntu_22_04_requirements(self):
        version_info = "Name: GLIBC_2.9 Flags: none\nName: GLIBC_2.35 Flags: none"
        self.assertEqual(MODULE.check_glibc(version_info, "2.35"), "2.35")

    def test_rejects_newer_glibc(self):
        with self.assertRaisesRegex(ValueError, "Requires GLIBC_2.38"):
            MODULE.check_glibc("Name: GLIBC_2.38 Flags: none", "2.35")

    def test_ignores_glibcxx(self):
        version_info = "Name: GLIBCXX_3.4.30 Flags: none\nName: GLIBC_2.17 Flags: none"
        self.assertEqual(MODULE.check_glibc(version_info, "2.35"), "2.17")

    def test_rejects_empty_version_information(self):
        with self.assertRaisesRegex(ValueError, "No GLIBC"):
            MODULE.check_glibc("", "2.35")

    def test_rejects_private_glibc_dependency(self):
        with self.assertRaisesRegex(ValueError, "Unsupported GLIBC"):
            MODULE.check_glibc("Name: GLIBC_PRIVATE Flags: none", "2.35")

    def test_cli_reports_newer_glibc_without_failing(self):
        output = io.StringIO()
        result = subprocess.CompletedProcess([], 0, stdout="Name: GLIBC_2.38 Flags: none")
        with mock.patch("sys.argv", ["check_linux_glibc.py", "plugin.so", "--max-glibc", "2.35"]):
            with mock.patch.object(MODULE.subprocess, "run", return_value=result):
                with contextlib.redirect_stdout(output):
                    self.assertIsNone(MODULE.main())
        self.assertIn("::error::Requires GLIBC_2.38", output.getvalue())

    def test_cli_still_fails_when_readelf_fails(self):
        with mock.patch("sys.argv", ["check_linux_glibc.py", "plugin.so", "--max-glibc", "2.35"]):
            with mock.patch.object(MODULE.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "readelf")):
                with self.assertRaises(subprocess.CalledProcessError):
                    MODULE.main()


if __name__ == "__main__":
    unittest.main()

"""Regression checks for the portable ICU placeholder and embedded-data generator."""

import importlib.util
from pathlib import Path
import re
import struct
import tempfile
import unittest


GENERATOR = Path(__file__).resolve().parents[2] / "ext/icu-cmake/make_data_cpp.py"


class IcuDataCodegenTest(unittest.TestCase):
    def generate(self, export_data):
        spec = importlib.util.spec_from_file_location("make_data_cpp", GENERATOR)
        generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(generator)
        generator.export_data = export_data
        data = b"icudt77\x00" + bytes(range(24))
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.dat"
            output = Path(directory) / "output.cpp"
            source.write_bytes(data)
            generator.convert(generator.cpp, str(source), str(output))
            text = output.read_text()
        self.assertIn('extern "C" {', text)
        self.assertIn('alignas(16) uint32_t icudt77_dat[] = {', text)
        self.assertNotIn('__attribute__', text)
        self.assertTrue(text.endswith('};\n}\n'))
        initializer = text.split('= {', 1)[1].split('}', 1)[0]
        words = [int(value) for value in re.findall(r'\d+', initializer)]
        return data, words

    def test_placeholder_has_one_zero_word(self):
        _, words = self.generate(False)
        self.assertEqual(words, [0])

    def test_embedded_data_preserves_every_word_without_padding(self):
        data, words = self.generate(True)
        self.assertEqual(words, list(struct.unpack('@8I', data)))


if __name__ == "__main__":
    unittest.main()

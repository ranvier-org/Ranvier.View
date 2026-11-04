"""Hermetic checks of the nuget.org hash lookup used to stage the public GPU runtime."""
import base64
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location('public_gpu_runtime', Path(__file__).with_name('public_gpu_runtime.py'))
GPU = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GPU)

# Arbitrary sample version; nothing here tracks the current release.
SAMPLE = '9.8.7-sample.3'


class PublishedHash(unittest.TestCase):
    def serve(self, entry, inline=False):
        digest = base64.b64encode(b'\x01' * 64).decode()
        catalog = {'packageHashAlgorithm': 'SHA512', 'packageHash': digest, **entry}
        pages = {f'https://api.nuget.org/v3/registration5-gz-semver2/{GPU.PACKAGE}/{SAMPLE}.json':
                 json.dumps({'catalogEntry': catalog if inline else 'https://catalog/entry.json'}).encode(),
                 'https://catalog/entry.json': json.dumps(catalog).encode()}
        return lambda url: pages[url]

    def test_hash_is_read_from_the_catalog_entry(self):
        self.assertEqual(GPU.published_sha512(SAMPLE.upper(), self.serve({})), b'\x01' * 64)

    def test_inline_catalog_entry(self):
        self.assertEqual(GPU.published_sha512(SAMPLE, self.serve({}, inline=True)), b'\x01' * 64)

    def test_other_algorithm_is_rejected(self):
        with self.assertRaises(SystemExit):
            GPU.published_sha512(SAMPLE, self.serve({'packageHashAlgorithm': 'SHA256'}))

    def test_project_version_is_read_from_the_project(self):
        with tempfile.TemporaryDirectory() as root:
            project = Path(root) / 'x.fsproj'
            project.write_text(f'<Project><PropertyGroup><RanvierWebCoreNativeVersion Condition="\'$(RanvierWebCoreNativeVersion)\' == \'\'">{SAMPLE}</RanvierWebCoreNativeVersion></PropertyGroup></Project>')
            self.assertEqual(GPU.project_version(project), SAMPLE)


if __name__ == '__main__':
    unittest.main()

import argparse
import hashlib
import json
from pathlib import Path
import unittest
import zipfile

PACKAGE = None
UPSTREAM = None


class HostPackage(unittest.TestCase):
    def test_the_upstream_tool_payload_keeps_its_relative_paths(self):
        with zipfile.ZipFile(PACKAGE) as package, zipfile.ZipFile(UPSTREAM) as upstream:
            names = package.namelist()
            self.assertEqual(len(names), len(set(names)))
            prefix = 'tools/net10.0/any/'
            for name in upstream.namelist():
                if name.startswith(prefix) and not name.endswith('/'):
                    self.assertIn(name, names)
                    if name != prefix + 'FSharp.Compiler.Service.dll':
                        self.assertEqual(package.read(name), upstream.read(name), name)

    def test_the_compiler_patch_and_licenses_match_the_manifest(self):
        with zipfile.ZipFile(PACKAGE) as package:
            prefix = 'tools/net10.0/any/'
            manifest = json.loads(package.read(prefix + 'vendor-manifest.json'))
            compiler = package.read(prefix + 'FSharp.Compiler.Service.dll')
            patch = package.read(prefix + 'vendor-notices/compiler.patch')
            self.assertEqual(hashlib.sha256(compiler).hexdigest(), manifest['compilerSha256'])
            self.assertEqual(hashlib.sha256(patch).hexdigest(), manifest['sourceGuardSha256'])
            self.assertEqual(manifest['compilerPublicKeyToken'], '')
            for name in ['FSharp-LICENSE.txt', 'FSharp.Analyzers.SDK-LICENSE.md']:
                self.assertTrue(package.read(prefix + 'vendor-notices/' + name))
            self.assertIn('build/Ranvier.Analyzers.Host.targets', package.namelist())
            self.assertEqual(package.read('build/Ranvier.Analyzers.Host.targets'),
                             package.read('buildTransitive/Ranvier.Analyzers.Host.targets'))
            runtime = json.loads(package.read(prefix + 'Ranvier.Analyzers.Host.runtimeconfig.json'))
            self.assertEqual(runtime['runtimeOptions']['configProperties']['System.GC.HeapHardLimit'], 4 * 1024 ** 3)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--upstream-package', type=Path, required=True)
    args = parser.parse_args()
    PACKAGE = args.package
    UPSTREAM = args.upstream_package
    unittest.main(argv=['host-package'], verbosity=2)

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

HOST = None
DOTNET = None


class HostStartup(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='ranvier-analyzer-host-')
        cls.payload = Path(cls.temporary.name) / 'host'
        shutil.copytree(HOST, cls.payload)
        cls.original = (cls.payload / 'FSharp.Compiler.Service.dll').read_bytes()

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def setUp(self):
        (self.payload / 'FSharp.Compiler.Service.dll').write_bytes(self.original)
        self.directory = Path(self.temporary.name) / self._testMethodName

    def run_host(self, receipt):
        env = os.environ.copy()
        env['DOTNET_HOST_PATH'] = str(Path(shutil.which(DOTNET) or DOTNET).resolve())
        env.pop('RANVIER_ANALYZER_HOST_PROVENANCE_DIR', None)
        if receipt:
            env['RANVIER_ANALYZER_HOST_PROVENANCE_DIR'] = str(self.directory)
        return subprocess.run([DOTNET, str(self.payload / 'Ranvier.Analyzers.Host.dll'), '--help'],
                              env=env, capture_output=True, text=True, timeout=30)

    def test_records_the_loaded_compiler_without_changing_cli_stdout(self):
        result = self.run_host(True)
        self.assertEqual(result.returncode, 0, result.stderr)
        receipts = list(self.directory.glob('*.json'))
        self.assertEqual(len(receipts), 1)
        receipt = json.loads(receipts[0].read_text())
        self.assertTrue(receipt['matched'])
        self.assertEqual(Path(receipt['compilerAssemblyPath']).resolve(), (self.payload / 'FSharp.Compiler.Service.dll').resolve())
        self.assertEqual(receipt['compilerSha256'], hashlib.sha256(self.original).hexdigest())
        self.assertEqual(receipt['compilerVersion'], '43.12.402.0')
        self.assertEqual(receipt['compilerPublicKeyToken'], '')
        self.assertEqual(receipt['compilerSourceCommit'], 'a8dd0577c5c13f43a2f57fe33b4ff11b90f6371e')
        self.assertEqual(receipt['gcTotalAvailableMemoryBytes'], 4 * 1024 ** 3)
        self.assertFalse(receipt['gcIsServer'])
        self.assertNotIn('compilerSha256', result.stdout)

    def test_provenance_is_opt_in(self):
        result = self.run_host(False)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertFalse(self.directory.exists())
        self.assertNotIn('compilerSha256', result.stdout)

    def test_a_changed_loaded_compiler_fails_closed(self):
        (self.payload / 'FSharp.Compiler.Service.dll').write_bytes(self.original + b'vendor-hash-mismatch')
        result = self.run_host(True)
        self.assertEqual(result.returncode, 78, result.stdout + result.stderr)
        receipt = json.loads(next(self.directory.glob('*.json')).read_text())
        self.assertFalse(receipt['matched'])
        self.assertIn('does not match', result.stderr)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--host-directory', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    HOST = args.host_directory.resolve()
    DOTNET = args.dotnet
    unittest.main(argv=['host-startup'], verbosity=2)

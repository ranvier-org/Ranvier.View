import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
import zipfile

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import support


class PublicGraph(unittest.TestCase):
    def fixture(self, mode='normal'):
        config = json.loads((HERE / 'inputs.json').read_text())
        suffix = '.Traced' if mode == 'traced' else ''
        libraries = {'Ranvier' + suffix + '/' + config['coreVersion']: {'type': 'package'},
                     'Ranvier.Dom' + suffix + '/' + config['domVersion']: {'type': 'package'},
                     'Ranvier.WebCore/' + config['interopVersion']: {'type': 'package'},
                     'FSharp.Core/' + config['fsharpCore']: {'type': 'package'},
                     'Ranvier.View.WebCore' + suffix + '/0.1.0-alpha.46': {'type': 'project'},
                     'Ranvier.View.WebCore.Sdl' + suffix + '/0.1.0-alpha.46': {'type': 'project'}}
        return {'libraries': libraries}, config

    def test_normal_and_traced_are_separate_complete_graphs(self):
        for mode in ['normal', 'traced']:
            data, config = self.fixture(mode)
            self.assertEqual(len(support.core_graph(data, mode, config)), 3)

    def test_case_insensitive_opposing_dom_rejects(self):
        for mode in ['normal', 'traced']:
            data, config = self.fixture(mode)
            opposite = '' if mode == 'traced' else '.traced'
            data['libraries']['rAnViEr.DoM' + opposite + '/' + config['domVersion']] = {'type': 'package'}
            with self.assertRaisesRegex(ValueError, 'Mixed'):
                support.core_graph(data, mode, config)

    def test_missing_dom_and_stale_interop_reject(self):
        for stale in [False, True]:
            data, config = self.fixture()
            key = next(k for k in data['libraries'] if k.startswith('Ranvier.Dom/'))
            del data['libraries'][key]
            if stale:
                data['libraries'][key] = {'type': 'package'}
                del data['libraries']['Ranvier.WebCore/' + config['interopVersion']]
                data['libraries']['Ranvier.WebCore/0.1.1-preview.7'] = {'type': 'package'}
            with self.assertRaises(ValueError):
                support.core_graph(data, 'normal', config)

    def test_source_core_or_interop_and_packaged_view_reject(self):
        for key in ['Ranvier/0.1.1-preview.1', 'Ranvier.WebCore/0.1.1-preview.11', 'Ranvier.View.WebCore/0.1.0-alpha.46']:
            data, config = self.fixture()
            data['libraries'][key]['type'] = 'project' if not key.startswith('Ranvier.View') else 'package'
            with self.assertRaises(ValueError):
                support.core_graph(data, 'normal', config)

    def test_extra_native_package_or_wrong_fsharp_rejects(self):
        data, config = self.fixture()
        data['libraries']['Ranvier.WebCore.Native.osx-arm64/0.1.1-preview.7'] = {'type': 'package'}
        with self.assertRaisesRegex(ValueError, 'native stage'):
            support.core_graph(data, 'normal', config)

    def test_opposing_source_project_and_wrong_source_version_reject(self):
        for mode in ['normal', 'traced']:
            data, config = self.fixture(mode)
            opposite = '' if mode == 'traced' else '.tRaCeD'
            data['libraries']['rAnViEr.ViEw.WebCore' + opposite + '/' + config['viewVersion']] = {'type': 'project'}
            with self.assertRaisesRegex(ValueError, 'source provider mode/version'):
                support.core_graph(data, mode, config)
            data, config = self.fixture(mode)
            suffix = '.Traced' if mode == 'traced' else ''
            del data['libraries']['Ranvier.View.WebCore' + suffix + '/' + config['viewVersion']]
            data['libraries']['Ranvier.View.WebCore' + suffix + '/0.1.0-alpha.45'] = {'type': 'project'}
            with self.assertRaisesRegex(ValueError, 'source provider mode/version'):
                support.core_graph(data, mode, config)
        data, config = self.fixture()
        data['libraries']['FSharp.Core/10.1.401'] = {'type': 'package'}
        with self.assertRaisesRegex(ValueError, 'FSharp.Core'):
            support.core_graph(data, 'normal', config)


class Verdicts(unittest.TestCase):
    def test_actual_unfiltered_summary_and_empty_negative(self):
        inventory = {'total': 3, 'ignoredCeiling': 1, 'names': ['a', 'b', 'c'], 'allowedIgnored': ['c']}
        text = '2 passed, 1 ignored, 0 failed, 0 errored\nPassed: 2\n\ta\n\tb\nIgnored: 1\n\tc\nFailed: 0\nErrored: 0'
        self.assertEqual(support.jit_summary(text, inventory), [2, 1, 0, 0])
        for text in ['0 passed, 0 ignored, 0 failed, 0 errored', 'PASS', '4 passed, 0 ignored, 1 failed, 0 errored']:
            with self.assertRaises(ValueError):
                support.jit_summary(text, inventory)

    def test_truncated_discovery_duplicate_and_ignore_inflation_reject(self):
        inventory = {'total': 3, 'ignoredCeiling': 1, 'names': ['a', 'b', 'c'], 'allowedIgnored': ['c']}
        positive = '2 passed, 1 ignored, 0 failed, 0 errored\nPassed: 2\n\ta\n\tb\nIgnored: 1\n\tc\nFailed: 0\nErrored: 0'
        for text in [positive.replace('\tb\n', '\ta\n'), positive.replace('\tc\n', '\tunknown\n'),
                     positive.replace('\tb\n', '\ttemporary\n').replace('\tc\n', '\tb\n').replace('\ttemporary\n', '\tc\n'),
                     positive.replace('2 passed', '1 passed').replace('Passed: 2\n\ta\n\tb', 'Passed: 1\n\ta'),
                     '1 passed, 2 ignored, 0 failed, 0 errored\nPassed: 1\n\ta\nIgnored: 2\n\tb\n\tc\nFailed: 0\nErrored: 0']:
            with self.assertRaises(ValueError):
                support.jit_summary(text, inventory)

    def test_inventory_pin_is_current_exact_source_and_bounded_policy(self):
        config = json.loads((HERE / 'inputs.json').read_text())
        path = HERE / config['jitInventory']['path']
        self.assertEqual(support.text_sha(path), config['jitInventory']['sha256'])
        inventory = json.loads(path.read_text())
        self.assertEqual(inventory['sourceCommit'], config['viewCommit'])
        self.assertEqual(len(inventory['names']), 849)
        self.assertEqual(len(set(inventory['names'])), 849)
        self.assertEqual(inventory['ignoredCeiling'], 16)
        self.assertEqual(len(inventory['allowedIgnored']), 16)

    def test_strict_native_generation_rejects_warnings_and_always_throw(self):
        support.strict_publish('Generating native code\n0 Warning(s)')
        for text in ['Build succeeded', 'Generating native code\nwarning IL3050: root',
                     'Generating native code\nwill always throw because: missing code']:
            with self.assertRaises(ValueError):
                support.strict_publish(text)

    def test_source_head_and_dirty_negative_are_actual_git(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            subprocess.run(['git', 'init', '-q', root], check=True)
            subprocess.run(['git', '-C', root, '-c', 'user.name=fixture', '-c', 'user.email=fixture@invalid',
                            'commit', '--allow-empty', '-qm', 'test: create source control'], check=True)
            head = subprocess.check_output(['git', '-C', root, 'rev-parse', 'HEAD'], text=True).strip()
            self.assertEqual(support.source_identity(root, head), head)
            with self.assertRaisesRegex(ValueError, 'HEAD/clean'):
                support.source_identity(root, '0' * 40)
            (root / 'unreviewed.txt').write_text('dirty')
            with self.assertRaisesRegex(ValueError, 'HEAD/clean'):
                support.source_identity(root, head)

    def test_native_payload_cannot_escape_or_partially_extract(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = root / 'native.nupkg'
            manifest = {'abi': 63, 'rid': 'osx-arm64', 'flavor': 'slim', 'files': [
                {'name': name, 'bytes': len(name), 'sha256': hashlib.sha256(name.encode()).hexdigest()}
                for name in sorted(support.NATIVE)]}
            with zipfile.ZipFile(package, 'w') as z:
                z.writestr('manifest.json', json.dumps(manifest))
                for row in manifest['files']:
                    z.writestr('runtimes/osx-arm64/native/' + row['name'], row['name'])
            config = {'native': {'sha256': support.sha(package), 'manifest': manifest}}
            result = support.stage_native(package, root / 'valid', config)
            self.assertEqual(set(result), support.NATIVE)
            bad = copy.deepcopy(config)
            bad['native']['manifest']['files'][0]['name'] = '../../escape'
            with self.assertRaises(ValueError):
                support.stage_native(package, root / 'invalid', bad)
            self.assertFalse((root / 'invalid').exists())

    def test_frozen_common_fixtures_and_existing_feed_mapping(self):
        config = json.loads((HERE / 'inputs.json').read_text())
        self.assertTrue(all(support.text_sha(HERE / name) == digest for name, digest in config['fixtureHashes'].items()))
        text = (HERE / 'qualify.py').read_text()
        self.assertIn("shutil.copy2(view / 'nuget.config'", text)
        self.assertNotIn('--filter', text)
        workflow = (HERE.parents[1] / '.github/workflows/mac-public-view46.yml').read_text()
        self.assertNotIn('publish-packages', workflow)
        self.assertIn('contents: read', workflow)
        self.assertIn('if: always()', workflow)
        self.assertIn('artifacts/mac-public-source', workflow)
        job_environment = workflow.split('    env:\n', 1)[1].split('    steps:', 1)[0]
        self.assertNotIn('${{ runner.', job_environment)

    def test_only_crlf_normalizes_and_other_text_mutation_rejects(self):
        with tempfile.TemporaryDirectory() as temporary:
            p = Path(temporary) / 'fixture.fs'
            p.write_bytes(b'let check = 1\n')
            expected = support.text_sha(p)
            p.write_bytes(b'let check = 1\r\n')
            self.assertEqual(support.text_sha(p), expected)
            p.write_bytes(b'let check = 2\r\n')
            self.assertNotEqual(support.text_sha(p), expected)
            p.write_bytes(b'let check = 1\r')
            with self.assertRaisesRegex(ValueError, 'bare CR'):
                support.text_sha(p)


@unittest.skipUnless(os.name == 'posix', 'Owned POSIX process-group control')
class ProcessCleanup(unittest.TestCase):
    def test_timeout_stops_spawned_descendant(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            record = {'commands': []}
            run = support.Runner(root, record, lambda: None, dict(os.environ))
            code = ('import subprocess,sys,time;p=subprocess.Popen([sys.executable,"-c","import time;time.sleep(30)"]);'
                    'print(p.pid,flush=True);time.sleep(30)')
            with self.assertRaisesRegex(RuntimeError, 'timeout'):
                run('timeout', [sys.executable, '-I', '-c', code], root, timeout=.75)
            row = record['commands'][0]
            self.assertTrue(row['reaped'])
            self.assertEqual(row['supervisorLimitTriggered'], 'timeout')
            pid = int((root / 'timeout.log').read_text())
            deadline = time.monotonic() + 2
            while True:
                status = subprocess.run(['ps', '-o', 'stat=', '-p', str(pid)], capture_output=True, text=True).stdout.strip()
                if not status or status.startswith('Z'):
                    break
                self.assertLess(time.monotonic(), deadline)
                time.sleep(.05)


if __name__ == '__main__':
    unittest.main()

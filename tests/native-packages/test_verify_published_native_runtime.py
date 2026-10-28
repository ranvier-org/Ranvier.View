"""Hermetic package-cache/output checks; no NuGet downloads or native execution."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
SCRIPT = Path(__file__).resolve().parents[2] / 'tools/verify-published-native-runtime.py'
SPEC = importlib.util.spec_from_file_location('published_native_runtime', SCRIPT)
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)

VERSION = '0.1.0-preview.6'
VIEW = 'Ranvier.View.WebCore.Sdl.Traced'
VIEW_VERSION = '0.1.0-test.1'
SLIM = 'Ranvier.WebCore.Native.win-x64'
GPU = 'Ranvier.WebCore.Native.Gpu.win-x64'
RIDS = ['linux-x64', 'linux-arm64', 'osx-arm64', 'win-arm64', 'win-x64']


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding='utf-8')


class PackageFixture:
    def __init__(self, root, flavor='slim'):
        self.root = root
        self.cache = root / 'cache'
        self.output = root / 'output'
        self.output.mkdir()
        self.assets_path = root / 'project.assets.json'
        self.flavor = flavor
        self.manifests = {}
        self.package_roots = {}
        libraries = {}
        target = {}
        base = {}
        for package in [VIEW, 'Ranvier.WebCore', 'Ranvier.WebCore.Native', *['Ranvier.WebCore.Native.' + rid for rid in RIDS]]:
            version = VIEW_VERSION if package == VIEW else VERSION
            key = package + '/' + version
            libraries[key] = {'type': 'package', 'path': key.lower()}
            target[key] = {'type': 'package'}
            base[key] = {'type': 'package'}
        target['Ranvier.WebCore.Native/' + VERSION]['dependencies'] = { 'Ranvier.WebCore.Native.' + rid: VERSION for rid in RIDS }
        base['Ranvier.WebCore.Native/' + VERSION] = copy.deepcopy(target['Ranvier.WebCore.Native/' + VERSION])
        for package in ['Ranvier.Dom.Traced/0.1.0-alpha.2', 'Fable.Core/5.3.0']:
            libraries[package] = {'type': 'package', 'path': package.lower()}
            target[package] = {'type': 'package'}
            base[package] = {'type': 'package'}
        for package, kind in [(SLIM, 'slim'), (GPU, 'gpu')]:
            if kind == 'gpu' and flavor != 'gpu':
                continue
            key = package + '/' + VERSION
            package_root = self.cache / key.lower()
            native = package_root / 'runtimes/win-x64/native'
            names = ['RanvierWebCoreHost.dll', 'WebCore.dll'] + [f'WebKit.resources/data/{i:03}.bin' for i in range(315)]
            if kind == 'gpu':
                names += ['libEGL.dll', 'libGLESv2.dll']
            records = []
            for index, name in enumerate(names):
                content = f'{kind}:{index:04}'.encode()
                path = native / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
                records.append({'name': name, 'bytes': len(content), 'sha256': hashlib.sha256(content).hexdigest()})
                if kind == flavor:
                    destination = self.output / name
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes(content)
            manifest = {'flavor': kind, 'rid': 'win-x64', 'version': VERSION, 'webkit': 'b' * 40, 'abi': 21,
                        'source': {'dirty': False, 'commit': 'a' * 40}, 'files': records, 'total_bytes': sum(record['bytes'] for record in records)}
            self.manifests[kind] = manifest
            self.package_roots[kind] = package_root
            write_json(package_root / 'manifest.json', manifest)
            write_json(package_root / '.nupkg.metadata', {'source': 'https://api.nuget.org/v3/index.json'})
            libraries[key] = {'type': 'package', 'path': key.lower()}
            active = kind == flavor
            payload = {'runtimes/win-x64/native/' + record['name']: {} for record in records}
            build = {'buildTransitive/' + package + '.targets': {}}
            target[key] = {'type': 'package', **({'native': payload, 'build': build} if active else {})}
            base[key] = {'type': 'package', **({'runtimeTargets': payload, 'build': build} if active else {})}
        self.assets = {'targets': {'net10.0': base, 'net10.0/win-x64': target}, 'libraries': libraries, 'packageFolders': {str(self.cache): {}}}

    def save(self):
        write_json(self.assets_path, self.assets)
        for kind, manifest in self.manifests.items():
            write_json(self.package_roots[kind] / 'manifest.json', manifest)

    def verify(self):
        self.save()
        return VERIFY.verify_runtime(self.assets_path, self.output, native_version=VERSION,
                                     view_package=VIEW, view_version=VIEW_VERSION, flavor=self.flavor)


class RuntimeVerification(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)

    def tearDown(self):
        self.directory.cleanup()

    def fixture(self, flavor='slim'):
        return PackageFixture(self.root, flavor)

    def test_cpu_checks_every_one_of_317_manifest_files(self):
        self.assertEqual(self.fixture().verify()['files'], 317)

    def test_gpu_suppression_and_matching_release_cohort(self):
        self.assertEqual(self.fixture('gpu').verify()['files'], 319)

    def test_last_resource_hash_is_checked_even_when_size_matches(self):
        fixture = self.fixture()
        path = fixture.output / fixture.manifests['slim']['files'][-1]['name']
        path.write_bytes(b'X' * path.stat().st_size)
        with self.assertRaisesRegex(ValueError, 'hash'):
            fixture.verify()

    def test_missing_output_resource(self):
        fixture = self.fixture()
        (fixture.output / fixture.manifests['slim']['files'][-1]['name']).unlink()
        with self.assertRaisesRegex(ValueError, 'missing'):
            fixture.verify()

    def test_manifest_identity_cleanliness_and_totals(self):
        mutations = [('rid', 'linux-x64'), ('version', '0.0.0'), ('flavor', 'gpu'), ('abi', 20),
                     ('webkit', ''), ('total_bytes', 0), ('source', {'dirty': True, 'commit': 'a' * 40}),
                     ('source', {'dirty': False, 'commit': 'private'})]
        for index, (field, value) in enumerate(mutations):
            with self.subTest(field=field, value=value):
                root = self.root / str(index)
                root.mkdir()
                fixture = PackageFixture(root)
                fixture.manifests['slim'][field] = value
                with self.assertRaises(ValueError):
                    fixture.verify()

    def test_gpu_and_slim_must_share_source_and_webkit(self):
        for index, field in enumerate(['source', 'webkit']):
            with self.subTest(field=field):
                root = self.root / str(index)
                root.mkdir()
                fixture = PackageFixture(root, 'gpu')
                fixture.manifests['slim'][field] = {'dirty': False, 'commit': 'c' * 40} if field == 'source' else 'c' * 40
                with self.assertRaisesRegex(ValueError, 'cohort'):
                    fixture.verify()

    def test_gpu_rejects_any_slim_runtime_or_build_assets(self):
        for index, section in enumerate(['native', 'runtime', 'runtimeTargets', 'build', 'buildTransitive', 'buildMultiTargeting']):
            with self.subTest(section=section):
                root = self.root / str(index)
                root.mkdir()
                fixture = PackageFixture(root, 'gpu')
                fixture.assets['targets']['net10.0'][SLIM + '/' + VERSION][section] = {'unexpected.dll': {}}
                with self.assertRaisesRegex(ValueError, 'suppressed'):
                    fixture.verify()

    def test_gpu_assets_must_select_the_gpu_payload(self):
        fixture = self.fixture('gpu')
        fixture.assets['targets']['net10.0/win-x64'][GPU + '/' + VERSION].pop('native')
        with self.assertRaisesRegex(ValueError, 'selected'):
            fixture.verify()

    def test_meta_keeps_all_five_rids(self):
        fixture = self.fixture('gpu')
        fixture.assets['targets']['net10.0/win-x64']['Ranvier.WebCore.Native/' + VERSION]['dependencies'].pop('Ranvier.WebCore.Native.linux-arm64')
        with self.assertRaisesRegex(ValueError, 'five'):
            fixture.verify()

    def test_wrong_view_version(self):
        fixture = self.fixture()
        fixture.assets['targets']['net10.0/win-x64'][VIEW + '/0.0.0'] = fixture.assets['targets']['net10.0/win-x64'].pop(VIEW + '/' + VIEW_VERSION)
        with self.assertRaisesRegex(ValueError, 'version'):
            fixture.verify()

    def test_no_project_reference_can_replace_a_published_package(self):
        fixture = self.fixture()
        fixture.assets['libraries']['local/1.0.0'] = {'type': 'project'}
        with self.assertRaisesRegex(ValueError, 'project reference'):
            fixture.verify()

    def test_native_graph_rejects_browser_bindings_but_permits_fable_core(self):
        for index, package in enumerate(['Xantham.Fable.Core.TS', 'Xantham.Other', 'Fable.Ranvier', 'Fable.Ranvier.Dom', 'Fable.Ranvier.Dom.Traced']):
            with self.subTest(package=package):
                root = self.root / str(index)
                root.mkdir()
                fixture = PackageFixture(root)
                fixture.assets['libraries'][package + '/1.0.0'] = {'type': 'package'}
                with self.assertRaisesRegex(ValueError, 'browser packages'):
                    fixture.verify()

    def test_native_contracts_must_resolve_shared_dom(self):
        fixture = self.fixture()
        fixture.assets['targets']['net10.0/win-x64'].pop('Ranvier.Dom.Traced/0.1.0-alpha.2')
        with self.assertRaisesRegex(ValueError, 'contracts'):
            fixture.verify()

    def test_selected_cache_must_come_from_official_nuget(self):
        fixture = self.fixture()
        write_json(fixture.package_roots['slim'] / '.nupkg.metadata', {'source': 'C:/local-packs'})
        with self.assertRaisesRegex(ValueError, 'official'):
            fixture.verify()

    def test_cache_payload_must_match_manifest_too(self):
        fixture = self.fixture()
        record = fixture.manifests['slim']['files'][-1]
        path = fixture.package_roots['slim'] / 'runtimes/win-x64/native' / record['name']
        path.write_bytes(b'Y' * path.stat().st_size)
        with self.assertRaisesRegex(ValueError, 'hash'):
            fixture.verify()

    def test_manifest_paths_are_safe_and_case_unique(self):
        for index, name in enumerate(['../escape', 'C:/escape', 'WebKit.resources\\escape', 'WebKit.resources/file.']):
            with self.subTest(name=name):
                root = self.root / str(index)
                root.mkdir()
                fixture = PackageFixture(root)
                fixture.manifests['slim']['files'][-1]['name'] = name
                with self.assertRaises(ValueError):
                    fixture.verify()
        fixture = self.fixture()
        fixture.manifests['slim']['files'][-1]['name'] = 'WEBCORE.DLL'
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            fixture.verify()

    def test_unlisted_package_or_output_resources_are_rejected(self):
        fixture = self.fixture()
        extra = fixture.output / 'WebKit.resources/leftover.bin'
        extra.write_bytes(b'extra')
        with self.assertRaisesRegex(ValueError, 'resource'):
            fixture.verify()
        extra.unlink()
        extra = fixture.package_roots['slim'] / 'runtimes/win-x64/native/leftover.dll'
        extra.write_bytes(b'extra')
        with self.assertRaisesRegex(ValueError, 'manifest'):
            fixture.verify()


if __name__ == '__main__':
    unittest.main()

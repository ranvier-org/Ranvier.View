import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from packages import admit_graph, expected_packages, validate_packages
from qualify import lifecycle, project, retained

CONFIG = json.loads(Path(__file__).with_name('inputs.json').read_text())


def assets(mode):
    rows = {**expected_packages(CONFIG, mode), **{r['id'].lower(): r for r in CONFIG['nativePackagePins']}}
    libraries = {r['id'] + '/' + r['version']: {'type': 'package'} for r in rows.values()}
    libraries['FSharp.Core/' + CONFIG['fsharpCore']] = {'type': 'package'}
    return {'libraries': libraries, 'targets': {'net10.0/osx-arm64': {}}}


def cache_fixture(root):
    config, data = copy.deepcopy(CONFIG), assets('normal')
    names = {'ranvier': 'Ranvier.dll', 'ranvier.dom': 'Ranvier.Dom.dll', 'ranvier.webcore': 'Ranvier.WebCore.dll',
             'ranvier.view': 'Ranvier.View.dll', 'ranvier.view.dom': 'Ranvier.View.Dom.dll',
             'ranvier.view.webcore': 'Ranvier.View.WebCore.dll', 'ranvier.view.webcore.sdl': 'Ranvier.View.WebCore.Sdl.dll',
             'fsharp.core': 'FSharp.Core.dll', 'ppy.sdl3-cs': 'SDL3-CS.dll'}
    digests = {}
    for key, row in data['libraries'].items():
        name, version = key.rsplit('/', 1)
        folder = root / name.lower() / version
        folder.mkdir(parents=True)
        row['path'] = folder.relative_to(root).as_posix()
        package = folder / (key.replace('/', '.').lower() + '.nupkg')
        with zipfile.ZipFile(package, 'w') as zipped:
            members = {}
            if name.lower() in names:
                member = 'lib/' + ('netstandard2.0/' if name == 'FSharp.Core' else 'net8.0/') + names[name.lower()]
                zipped.writestr(member, key.encode())
                members[member] = {}
            data['targets']['net10.0/osx-arm64'][key] = {'runtime': members}
        digests[name.lower()] = hashlib.sha256(package.read_bytes()).hexdigest()
        (folder / '.nupkg.metadata').write_text(json.dumps({'source': 'https://api.nuget.org/v3/index.json'}))
    for row in config['viewPackages'] + config['nativePackagePins']:
        if row['id'].lower() in digests:
            row['sha256'] = digests[row['id'].lower()]
    config['domPackageSha256']['normal'] = digests['ranvier.dom']
    config['interopPackageSha256'] = digests['ranvier.webcore']
    config['sdl']['packageSha256'] = digests['ppy.sdl3-cs']
    return config, data


class PackageAdmission(unittest.TestCase):
    def test_both_complete_public_modes(self):
        for mode in ['normal', 'traced']:
            self.assertIn('ranvier.view.webcore', {n.removesuffix('.traced') for n in admit_graph(assets(mode), CONFIG, mode)[0]})

    def test_opposing_mixed_case_package(self):
        for mode in ['normal', 'traced']:
            data = assets(mode)
            name = 'Ranvier.VIEW' + ('.Traced' if mode == 'normal' else '')
            data['libraries'][name + '/0.1.46'] = {'type': 'package'}
            with self.assertRaisesRegex(ValueError, 'opposing'):
                admit_graph(data, CONFIG, mode)

    def test_missing_pair(self):
        data = assets('normal')
        del data['libraries']['Ranvier.View.WebCore.Sdl/0.1.0-alpha.46']
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            admit_graph(data, CONFIG, 'normal')

    def test_source_provider_rejected(self):
        data = assets('normal')
        data['libraries']['Ranvier.View/0.1.46']['type'] = 'project'
        with self.assertRaisesRegex(ValueError, 'source project'):
            admit_graph(data, CONFIG, 'normal')

    def test_wrong_version(self):
        data = assets('normal')
        data['libraries']['Ranvier.View/0.1.45'] = data['libraries'].pop('Ranvier.View/0.1.46')
        with self.assertRaisesRegex(ValueError, 'stale'):
            admit_graph(data, CONFIG, 'normal')

    def test_native_gpu_and_abi64_family_rejected(self):
        for name in ['Ranvier.WebCore.Native.Gpu.osx-arm64/0.1.1-preview.11', 'Ranvier.WebCore.Native.osx-arm64/0.1.1-preview.12']:
            data = assets('normal')
            data['libraries'][name] = {'type': 'package'}
            with self.assertRaisesRegex(ValueError, 'native package'):
                admit_graph(data, CONFIG, 'normal')

    def test_actual_target_required(self):
        data = assets('normal')
        data['targets'] = {'net10.0/win-x64': {}}
        with self.assertRaisesRegex(ValueError, 'osx-arm64'):
            admit_graph(data, CONFIG, 'normal')

    def test_project_is_package_only_and_exact(self):
        for mode in ['normal', 'traced']:
            with tempfile.TemporaryDirectory() as directory:
                xml = ET.parse(project(Path(directory) / 'app', mode, CONFIG))
                self.assertFalse(xml.findall('.//ProjectReference'))
                actual = {r.attrib['Include'].lower(): r.attrib['Version'] for r in xml.findall('.//PackageReference') if 'Include' in r.attrib}
                self.assertEqual(actual, {n: '[' + r['version'] + ']' for n, r in expected_packages(CONFIG, mode).items()})

    def test_exact_lifecycle_and_dynamic_code(self):
        for dynamic in [True, False]:
            text = 'DYNAMIC_CODE_SUPPORTED=' + str(dynamic) + '\nLIFECYCLE_PASS host=7 window=35 common=49'
            self.assertEqual(lifecycle(text, dynamic)['common'], 49)
            for bad in [text.replace('common=49', 'common=4'), text.replace('common=49', 'common=490'),
                        text.replace(str(dynamic), str(not dynamic)), text + '\n' + text, '']:
                with self.assertRaises(ValueError):
                    lifecycle(bad, dynamic)

    def test_actual_runtime_member_paths_and_hashes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config, data = cache_fixture(root)
            result = validate_packages(data, config, 'normal', root)
            self.assertEqual(len(result['dlls']), 9)
            self.assertIn('FSharp.Core.dll', result['dlls'])

    def test_raw_package_tamper(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config, data = cache_fixture(root)
            p = root / 'ranvier.view/0.1.46/ranvier.view.0.1.46.nupkg'
            with p.open('ab') as stream:
                stream.write(b'tamper')
            with self.assertRaisesRegex(ValueError, 'bytes differ'):
                validate_packages(data, config, 'normal', root)

    def test_nonpublic_cache_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config, data = cache_fixture(root)
            (root / 'ranvier.view/0.1.46/.nupkg.metadata').write_text(json.dumps({'source': '/local/feed'}))
            with self.assertRaisesRegex(ValueError, 'public NuGet'):
                validate_packages(data, config, 'normal', root)

    def test_evidence_selection_matches_uploaded_categories(self):
        for name in ['run-jit-normal/libWebCore.dylib', 'run-aot-traced/Ranvier.View.WebCore.Tests',
                     'jit-normal/obj/Consumer/project.assets.json', 'aot-traced/bin/Consumer/.hidden.json',
                     'receipt.json', 'publish-aot.log', 'normal.binlog', 'controller-source.zip',
                     'NuGet.Config', 'app-normal/Consumer.fsproj']:
            self.assertTrue(retained(Path(name)), name)
        for name in ['cache-normal/ranvier/metadata.json', 'evidence.json', 'aot-normal/obj/native/object.o']:
            self.assertFalse(retained(Path(name)), name)


if __name__ == '__main__':
    unittest.main()

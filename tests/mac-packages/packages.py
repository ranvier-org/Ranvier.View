"""Require the audited public package graph and its actual runtime DLLs."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

sys.path.append(str(Path(__file__).resolve().parents[1] / 'mac-public'))
from support import PUBLIC, sha

FAMILIES = {'ranvier.view', 'ranvier.view.dom', 'ranvier.view.webcore', 'ranvier.view.webcore.sdl'}


def expected_packages(config, mode):
    traced = mode == 'traced'
    result = {}
    for row in config['viewPackages']:
        name = row['id'].lower()
        if name.endswith('.traced') == traced:
            result[name] = row
    if {name.removesuffix('.traced') for name in result} != FAMILIES or len(result) != 4:
        raise ValueError('Incomplete audited View package family')
    suffix = '.Traced' if traced else ''
    for name, version, digest in [('Ranvier' + suffix, config['coreVersion'], None),
                                  ('Ranvier.Dom' + suffix, config['domVersion'], config['domPackageSha256'][mode]),
                                  ('Ranvier.WebCore', config['interopVersion'], config['interopPackageSha256']),
                                  ('ppy.SDL3-CS', config['sdl']['version'], config['sdl']['packageSha256'])]:
        result[name.lower()] = {'id': name, 'version': version, 'sha256': digest}
    return result


def sdk_source(value, directory):
    return directory is not None and Path(value).is_absolute() and Path(value).resolve() == directory.resolve()


def admit_graph(data, config, mode, sdk_library_packs=None):
    sources = data['project']['restore']['sources']
    if PUBLIC not in sources or any(s != PUBLIC and not sdk_source(s, sdk_library_packs) for s in sources):
        raise ValueError('Unexpected restore source outside public NuGet and the pinned SDK infrastructure')
    expected = expected_packages(config, mode)
    native = {r['id'].lower(): r for r in config['nativePackagePins']}
    found = {}
    for key, row in data['libraries'].items():
        name, version = key.rsplit('/', 1)
        lower = name.lower()
        if row['type'] != 'package':
            raise ValueError('Package-only consumer cannot contain a source project: ' + key)
        if lower.startswith('ranvier.view') or lower in {'ranvier', 'ranvier.traced', 'ranvier.dom', 'ranvier.dom.traced', 'ranvier.webcore', 'ppy.sdl3-cs'}:
            if lower not in expected or version != expected[lower]['version'] or lower in found:
                raise ValueError('Unexpected/opposing/stale managed package: ' + key)
            found[lower] = key
        elif lower.startswith('ranvier.webcore.native'):
            if lower not in native or version != native[lower]['version'] or lower in found:
                raise ValueError('Unexpected native package profile/version: ' + key)
            found[lower] = key
    if not expected.keys() <= found.keys() or not {'ranvier.webcore.native', 'ranvier.webcore.native.osx-arm64'} <= found.keys():
        raise ValueError('Incomplete public managed/native graph')
    fsharp = [key for key in data['libraries'] if key.lower().startswith('fsharp.core/')]
    if len(fsharp) != 1 or fsharp[0].rsplit('/', 1)[1] != config['fsharpCore']:
        raise ValueError('Unexpected FSharp.Core')
    found['fsharp.core'] = fsharp[0]
    targets = [target for name, target in data['targets'].items() if name == 'net10.0/osx-arm64']
    if len(targets) != 1:
        raise ValueError('Require actual net10.0/osx-arm64 dependency resolution')
    return found, {**expected, **native, 'fsharp.core': {'sha256': config['fsharpCorePackageSha256']}}, targets[0]


def validate_packages(data, config, mode, cache, sdk_library_packs=None, public_fsharp=None):
    found, expected, target = admit_graph(data, config, mode, sdk_library_packs)
    rows, dlls, framework_assets = [], {}, {}
    for lower, key in found.items():
        folder = (cache / data['libraries'][key]['path']).resolve(strict=True)
        if not folder.is_relative_to(cache.resolve()):
            raise ValueError('Cache path escaped')
        path = folder / (key.replace('/', '.').lower() + '.nupkg')
        metadata = json.loads((folder / '.nupkg.metadata').read_text())
        framework = lower == 'fsharp.core' and sdk_source(metadata['source'], sdk_library_packs)
        if metadata['source'] != PUBLIC and not framework:
            raise ValueError('Expected public NuGet source: ' + key)
        digest = sha(path)
        pinned = expected.get(lower, {}).get('sha256')
        infrastructure = None
        if framework:
            packages = [p for p in sdk_library_packs.glob('*.nupkg') if p.name.lower() == key.replace('/', '.').lower() + '.nupkg']
            if len(packages) != 1 or sha(packages[0]) != digest or public_fsharp is None or sha(public_fsharp) != pinned:
                raise ValueError('FSharp.Core does not match the installed SDK and pinned public package')
            with zipfile.ZipFile(path) as sdk, zipfile.ZipFile(public_fsharp) as public:
                sdk_members = {n: hashlib.sha256(sdk.read(n)).hexdigest() for n in sdk.namelist() if n.endswith('.dll')}
                public_members = {n: hashlib.sha256(public.read(n)).hexdigest() for n in public.namelist() if n.endswith('.dll')}
            if not sdk_members or sdk_members != public_members:
                raise ValueError('SDK FSharp.Core DLL/resource bytes differ from public package')
            infrastructure = {'sdkPackageSha256': digest, 'publicPackageSha256': pinned, 'equalDllAndResourceMembers': sdk_members}
        elif pinned and pinned != digest:
            raise ValueError('Audited public package bytes differ: ' + key)
        members = target.get(key, {}).get('runtime', {})
        with zipfile.ZipFile(path) as zipped:
            for member in members:
                if member.endswith('.dll') and Path(member).name.startswith(('Ranvier', 'FSharp.Core', 'SDL3-CS')):
                    name = Path(member).name
                    if name in dlls:
                        raise ValueError('Ambiguous runtime DLL: ' + name)
                    dlls[name] = hashlib.sha256(zipped.read(member)).hexdigest()
                    if lower == 'fsharp.core':
                        framework_assets[name] = member
            if lower == 'fsharp.core':
                for member, row in target[key].get('resource', {}).items():
                    dlls[row['locale'] + '/' + Path(member).name] = hashlib.sha256(zipped.read(member)).hexdigest()
                    framework_assets[row['locale'] + '/' + Path(member).name] = member
        rows.append({'id': key, 'sha256': digest, 'package': str(path), 'metadata': metadata,
                     'runtimeAssets': list(members), 'frameworkInfrastructure': infrastructure})
    required = {'Ranvier.dll', 'Ranvier.Dom.dll', 'Ranvier.WebCore.dll', 'FSharp.Core.dll',
                'Ranvier.View.dll', 'Ranvier.View.Dom.dll', 'Ranvier.View.WebCore.dll', 'Ranvier.View.WebCore.Sdl.dll', 'SDL3-CS.dll'}
    if {name for name in dlls if '/' not in name} != required:
        raise ValueError('Missing/extra actual public runtime DLLs: ' + repr(dlls.keys()))
    return {'packages': rows, 'dlls': dlls, 'frameworkAssets': framework_assets}

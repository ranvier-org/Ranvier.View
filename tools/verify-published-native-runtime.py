"""Verify restored public native assets and every manifest file in a JIT/AOT output."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re

OFFICIAL_SOURCE = 'https://api.nuget.org/v3/index.json'
# Ranvier.WebCore.Native 0.1.1-rc.5 and later: the GPU runtime under the plain per-RID IDs, Slim under .Cpu.<rid>.
# The aggregate depends on the GPU runtimes and on the CPU win-arm64 runtime, which has no GPU build.
META_DEPENDENCIES = {'Ranvier.WebCore.Native.' + rid for rid in ('linux-x64', 'linux-arm64', 'osx-arm64', 'win-x64')} | {
    'Ranvier.WebCore.Native.Cpu.win-arm64'}
MAC_LIBRARIES = {'libRanvierWebCoreHost.dylib', 'libWebCore.dylib', 'libJavaScriptCore.dylib', 'libWebGPU.dylib'}
ASSET_SECTIONS = {'native', 'runtime', 'runtimeTargets', 'resource', 'build', 'buildTransitive', 'buildMultiTargeting'}


def relative_name(value):
    if not isinstance(value, str):
        raise ValueError('Manifest/package paths must be strings.')
    path = PurePosixPath(value)
    if (not value or path.is_absolute() or path.as_posix() != value or '\\' in value
            or any(part in {'.', '..'} or ':' in part or part.endswith(('.', ' ')) for part in path.parts)):
        raise ValueError(f'Unsafe manifest/package path: {value}')
    return value


def contained_file(root, name):
    path = root / relative_name(name)
    if not path.is_file():
        raise ValueError(f'Required file is missing: {path}')
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError(f'File escapes its package/output directory: {path}')
    return path


def matching_entry(entries, package, version):
    matches = [(key, value) for key, value in entries.items() if key.rsplit('/', 1)[0].casefold() == package.casefold()]
    if len(matches) != 1 or matches[0][0].rsplit('/', 1)[1].casefold() != version.casefold():
        raise ValueError(f'Expected exactly {package} version {version}.')
    return matches[0]


def active_assets(entry):
    return {path for section in ASSET_SECTIONS for path in entry.get(section, {}) if PurePosixPath(path).name != '_._'}


def check_bytes(path, record):
    if path.stat().st_size != record['bytes']:
        raise ValueError(f'Runtime file size mismatch: {path}')
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    if digest != record['sha256'].lower():
        raise ValueError(f'Runtime file hash mismatch: {path}')


def package_manifest(assets, package, version, flavor, abi, rid='win-x64'):
    _, library = matching_entry(assets['libraries'], package, version)
    name = relative_name(library['path'])
    candidates = [Path(folder) / name for folder in assets['packageFolders'] if (Path(folder) / name / 'manifest.json').is_file()]
    if len(candidates) != 1:
        raise ValueError(f'Expected one restored package-cache manifest for {package}.')
    root = candidates[0]
    metadata = json.loads(contained_file(root, '.nupkg.metadata').read_text(encoding='utf-8'))
    if metadata.get('source', '').rstrip('/').casefold() != OFFICIAL_SOURCE:
        raise ValueError(f'{package} was not restored from official NuGet.')
    manifest = json.loads(contained_file(root, 'manifest.json').read_text(encoding='utf-8'))
    if manifest.get('version') != version or manifest.get('rid') != rid or manifest.get('flavor') != flavor:
        raise ValueError(f'{package} manifest version/RID/flavor does not match the requested runtime.')
    if type(manifest.get('abi')) is not int or manifest['abi'] != abi:
        raise ValueError(f'{package} manifest ABI must be {abi}.')
    source = manifest.get('source', {})
    if source.get('dirty') is not False or not re.fullmatch('[0-9a-fA-F]{40}', source.get('commit', '')):
        raise ValueError(f'{package} must identify a clean source commit.')
    if not re.fullmatch('[0-9a-fA-F]{40}', manifest.get('webkit', '')):
        raise ValueError(f'{package} must identify its WebKit commit.')
    records = manifest.get('files')
    if not isinstance(records, list) or not records:
        raise ValueError(f'{package} manifest has no files.')
    names = set()
    for record in records:
        name = relative_name(record['name'])
        if name.casefold() in names:
            raise ValueError(f'{package} has duplicate manifest paths: {name}')
        names.add(name.casefold())
        if type(record.get('bytes')) is not int or record['bytes'] < 0 or not re.fullmatch('[0-9a-fA-F]{64}', record.get('sha256', '')):
            raise ValueError(f'{package} has an invalid size/hash record: {name}')
    if rid == 'osx-arm64':
        required = {name.casefold() for name in MAC_LIBRARIES}
        required |= {'libegl.dylib', 'libglesv2.dylib'} if flavor == 'gpu' else set()
    else:
        required = {'ranvierwebcorehost.dll', 'webcore.dll'} | ({'libegl.dll', 'libglesv2.dll'} if flavor == 'gpu' else set())
    if not required <= names:
        raise ValueError(f'{package} is missing required native libraries.')
    if type(manifest.get('total_bytes')) is not int or sum(record['bytes'] for record in records) != manifest['total_bytes']:
        raise ValueError(f'{package} manifest total size does not match.')
    native = root / f'runtimes/{rid}/native'
    actual = {path.relative_to(native).as_posix().casefold() for path in native.rglob('*') if path.is_file()}
    if names != actual:
        raise ValueError(f'{package} native files do not match its complete manifest.')
    for record in records:
        check_bytes(contained_file(native, record['name']), record)
    return manifest


def verify_runtime(assets_path, output, *, native_version, view_package, view_version, flavor, abi, rid='win-x64'):
    if flavor not in {'slim', 'gpu'} or type(abi) is not int or abi <= 0 or rid not in {'win-x64', 'osx-arm64'}:
        raise ValueError('Expected slim/gpu flavor, a positive ABI and supported Windows x64 or macOS ARM64 RID.')
    assets = json.loads(Path(assets_path).read_text(encoding='utf-8'))
    if any(library.get('type') == 'project' for library in assets['libraries'].values()):
        raise ValueError('Published acceptance cannot use a project reference.')
    if any(key.casefold().startswith(('xantham.', 'fable.ranvier')) for key in assets['libraries']):
        raise ValueError('Native acceptance cannot reference Xantham or Fable.Ranvier browser packages.')
    targets = assets['targets']
    traced = view_package.casefold().endswith('.traced')
    families = {'ranvier.view', 'ranvier.view.dom', 'ranvier.view.webcore', 'ranvier.view.webcore.sdl', 'ranvier.dom'}
    opposing = families if traced else {name + '.traced' for name in families}
    if any(key.rsplit('/', 1)[0].casefold() in opposing for graph in [assets['libraries'], *targets.values()] for key in graph):
        raise ValueError('Published acceptance cannot contain opposing normal/traced View or DOM packages.')
    rid_targets = [target for name, target in targets.items() if name.endswith('/' + rid)]
    if len(rid_targets) != 1:
        raise ValueError(f'Restore explicitly for one {rid} target.')
    selected = rid_targets[0]
    matching_entry(selected, view_package, view_version)
    dom_package = 'Ranvier.Dom.Traced' if traced else 'Ranvier.Dom'
    if sum(key.rsplit('/', 1)[0].casefold() == dom_package.casefold() for key in selected) != 1:
        raise ValueError(f'Native contracts must resolve {dom_package}.')
    matching_entry(selected, 'Ranvier.WebCore', native_version)
    _, meta = matching_entry(selected, 'Ranvier.WebCore.Native', native_version)
    dependencies = {name.casefold() for name in meta.get('dependencies', {})}
    if not {name.casefold() for name in META_DEPENDENCIES} <= dependencies:
        raise ValueError('The native meta-package must preserve all five RID dependencies.')
    for dependency in sorted(META_DEPENDENCIES):
        matching_entry(selected, dependency, native_version)
    gpu_package = 'Ranvier.WebCore.Native.' + rid
    slim_package = 'Ranvier.WebCore.Native.Cpu.' + rid
    package = gpu_package if flavor == 'gpu' else slim_package
    _, entry = matching_entry(selected, package, native_version)
    if flavor == 'slim':
        for target in targets.values():
            _, gpu_entry = matching_entry(target, gpu_package, native_version)
            if active_assets(gpu_entry):
                raise ValueError(f'CPU restore must have all default GPU {rid} runtime/native/buildTransitive assets suppressed.')
    elif any(key.rsplit('/', 1)[0].casefold() == slim_package.casefold() and active_assets(value) for target in targets.values() for key, value in target.items()):
        raise ValueError('A GPU restore selected CPU assets.')
    manifest = package_manifest(assets, package, native_version, flavor, abi, rid)
    if flavor == 'slim':
        gpu = package_manifest(assets, gpu_package, native_version, 'gpu', abi, rid)
        cohort = lambda value: (value['version'], value['abi'], value['webkit'].lower(), value['source']['commit'].lower())
        if cohort(manifest) != cohort(gpu):
            raise ValueError('GPU and slim manifests must belong to the exact same release cohort.')
    prefix = f'runtimes/{rid}/native/'
    payload = {path[len(prefix):].casefold() for path in active_assets(entry) if path.startswith(prefix)}
    records = manifest['files']
    if payload != {record['name'].casefold() for record in records}:
        raise ValueError(f'The selected {package} native assets do not match its manifest.')
    output = Path(output)
    for record in records:
        check_bytes(contained_file(output, record['name']), record)
    resources = output / 'WebKit.resources'
    actual_resources = {path.relative_to(output).as_posix().casefold() for path in resources.rglob('*') if path.is_file()}
    expected_resources = {record['name'].casefold() for record in records if record['name'].casefold().startswith('webkit.resources/')}
    if actual_resources != expected_resources:
        raise ValueError('Output WebKit resources do not match the selected runtime manifest.')
    for name in ['libEGL.dll', 'libGLESv2.dll', 'libEGL.dylib', 'libGLESv2.dylib']:
        if (output / name).exists() and name.casefold() not in {record['name'].casefold() for record in records}:
            raise ValueError(f'Unexpected GPU native library in output: {name}')
    return {'package': package, 'version': native_version, 'flavor': flavor, 'rid': rid, 'abi': abi, 'files': len(records), 'output': str(output)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--native-version', required=True)
    parser.add_argument('--view-package', required=True)
    parser.add_argument('--view-version', required=True)
    parser.add_argument('--flavor', required=True, choices=['slim', 'gpu'])
    parser.add_argument('--abi', type=int, required=True, help='Expected ABI of the exact published runtime cohort.')
    parser.add_argument('--rid', choices=['win-x64', 'osx-arm64'], default='win-x64')
    args = parser.parse_args()
    try:
        result = verify_runtime(args.assets, args.output, native_version=args.native_version,
                                view_package=args.view_package, view_version=args.view_version, flavor=args.flavor, abi=args.abi, rid=args.rid)
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f'Published runtime verification failed: {error}\n')
    print(f"Verified {result['package']} {result['version']}, ABI {result['abi']}: all {result['files']} native/resource hashes in {result['output']}")


if __name__ == '__main__':
    main()

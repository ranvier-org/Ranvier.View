"""Build only the consumer against audited public View46 packages on Mac26."""
import argparse
import json
import os
from pathlib import Path
import platform
import re
import shutil
import sys
import traceback
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
FIXTURES = ROOT / 'tests/mac-public'
sys.path.insert(0, str(FIXTURES))
sys.path.insert(0, str(HERE))
from packages import expected_packages, validate_packages
from reuse import admit_reuse
from support import Runner, atomic_json, runtime_identity, sha, source_identity, strict_publish, text_sha


def project(directory, mode, config):
    directory.mkdir()
    xml = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(xml, 'PropertyGroup')
    for key, value in dict(TargetFramework='net10.0', OutputType='Exe', AssemblyName='Ranvier.View.WebCore.Tests',
                           IsPackable='false', GenerateDocumentationFile='false', IlcTreatWarningsAsErrors='true',
                           ILLinkTreatWarningsAsErrors='true', TrimmerSingleWarn='false',
                           DisableImplicitLibraryPacksFolder='true').items():
        ET.SubElement(properties, key).text = value
    items = ET.SubElement(xml, 'ItemGroup')
    for name in config['fixtureHashes']:
        ET.SubElement(items, 'Compile', Include=str(FIXTURES / name))
    for row in expected_packages(config, mode).values():
        ET.SubElement(items, 'PackageReference', Include=row['id'], Version='[' + row['version'] + ']')
    ET.SubElement(items, 'PackageReference', Update='FSharp.Core', Version='[' + config['fsharpCore'] + ']')
    path = directory / 'Consumer.fsproj'
    ET.ElementTree(xml).write(path, encoding='utf-8', xml_declaration=True)
    if ET.parse(path).findall('.//ProjectReference'):
        raise ValueError('A package-only consumer must not contain source project references')
    return path


def graph(artifact, mode, cache, config, sdk_packs, public_fsharp):
    paths = list((artifact / 'obj/Consumer').rglob('project.assets.json'))
    if len(paths) != 1:
        raise ValueError('Missing/ambiguous standalone consumer assets')
    data = json.loads(paths[0].read_text())
    metadata = []
    for name, row in data['libraries'].items():
        folder = (cache / row['path']).resolve()
        if not folder.is_relative_to(cache.resolve()):
            raise ValueError('Package diagnostic path escaped')
        path = folder / '.nupkg.metadata'
        metadata.append({'id': name, 'metadata': json.loads(path.read_text()) if path.is_file() else None})
    atomic_json(artifact / 'public-package-admission.json', {'assetsSha256': sha(paths[0]),
        'restoreSources': data['project']['restore']['sources'], 'packageMetadata': metadata})
    result = validate_packages(data, config, mode, cache, sdk_packs, public_fsharp)
    result.update(assetsSha256=sha(paths[0]), libraries=data['libraries'], project=data['project'])
    return result


def lifecycle(text, dynamic):
    lines = text.splitlines()
    if ([line for line in lines if line.startswith('DYNAMIC_CODE_SUPPORTED=')] != ['DYNAMIC_CODE_SUPPORTED=' + str(dynamic)]
            or lines.count('LIFECYCLE_PASS host=7 window=35 common=49') != 1):
        raise ValueError('Actual complete91 lifecycle/dynamic-code verdict missing')
    return {'host': 7, 'window': 35, 'common': 49, 'dynamicCodeSupported': dynamic}


def framework_overlay(output, public, assets):
    before = {p.relative_to(output).as_posix(): sha(p) for p in output.rglob('*') if p.is_file()}
    expected, rows = dict(before), []
    with zipfile.ZipFile(public) as zipped:
        for name, member in assets['frameworkAssets'].items():
            path = output / name
            parts = Path(name).parts
            if (not path.is_file() or '..' in parts or Path(name).is_absolute()
                    or not (name == 'FSharp.Core.dll' or (len(parts) == 2 and parts[1] == 'FSharp.Core.resources.dll'))):
                raise ValueError('Framework overlay does not name an existing output')
            old = sha(path)
            path.write_bytes(zipped.read(member))
            expected[name] = assets['dlls'][name]
            rows.append({'name': name, 'originalSdkSha256': old, 'publicSha256': sha(path)})
    after = {p.relative_to(output).as_posix(): sha(p) for p in output.rglob('*') if p.is_file()}
    if after != expected:
        raise ValueError('Framework-only overlay changed other JIT output bytes')
    return {'frameworkFiles': rows, 'consumerAndProductBytesUnchanged': True}


def retained(path):
    if path.parts[0].startswith('cache-') or path.name in {'evidence.json', 'evidence.json.tmp'}:
        return False
    return (path.parts[0].startswith('run-') or path.suffix == '.json'
            or (len(path.parts) == 1 and path.suffix in {'.log', '.binlog', '.zip', '.Config', '.nupkg'})
            or (path.parts[0].startswith('app-') and path.suffix == '.fsproj'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inputs', type=Path, default=HERE / 'inputs.json')
    parser.add_argument('--inputs-sha256', required=True)
    parser.add_argument('--controller-commit', required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--reuse-jit', type=Path, required=True)
    args = parser.parse_args()
    if sha(args.inputs) != args.inputs_sha256:
        raise ValueError('Reviewed package inputs changed')
    config = json.loads(args.inputs.read_text())
    if not args.execute:
        print(json.dumps({'execute': False, 'scope': config['scope'], 'publicViewSource': config['publicViewSource']}))
        return 0
    if platform.system() != 'Darwin' or platform.machine() != 'arm64' or platform.mac_ver()[0].split('.')[0] != '26':
        raise ValueError('Actual Mac26 ARM64 required')
    work = args.work.resolve()
    if work.exists() or work.is_relative_to(ROOT) or ROOT.is_relative_to(work):
        raise ValueError('New consumer output must be outside source')
    work.mkdir(parents=True)
    record = {'scope': config['scope'], 'controllerCommit': args.controller_commit, 'publicViewSource': config['publicViewSource'],
              'commands': [], 'routes': [], 'status': 'STARTING', 'noSourceProviderBuild': True,
              'noEngineBuild': True, 'noPublication': True, 'platform': platform.platform()}
    def save():
        atomic_json(work / 'receipt.json', record)
    environment = dict(os.environ, DOTNET_PROCESSOR_COUNT='2', DOTNET_CLI_USE_MSBUILD_SERVER='0',
                       MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_UI_LANGUAGE='en', DOTNET_NOLOGO='1')
    for name in ['DYLD_LIBRARY_PATH', 'DYLD_FRAMEWORK_PATH', 'DYLD_INSERT_LIBRARIES', 'DYLD_FALLBACK_LIBRARY_PATH']:
        environment.pop(name, None)
    run = Runner(work, record, save, environment)
    inputs = {}
    reuse_inputs = {}
    native = {r['name']: r['sha256'] for r in config['native']['manifest']['files']}
    native['libSDL3.dylib'] = config['sdl']['dylibSha256']
    try:
        source_identity(ROOT, args.controller_commit)
        paths = [*HERE.glob('*.py'), HERE / 'inputs.json', FIXTURES / 'support.py',
                 *(FIXTURES / n for n in config['fixtureHashes'])]
        inputs = {str(p): sha(p) for p in paths}
        record['sourceInputHashes'] = inputs
        if config['reviewedTextHashPolicy'] != 'UTF8_CRLF_TO_LF_ONLY' or any(
                text_sha(FIXTURES / n) != digest for n, digest in config['fixtureHashes'].items()):
            raise ValueError('Reviewed lifecycle fixture mismatch')
        atomic_json(work / 'global.json', {'sdk': {'version': config['sdk'], 'rollForward': 'disable', 'allowPrerelease': False}})
        if run('sdk', ['dotnet', '--version'], work) != config['sdk']:
            raise ValueError('Wrong SDK')
        run('dotnet-info', ['dotnet', '--info'], work)
        sdk_rows = re.findall('^' + re.escape(config['sdk']) + r' \[(.+)\]$', run('sdk-list', ['dotnet', '--list-sdks'], work), re.M)
        if len(sdk_rows) != 1:
            raise ValueError('Actual pinned SDK directory missing/ambiguous')
        sdk_packs = (Path(sdk_rows[0]) / config['sdk'] / 'FSharp/library-packs').resolve(strict=True)
        public_fsharp = work / 'fsharp-core-public.nupkg'
        with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/fsharp.core/' + config['fsharpCore'] + '/fsharp.core.' + config['fsharpCore'] + '.nupkg', timeout=60) as response:
            data = response.read(8 * 1024 ** 2 + 1)
        if len(data) > 8 * 1024 ** 2:
            raise ValueError('Public framework package exceeds budget')
        public_fsharp.write_bytes(data)
        if sha(public_fsharp) != config['fsharpCorePackageSha256']:
            raise ValueError('Pinned public framework package differs')
        record['sdkLibraryPacks'] = str(sdk_packs)
        reused, reuse_inputs = admit_reuse(args.reuse_jit.resolve(strict=True), config)
        record['reusedJit'] = reused
        run('xcode', ['xcodebuild', '-version'], work)
        (work / 'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="public" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>', encoding='utf-8')
        run('source-archive', ['git', 'archive', '--format=zip', '--output=' + str(work / 'controller-source.zip'),
            args.controller_commit, 'tests/mac-packages', 'tests/mac-public/support.py',
            *('tests/mac-public/' + n for n in config['fixtureHashes']), '.github/workflows/mac-public-view46-packages.yml'], ROOT)
        for mode in ['normal', 'traced']:
            app = project(work / ('app-' + mode), mode, config)
            cache = work / ('cache-' + mode)
            env = dict(environment, NUGET_PACKAGES=str(cache))
            common = ['-p:RestorePackagesPath=' + str(cache), '-p:RestoreConfigFile=' + str(work / 'NuGet.Config'),
                      '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
            for kind in ['jit', 'aot']:
                row = {'mode': mode, 'kind': kind, 'status': 'STARTING'}
                record['routes'].append(row)
                artifact = work / (kind + '-' + mode)
                output = work / ('run-' + kind + '-' + mode)
                try:
                    if kind == 'jit':
                        shutil.copytree(args.reuse_jit / ('run-jit-' + mode), output)
                        row['reusedOriginalJitBuild'] = config['reuseJit']['controllerCommit']
                        run('restore-jit-' + mode, ['dotnet', 'msbuild', app, '-t:Restore', '-p:Configuration=Release',
                            '-p:RuntimeIdentifier=osx-arm64', '-p:PublishAot=false', '-p:SelfContained=false',
                            '-p:ArtifactsPath=' + str(artifact), '-p:UseArtifactsOutput=true', '-m:1', '/nr:false', *common], work, env=env)
                    else:
                        run('restore-aot-' + mode, ['dotnet', 'msbuild', app, '-t:Restore', '-p:Configuration=Release',
                            '-p:RuntimeIdentifier=osx-arm64', '-p:PublishAot=true', '-p:SelfContained=true',
                            '-p:ArtifactsPath=' + str(artifact), '-p:UseArtifactsOutput=true', '-m:1', '/nr:false', *common], work, env=env)
                    assets = graph(artifact, mode, cache, config, sdk_packs, public_fsharp)
                    row['graph'] = assets
                    if kind == 'jit':
                        row['frameworkOverlay'] = framework_overlay(output, public_fsharp, assets)
                    if kind == 'aot':
                        text = run('publish-aot-' + mode, ['dotnet', 'publish', app, '-c', 'Release', '-r', 'osx-arm64',
                            '--self-contained', 'true', '--no-restore', '-p:PublishAot=true', '-m:1', '/nr:false',
                            '--artifacts-path', artifact, '-bl:' + str(work / ('aot-' + mode + '.binlog')), '-o', output, *common],
                            work, timeout=1800, env=env)
                        strict_publish(text)
                        binary = output / 'Ranvier.View.WebCore.Tests'
                        if run('aot-arch-' + mode, ['lipo', '-archs', binary], work) != 'arm64':
                            raise ValueError('Wrong AOT architecture')
                        imports = run('aot-imports-' + mode, ['otool', '-L', binary], work)
                        if 'coreclr' in imports.lower() or any(p.name.lower().startswith(('libcoreclr', 'system.private.corelib')) for p in output.iterdir()):
                            raise ValueError('AOT output contains CLR')
                    before = runtime_identity(output, native, assets['dlls'] if kind == 'jit' else None)
                    row['runtimeBefore'] = before
                    runtime_env = dict(env, DYLD_LIBRARY_PATH=str(output))
                    code = ('import ctypes;h=ctypes.CDLL(' + repr(str(output / 'libRanvierWebCoreHost.dylib'))
                            + ');h.rv_abi_version.restype=ctypes.c_int32;assert h.rv_abi_version()==63;print("PUBLIC_ABI63_PASS")')
                    try:
                        run('abi-' + kind + '-' + mode, [sys.executable, '-I', '-c', code], output, env=runtime_env)
                        command = ['dotnet', output / 'Ranvier.View.WebCore.Tests.dll'] if kind == 'jit' else [binary]
                        text = run('run-' + kind + '-' + mode, command, output, env=runtime_env)
                        row['summary'] = lifecycle(text, kind == 'jit')
                    finally:
                        after = runtime_identity(output, native, assets['dlls'] if kind == 'jit' else None)
                        row.update(runtimeBefore=before, runtimeAfter=after)
                        if before != after:
                            raise ValueError('Actual public runtime changed')
                    row['status'] = 'PASS'
                except Exception as error:
                    row.update(status='FAIL', error=repr(error), traceback=traceback.format_exc())
                save()
        record['status'] = 'PASS' if len(record['routes']) == 4 and all(r['status'] == 'PASS' for r in record['routes']) else 'FAIL'
    except BaseException as error:
        record.update(status='FAIL', error=repr(error), traceback=traceback.format_exc())
        raise
    finally:
        try:
            record['sourceUnchanged'] = source_identity(ROOT, args.controller_commit) == args.controller_commit
            record['inputsUnchanged'] = bool(inputs) and all(sha(p) == digest for p, digest in inputs.items())
            record['allChildrenReaped'] = all(r.get('reaped') for r in record['commands'])
            record['reuseInputsUnchanged'] = bool(reuse_inputs) and all(sha(p) == digest for p, digest in reuse_inputs.items())
            if not all(record.get(n) for n in ['sourceUnchanged', 'inputsUnchanged', 'allChildrenReaped', 'reuseInputsUnchanged']):
                record['status'] = 'FAIL'
        except Exception as error:
            record.update(status='FAIL', finalIdentityError=repr(error))
        save()
        try:
            files = {p.relative_to(work).as_posix(): {'sha256': sha(p), 'bytes': p.stat().st_size}
                     for p in work.rglob('*') if p.is_file() and retained(p.relative_to(work))}
            atomic_json(work / 'evidence.json', {'scope': config['scope'], 'files': files,
                'retention': 'All runtime bytes, route JSON metadata, logs/binlogs, consumer projects and controller source ZIP; cache and other generated intermediates excluded.'})
        except Exception as error:
            record.update(status='FAIL', collectorError=repr(error))
            save()
    return int(record['status'] != 'PASS')


if __name__ == '__main__':
    raise SystemExit(main())

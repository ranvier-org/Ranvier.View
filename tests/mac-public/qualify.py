"""Run current View source on public Core58/InteropP11/Mac26 Slim inputs."""
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import traceback
import urllib.request
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parent))
from support import (Runner, atomic_json, jit_summary, managed_packages, runtime_identity,
                     sha, source_identity, stage_native, strict_publish, text_sha)

HERE = Path(__file__).resolve().parent


def project(view, root):
    app = root / 'app'
    app.mkdir()
    xml = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(xml, 'PropertyGroup')
    for key, value in dict(TargetFramework='net10.0', OutputType='Exe', AssemblyName='Ranvier.View.WebCore.Tests',
                           PublishAot='true', IlcTreatWarningsAsErrors='true', ILLinkTreatWarningsAsErrors='true',
                           TrimmerSingleWarn='false', GenerateDocumentationFile='false').items():
        ET.SubElement(properties, key).text = value
    items = ET.SubElement(xml, 'ItemGroup')
    for name in ['CleanupFaultChecks.fs', 'LifecycleChecks.fs', 'LifecycleMain.fs']:
        ET.SubElement(items, 'Compile', Include=str(HERE / name))
    for name in ['Ranvier.View.WebCore', 'Ranvier.View.WebCore.Sdl']:
        ET.SubElement(items, 'ProjectReference', Include=str(view / 'src' / name / (name + '.fsproj')))
    path = app / 'Lifecycle.fsproj'
    ET.ElementTree(xml).write(path, encoding='utf-8', xml_declaration=True)
    return path


def graph(artifact, name, mode, cache, config):
    paths = list((artifact / 'obj' / name).rglob('project.assets.json'))
    if len(paths) != 1:
        raise ValueError('Missing/ambiguous assets graph: ' + name)
    data = json.loads(paths[0].read_text())
    result = managed_packages(data, mode, cache, config)
    result.update(path=str(paths[0]), sha256=sha(paths[0]), libraries=data['libraries'],
                  project=data['project'])
    return result


def collect(root, config, record):
    bound = {}
    for path in root.rglob('*'):
        if not path.is_file() or path.name in {'evidence.json', 'evidence.json.tmp'}:
            continue
        relative = path.relative_to(root)
        if relative.parts[0].startswith('cache-'):
            continue
        if (path.suffix in {'.log', '.json', '.binlog', '.fsproj', '.Config', '.zip'}
                or relative.parts[0].startswith('publish-')
                or (relative.parts[0].startswith('jit-') and 'bin' in relative.parts)):
            bound[relative.as_posix()] = {'sha256': sha(path), 'bytes': path.stat().st_size}
    atomic_json(root / 'evidence.json', {'sourceCommit': config['viewCommit'], 'files': bound,
                                      'receiptSha256': sha(root / 'receipt.json'),
                                      'scope': record['scope']})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inputs', type=Path, default=HERE / 'inputs.json')
    parser.add_argument('--inputs-sha256', required=True)
    parser.add_argument('--view-root', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--mode', choices=['both', 'normal', 'traced'], default='both')
    args = parser.parse_args()
    if sha(args.inputs) != args.inputs_sha256:
        raise ValueError('Reviewed input identity mismatch')
    config = json.loads(args.inputs.read_text())
    modes = ['normal', 'traced'] if args.mode == 'both' else [args.mode]
    if not args.execute:
        print(json.dumps({'execute': False, 'sourceCommit': config['viewCommit'], 'scope': config['scope'], 'modes': modes}))
        return 0
    if config['viewSourceReview'] != 'QUALIFIED':
        raise ValueError('Formatter/source checkpoint has not been independently qualified')
    if platform.system() != 'Darwin' or platform.machine() != 'arm64' or platform.mac_ver()[0].split('.')[0] != '26':
        raise ValueError('Actual macOS26 arm64 is required')
    view, root = args.view_root.resolve(strict=True), args.work.resolve()
    if root.exists() or any(root.is_relative_to(p) or p.is_relative_to(root) for p in [view, HERE]):
        raise ValueError('Output must be new and separate from immutable sources')
    root.mkdir(parents=True)
    record = {'scope': config['scope'], 'sourceCommit': config['viewCommit'], 'commands': [], 'routes': [],
              'status': 'STARTING', 'noEngineBuild': True, 'noPublication': True,
              'executionModes': modes, 'completeFourRouteQualification': args.mode == 'both',
              'platform': platform.platform(), 'machine': platform.machine(), 'inputsSha256': sha(args.inputs)}
    receipt = root / 'receipt.json'
    def save():
        atomic_json(receipt, record)
    stage = root / 'native/runtimes/osx-arm64/native'
    environment = dict(os.environ, DOTNET_PROCESSOR_COUNT='2', DOTNET_CLI_USE_MSBUILD_SERVER='0',
                       MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_UI_LANGUAGE='en',
                       DOTNET_NOLOGO='1', DYLD_LIBRARY_PATH=str(stage))
    for name in ['DYLD_FRAMEWORK_PATH', 'DYLD_INSERT_LIBRARIES', 'DYLD_FALLBACK_LIBRARY_PATH']:
        environment.pop(name, None)
    run = Runner(root, record, save, environment)
    native = {}
    sources = {}
    try:
        source_identity(view, config['viewCommit'])
        controller_commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=HERE, text=True).strip()
        source_identity(HERE, controller_commit)
        record['controllerCommit'] = controller_commit
        sources = {str(path): sha(path) for path in HERE.iterdir() if path.is_file() and path.suffix in {'.py', '.fs', '.json'}}
        sources[str(args.inputs)] = sha(args.inputs)
        record['controllerInputHashes'] = dict(sources)
        if config.get('reviewedTextHashPolicy') != 'UTF8_CRLF_TO_LF_ONLY':
            raise ValueError('Unknown reviewed text hash policy')
        if any(text_sha(HERE / name) != digest for name, digest in config['fixtureHashes'].items()):
            raise ValueError('Reviewed lifecycle fixture changed')
        inventory_path = HERE / config['jitInventory']['path']
        if inventory_path.parent != HERE or text_sha(inventory_path) != config['jitInventory']['sha256']:
            raise ValueError('Reviewed source test inventory changed')
        inventory = json.loads(inventory_path.read_text())
        if inventory['sourceCommit'] != config['viewCommit']:
            raise ValueError('Test inventory is for another source checkpoint')
        if (HERE / 'CleanupFaultChecks.fs').read_bytes().replace(b'\r\n', b'\n') != (
                view / 'tests/Ranvier.View.WebCore.Tests/CleanupFaultChecks.fs').read_bytes().replace(b'\r\n', b'\n'):
            raise ValueError('Cleanup fixture is not the matching source version')
        atomic_json(root / 'global.json', {'sdk': {'version': config['sdk'], 'rollForward': 'disable', 'allowPrerelease': False}})
        if run('sdk', ['dotnet', '--version'], root) != config['sdk']:
            raise ValueError('Selected SDK differs from reviewed pin')
        run('dotnet-info', ['dotnet', '--info'], root)
        run('xcode', ['xcodebuild', '-version'], root)
        run('os', ['sw_vers'], root)
        run('archive-view', ['git', 'archive', '--format=zip', '--output=' + str(root / 'view-source.zip'), config['viewCommit']], view)
        run('archive-controller', ['git', 'archive', '--format=zip', '--output=' + str(root / 'controller-source.zip'), controller_commit], HERE)
        package = root / 'native.nupkg'
        with urllib.request.urlopen(config['native']['url'], timeout=120) as response, package.open('wb') as output:
            downloaded = 0
            while chunk := response.read(1024 * 1024):
                downloaded += len(chunk)
                if downloaded > 256 * 1024 ** 2:
                    raise ValueError('Native package exceeds the bounded download size')
                output.write(chunk)
        native = stage_native(package, stage, config)
        for name in native:
            if run('arch-' + name, ['lipo', '-archs', stage / name], root) != 'arm64':
                raise ValueError('Wrong native architecture')
        code = ('import ctypes;h=ctypes.CDLL(' + repr(str(stage / 'libRanvierWebCoreHost.dylib'))
                + ');h.rv_abi_version.restype=ctypes.c_int32;assert h.rv_abi_version()==63;print("PUBLIC_P11_ABI63_PASS")')
        run('native-abi', [sys.executable, '-I', '-c', code], root)
        shutil.copy2(view / 'nuget.config', root / 'NuGet.Config')
        record['nugetConfigSha256'] = sha(root / 'NuGet.Config')
        sources[str(view / 'nuget.config')] = record['nugetConfigSha256']
        app = project(view, root)
        for mode in modes:
            cache = root / ('cache-' + mode)
            env = dict(environment, NUGET_PACKAGES=str(cache))
            common = ['-p:RanvierTrace=' + str(mode == 'traced').lower(), '-p:UseLocalRanvier=false',
                      '-p:UseLocalRanvierCss=false', '-p:UseLocalRanvierWebCore=false',
                      '-p:RanvierWebCoreNativeVersion=' + config['interopVersion'], '-p:RanvierWebCoreNativeRid=osx-arm64',
                      '-p:RanvierWebCoreNativeDir=' + str(root / 'native'), '-p:RestorePackagesPath=' + str(cache),
                      '-p:RestoreConfigFile=' + str(root / 'NuGet.Config'), '-p:UseSharedCompilation=false', '-p:NuGetAudit=false']
            for kind in ['jit', 'aot']:
                row = {'mode': mode, 'kind': kind, 'status': 'STARTING'}
                record['routes'].append(row)
                try:
                    artifact = root / (kind + '-' + mode)
                    if kind == 'jit':
                        test_project = view / 'tests/Ranvier.View.WebCore.Tests/Ranvier.View.WebCore.Tests.fsproj'
                        run('build-jit-' + mode, ['dotnet', 'build', test_project, '-c', 'Release', '-r', 'osx-arm64',
                            '-m:1', '/nr:false', '--artifacts-path', artifact, '-bl:' + str(root / ('jit-' + mode + '.binlog')),
                            *common], root, env=env)
                        assets = graph(artifact, 'Ranvier.View.WebCore.Tests', mode, cache, config)
                        entries = list((artifact / 'bin/Ranvier.View.WebCore.Tests').rglob('Ranvier.View.WebCore.Tests.dll'))
                        if len(entries) != 1:
                            raise ValueError('Missing/ambiguous test entry')
                        before = runtime_identity(entries[0].parent, native, assets['dlls'])
                        try:
                            text = run('run-jit-' + mode, ['dotnet', entries[0], '--summary'], entries[0].parent, timeout=1200, env=env)
                            row['summary'] = jit_summary(text, inventory)
                            row['testInventorySha256'] = config['jitInventory']['sha256']
                        finally:
                            after = runtime_identity(entries[0].parent, native, assets['dlls'])
                            row.update(runtimeBefore=before, runtimeAfter=after)
                            if before != after:
                                raise ValueError('JIT runtime changed during execution')
                    else:
                        run('restore-aot-' + mode, ['dotnet', 'msbuild', app, '-t:Restore', '-p:Configuration=Release',
                            '-p:RuntimeIdentifier=osx-arm64', '-p:SelfContained=true', '-p:PublishAot=true',
                            '-p:ArtifactsPath=' + str(artifact), '-p:UseArtifactsOutput=true', '-m:1', '/nr:false', *common], root, env=env)
                        assets = graph(artifact, 'Lifecycle', mode, cache, config)
                        output = root / ('publish-' + mode)
                        text = run('publish-aot-' + mode, ['dotnet', 'publish', app, '-c', 'Release', '-r', 'osx-arm64',
                            '--self-contained', 'true', '--no-restore', '-m:1', '/nr:false', '--artifacts-path', artifact,
                            '-bl:' + str(root / ('aot-' + mode + '.binlog')), '-o', output, *common], root, timeout=1800, env=env)
                        strict_publish(text)
                        binary = output / 'Ranvier.View.WebCore.Tests'
                        if run('aot-arch-' + mode, ['lipo', '-archs', binary], root) != 'arm64':
                            raise ValueError('Wrong AOT architecture')
                        loads = run('aot-loads-' + mode, ['otool', '-L', binary], root)
                        if 'coreclr' in loads.lower() or any(p.name.lower().startswith(('libcoreclr', 'system.private.corelib')) for p in output.iterdir()):
                            raise ValueError('AOT route contains a CLR runtime')
                        before = runtime_identity(output, native)
                        try:
                            text = run('run-aot-' + mode, [binary], output, env=env)
                            if 'DYNAMIC_CODE_SUPPORTED=False' not in text or 'LIFECYCLE_PASS host=7 window=35 common=49' not in text:
                                raise ValueError('Actual complete common lifecycle/AOT verdict missing')
                            row['summary'] = {'host': 7, 'window': 35, 'common': 49, 'dynamicCodeSupported': False}
                        finally:
                            after = runtime_identity(output, native)
                            row.update(runtimeBefore=before, runtimeAfter=after)
                            if before != after:
                                raise ValueError('AOT runtime changed during execution')
                    row.update(status='PASS', graph=assets)
                except Exception as error:
                    row.update(status='FAIL', error=repr(error), traceback=traceback.format_exc())
                save()
        record['status'] = 'PASS' if len(record['routes']) == len(modes) * 2 and all(row['status'] == 'PASS' for row in record['routes']) else 'FAIL'
    except BaseException as error:
        record.update(status='FAIL', error=repr(error), traceback=traceback.format_exc())
        raise
    finally:
        try:
            record['nativeUnchanged'] = bool(native) and {name: sha(stage / name) for name in native} == native
            record['sourceUnchanged'] = source_identity(view, config['viewCommit']) == config['viewCommit']
            record['controllerUnchanged'] = source_identity(HERE, record['controllerCommit']) == record['controllerCommit']
            record['inputsUnchanged'] = bool(sources) and all(sha(name) == digest for name, digest in sources.items())
            record['allChildrenReaped'] = all(row.get('reaped') for row in record['commands'])
            if not all(record.get(name) for name in ['nativeUnchanged', 'sourceUnchanged', 'controllerUnchanged', 'inputsUnchanged', 'allChildrenReaped']):
                record['status'] = 'FAIL'
        except Exception as error:
            record.update(status='FAIL', finalIdentityError=repr(error))
        save()
        try:
            collect(root, config, record)
        except Exception as error:
            record.update(status='FAIL', evidenceCollectionError=repr(error))
            save()
            raise
    return int(record['status'] != 'PASS')


if __name__ == '__main__':
    raise SystemExit(main())

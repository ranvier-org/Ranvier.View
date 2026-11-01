import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import signal
import subprocess
import time
import urllib.request


ROOT = Path(__file__).resolve().parent
SDK = '10.0.403'
PATCHED_SOURCE_SHA256 = 'fdd4a841175a87f2ec4ea4e849a79f333f97fb155af1e76ecde7a9229fe407f2'
EXPRESSION_SOURCE = 'src/Compiler/Symbols/Exprs.fs'


def sha256(content):
    return hashlib.sha256(content).hexdigest()


def git_blob(content):
    return hashlib.sha1(b'blob ' + str(len(content)).encode() + b'\0' + content).hexdigest()


def destination(root, name):
    relative = PurePosixPath(name)
    if relative.is_absolute() or '..' in relative.parts or '\\' in name or ':' in name:
        raise ValueError(f'Invalid pinned source path: {name}')
    target = (root / name).resolve()
    if not target.is_relative_to(root.resolve()):
        raise ValueError(f'Source path escapes its directory: {name}')
    return target


def fetch_source(source, manifest):
    def fetch(item):
        name, expected = item
        target = destination(source, name)
        if target.is_file():
            content = target.read_bytes()
            if git_blob(content) == expected or (name == EXPRESSION_SOURCE and sha256(content) == PATCHED_SOURCE_SHA256):
                return
            raise ValueError(f'Existing source differs from its pin: {name}')
        url = f'https://raw.githubusercontent.com/dotnet/dotnet/{manifest["pin"]}/src/fsharp/{name}'
        request = urllib.request.Request(url, headers={'User-Agent': 'Ranvier.Analyzers.Host source producer'})
        with urllib.request.urlopen(request, timeout=60) as response:
            content = response.read(32 * 1024 * 1024 + 1)
        if git_blob(content) != expected:
            raise ValueError(f'Downloaded source differs from its Git blob: {name}')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(fetch, manifest['gitBlobs'].items()))


def apply_guard(source, spec, manifest):
    patch = (ROOT / 'compiler.patch').read_bytes()
    if sha256(patch) != spec['sourceGuardSha256']:
        raise ValueError('Compiler patch differs from its reviewed pin.')
    target = destination(source, EXPRESSION_SOURCE)
    content = target.read_bytes()
    if sha256(content) == PATCHED_SOURCE_SHA256:
        return
    if git_blob(content) != manifest['gitBlobs'][EXPRESSION_SOURCE]:
        raise ValueError('Compiler expression source differs from its original Git blob.')
    lines = patch.decode().splitlines(keepends=True)
    hunks = [index for index, line in enumerate(lines) if line.startswith('@@ ')]
    if len(hunks) != 1:
        raise ValueError('Expected the reviewed single source-guard hunk.')
    body = lines[hunks[0] + 1:]
    old = ''.join(line[1:] for line in body if line.startswith((' ', '-'))).encode()
    new = ''.join(line[1:] for line in body if line.startswith((' ', '+'))).encode()
    if content.count(old) != 1:
        raise ValueError('Source-guard context is not unique.')
    patched = content.replace(old, new, 1)
    if sha256(patched) != PATCHED_SOURCE_SHA256:
        raise ValueError('Patched expression source differs from its reviewed bytes.')
    target.write_bytes(patched)


def sdk_path(dotnet):
    output = subprocess.check_output([dotnet, '--list-sdks'], text=True)
    for line in output.splitlines():
        match = re.fullmatch(re.escape(SDK) + r' \[(.+)\]', line.strip())
        if match:
            return Path(match.group(1)) / SDK
    raise ValueError(f'The compiler producer requires .NET SDK {SDK}.')


def build(source, work, dotnet, spec):
    sdk = sdk_path(dotnet)
    environment = dict(os.environ, MSBuildSDKsPath=str(sdk / 'Sdks'), DOTNET_HOST_PATH=dotnet,
                       DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1',
                       DOTNET_GCHeapHardLimit='100000000')
    properties = ['Configuration=Release', 'BUILDING_USING_DOTNET=true', 'DISABLE_ARCADE=true',
                  'FSHARPCORE_USE_PACKAGE=true', 'FSharpCoreShippedPackageVersionValue=10.1.402',
                  'SKIP_NETCURRENT_FSC_BUILD=true', 'EnablePublishReadyToRun=false', 'IgnoreMibc=true',
                  'GenerateDocumentationFile=false', 'UseSharedCompilation=false',
                  f'SourceRevisionId={spec["compilerSourceCommit"]}',
                  'EnableXlfLocalization=false', 'UpdateXlfOnBuild=false']
    command = [dotnet, str(sdk / 'MSBuild.dll'), 'src/Compiler/FSharp.Compiler.Service.fsproj',
               '-restore', '-t:Build', '-m:1', '-nodeReuse:false', '-v:minimal',
               *['-p:' + value for value in properties]]
    started = time.monotonic()
    receipt = {'command': command, 'sdk': SDK, 'sourceCommit': spec['compilerSourceCommit'],
               'patchSha256': spec['sourceGuardSha256'], 'sourceSha256': PATCHED_SOURCE_SHA256}
    process = None
    try:
        with (work / 'compiler-build.log').open('w', encoding='utf-8') as log:
            process = subprocess.Popen(command, cwd=source, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                       start_new_session=os.name != 'nt',
                                       creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0)
            try:
                receipt['exit'] = process.wait(timeout=540)
            except BaseException:
                if os.name == 'nt':
                    try:
                        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                                       capture_output=True, check=True, timeout=10)
                    except (OSError, subprocess.SubprocessError) as error:
                        receipt['processTreeTerminationError'] = str(error)
                        if process.poll() is None:
                            process.kill()
                        process.wait(timeout=10)
                        raise RuntimeError('The compiler process-tree termination failed; descendant cleanup is unproven.') from error
                else:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                process.wait(timeout=10)
                raise
        if receipt['exit'] != 0:
            raise RuntimeError(f'Compiler build failed; see {work / "compiler-build.log"}')
        compiler = source / 'artifacts/bin/FSharp.Compiler.Service/Release/netstandard2.0/FSharp.Compiler.Service.dll'
        receipt['compilerPath'] = str(compiler)
        receipt['compilerSha256'] = sha256(compiler.read_bytes())
        receipt['matchesReviewedCompiler'] = receipt['compilerSha256'] == spec['compilerSha256']
        if not receipt['matchesReviewedCompiler']:
            raise ValueError('Fresh compiler output differs from the reviewed DLL. Review the source-build receipt before changing the vendor pin; host packaging is blocked.')
        return compiler
    finally:
        receipt['seconds'] = round(time.monotonic() - started, 3)
        (work / 'compiler-build.json').write_text(json.dumps(receipt, indent=2) + '\n')


def main():
    parser = argparse.ArgumentParser(description='Build the pinned source-patched compiler without cloning the VMR or changing an SDK/tool cache.')
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--prepare-only', action='store_true')
    arguments = parser.parse_args()
    spec = json.loads((ROOT / 'vendor-spec.json').read_text())
    manifest = json.loads((ROOT / 'compiler-sources.json').read_text())
    if manifest['pin'] != spec['compilerSourceCommit'] or len(manifest['gitBlobs']) != 754 or manifest['sourceBytes'] != 24333291:
        raise ValueError('The bounded source manifest differs from the reviewed compiler source export.')
    work = arguments.work.resolve()
    source = work / 'source'
    source.mkdir(parents=True, exist_ok=True)
    fetch_source(source, manifest)
    apply_guard(source, spec, manifest)
    (work / 'compiler-source-receipt.json').write_text(json.dumps({
        'sourceCommit': manifest['pin'], 'sourceManifestSha256': sha256((ROOT / 'compiler-sources.json').read_bytes()),
        'files': len(manifest['gitBlobs']), 'sourceBytes': manifest['sourceBytes'],
        'patchSha256': spec['sourceGuardSha256'], 'patchedSourceSha256': PATCHED_SOURCE_SHA256,
    }, indent=2) + '\n')
    if not arguments.prepare_only:
        dotnet = shutil.which(arguments.dotnet)
        if not dotnet:
            raise ValueError(f'Cannot locate dotnet: {arguments.dotnet}')
        print(build(source, work, dotnet, spec))


if __name__ == '__main__':
    main()

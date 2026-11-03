"""Admission and process supervision for the public Slim Mac consumer."""
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import time
import zipfile

PUBLIC = 'https://api.nuget.org/v3/index.json'
NATIVE = {'libJavaScriptCore.dylib', 'libRanvierWebCoreHost.dylib', 'libWebCore.dylib', 'libWebGPU.dylib'}


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def atomic_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def source_identity(view, commit):
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=view, text=True).strip()
    status = subprocess.check_output(['git', 'status', '--porcelain'], cwd=view, text=True).strip()
    if head != commit or status:
        raise ValueError('Actual View source HEAD/clean state mismatch')
    return head


def core_graph(data, mode, config):
    libraries = data['libraries']
    suffix = '.Traced' if mode == 'traced' else ''
    wanted = {'ranvier' + suffix.lower(): config['coreVersion'],
              'ranvier.dom' + suffix.lower(): config['domVersion'],
              'ranvier.webcore': config['interopVersion']}
    found = {}
    for key, row in libraries.items():
        name, version = key.rsplit('/', 1)
        lower = name.lower()
        if lower in {'ranvier', 'ranvier.traced', 'ranvier.dom', 'ranvier.dom.traced', 'ranvier.webcore'}:
            if lower not in wanted or version != wanted[lower] or row['type'] != 'package' or lower in found:
                raise ValueError('Mixed, source-backed or stale public dependency: ' + key)
            found[lower] = key
        if lower.startswith('ranvier.view') and row['type'] != 'project':
            raise ValueError('View qualification must compile the current source: ' + key)
        if lower.startswith('ranvier.webcore.native'):
            raise ValueError('Use the one independently pinned public native stage')
    if found.keys() != wanted.keys():
        raise ValueError('Missing public Core/DOM/Interop dependency')
    if [key.lower() for key in libraries if key.lower().startswith('fsharp.core/')] != ['fsharp.core/' + config['fsharpCore']]:
        raise ValueError('Unexpected FSharp.Core graph')
    if not {'ranvier.view.webcore', 'ranvier.view.webcore.sdl'} <= {key.rsplit('/', 1)[0].lower() for key in libraries}:
        raise ValueError('Missing current source providers')
    return found


def managed_packages(data, mode, cache, config):
    found = core_graph(data, mode, config)
    rows, dlls = [], {}
    for lower, key in found.items():
        folder = (cache / data['libraries'][key]['path']).resolve(strict=True)
        if not folder.is_relative_to(cache.resolve()):
            raise ValueError('Package cache path escapes the owned cache')
        package = folder / (key.replace('/', '.').lower() + '.nupkg')
        metadata = json.loads((folder / '.nupkg.metadata').read_text())
        if metadata['source'] != PUBLIC:
            raise ValueError('Ranvier dependency did not restore from public NuGet')
        digest = sha(package)
        expected = config['interopPackageSha256'] if lower == 'ranvier.webcore' else (
            config['domPackageSha256'][mode] if lower.startswith('ranvier.dom') else None)
        if expected and expected != digest:
            raise ValueError('Public package identity mismatch: ' + key)
        dll = 'Ranvier.WebCore.dll' if lower == 'ranvier.webcore' else 'Ranvier.Dom.dll' if lower.startswith('ranvier.dom') else 'Ranvier.dll'
        with zipfile.ZipFile(package) as zipped:
            member = 'lib/net10.0/' + dll
            if member not in zipped.namelist():
                raise ValueError('Missing admitted managed runtime asset: ' + member)
            dlls[dll] = hashlib.sha256(zipped.read(member)).hexdigest()
        rows.append({'id': key, 'package': str(package), 'sha256': digest, 'metadata': metadata, 'runtimeAsset': member})
    return {'packages': rows, 'dlls': dlls}


def stage_native(package, destination, config):
    if sha(package) != config['native']['sha256']:
        raise ValueError('Public P11 native package identity mismatch')
    with zipfile.ZipFile(package) as zipped:
        manifests = [name for name in zipped.namelist() if name.endswith('manifest.json')]
        if len(manifests) != 1:
            raise ValueError('Ambiguous native manifest')
        manifest = json.loads(zipped.read(manifests[0]))
        if manifest != config['native']['manifest'] or (manifest['abi'], manifest['rid'], manifest['flavor']) != (63, 'osx-arm64', 'slim'):
            raise ValueError('Wrong public native contract')
        if len(manifest['files']) != 4 or {row['name'] for row in manifest['files']} != NATIVE:
            raise ValueError('Wrong native file closure')
        payloads = {}
        for row in manifest['files']:
            data = zipped.read('runtimes/osx-arm64/native/' + row['name'])
            if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
                raise ValueError('Native payload differs from manifest')
            payloads[row['name']] = data
    destination.mkdir(parents=True, exist_ok=False)
    for name, data in payloads.items():
        (destination / name).write_bytes(data)
    return {row['name']: row['sha256'] for row in manifest['files']}


def runtime_identity(directory, native, managed=None):
    result = {'native': {name: sha(directory / name) for name in native}}
    if result['native'] != native:
        raise ValueError('Runtime differs from public P11 payload')
    if managed:
        result['managed'] = {name: sha(directory / name) for name in managed}
        if result['managed'] != managed:
            raise ValueError('Runtime does not use the public package DLLs')
    result['files'] = {p.name: sha(p) for p in directory.iterdir() if p.is_file()}
    return result


def jit_summary(text):
    text = re.sub(r'\x1b\[[0-9;?]*[A-Za-z]', '', text)
    rows = re.findall(r'(\d+) passed,\s*(\d+) ignored,\s*(\d+) failed,\s*(\d+) errored', text)
    if len(rows) != 1:
        raise ValueError('Missing/ambiguous actual unfiltered test summary')
    counts = list(map(int, rows[0]))
    if counts[0] <= 0 or counts[2:] != [0, 0]:
        raise ValueError('Unfiltered suite did not pass: ' + repr(counts))
    return counts


def strict_publish(text):
    diagnostics = [line for line in text.splitlines() if re.search(r'\b(?:warning|error) IL\d+', line, re.I)]
    if diagnostics or 'Generating native code' not in text or 'will always throw because:' in text:
        raise ValueError('Strict NativeAOT generation not proven: ' + repr(diagnostics))


class Runner:
    def __init__(self, root, record, save, environment):
        self.root, self.record, self.save, self.environment = root, record, save, environment

    def __call__(self, name, command, cwd, timeout=900, env=None):
        row = {'name': name, 'argv': [str(value) for value in command], 'cwd': str(cwd), 'supervisorLimitTriggered': None}
        self.record['commands'].append(row)
        self.save()
        log = self.root / (name + '.log')
        with log.open('wb') as stream:
            child = subprocess.Popen(row['argv'], cwd=cwd, env=env or self.environment,
                                     stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
            row['pid'] = child.pid
            started, peak = time.monotonic(), 0
            try:
                while child.poll() is None:
                    census = subprocess.check_output(['ps', '-axo', 'pgid=,rss='], text=True)
                    rss = sum(int(fields[1]) * 1024 for line in census.splitlines()
                              if len(fields := line.split()) == 2 and int(fields[0]) == child.pid)
                    peak = max(peak, rss)
                    if rss > 8 * 1024 ** 3 or time.monotonic() - started >= timeout:
                        row['supervisorLimitTriggered'] = 'memory' if rss > 8 * 1024 ** 3 else 'timeout'
                        raise RuntimeError(name + ': ' + row['supervisorLimitTriggered'])
                    time.sleep(.25)
                row['exit'] = child.returncode
            finally:
                row['childExitBeforeCleanup'] = child.poll()
                try:
                    os.killpg(child.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                child.wait(timeout=10)
                row.update(reaped=child.poll() is not None, elapsedSeconds=time.monotonic() - started,
                           peakGroupRssBytes=peak, logSha256=sha(log))
                self.save()
        text = log.read_text(encoding='utf-8', errors='replace')
        if row['exit']:
            raise RuntimeError(name + ': exit ' + str(row['exit']) + '; log retained')
        return text.strip()

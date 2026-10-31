"""Writes Ranvier.View docs/research/native-api-delivery/blob-urls-runtime-evidence.json for the #144 private integration.
Usage: make_blob_urls_evidence.py <managed-run-label> <native candidate> <stacked-on core note> <stacked-on views note>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

views = Path('C:/wk/rv-blob')
managed = Path('C:/wk/blob-artifacts/managed') / sys.argv[1]
feed = Path('C:/wk/blob-local-feed')
runtime = Path('C:/wk/native-blob/artifacts') / (sys.argv[2] + '-frozen') / 'runtimes/win-x64/native'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def head(repo):
    return subprocess.run(['git', '-C', repo, 'rev-parse', 'HEAD'], capture_output=True, text=True, check=True).stdout.strip()


def run(log, prefix):
    text = Path(log).read_text(encoding='utf-8', errors='replace')
    lines = sorted(set(re.findall(rf'^{prefix}[^\r\n]*PASS[^\r\n]*', text, re.M)))
    exit_code = int(re.search(r'_EXIT=(\d+)', text).group(1))
    return {'exitCode': exit_code, 'logSha256': sha(log), 'passLines': len(lines),
            'failLines': re.findall(rf'^{prefix}[^\r\n]*FAIL[^\r\n]*', text, re.M),
            'b144': [line for line in lines if ' B144 ' in line]}, lines


jit, jit_lines = run(managed / 'jit.log', 'NATIVE_API')
aot, aot_lines = run(managed / 'aot.log', 'NATIVE_API')
chromium, _ = run(managed / 'chromium.log', 'BROWSER_API')
evidence = {
    'schemaVersion': 1,
    'group': '#144 blob URLs, blob: loading and FileReader',
    'status': 'green (private integration; not accepted)',
    'route': 'Engine object URLs, blob: loads in WebCore, engine blob reads and FileReader (Native44 / ABI52)',
    'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/144',
    'coreSourceCommit': head('C:/wk/rv-blob-core'),
    'viewsSourceCommit': head(views),
    'stackedOn': {'core': sys.argv[3], 'views': sys.argv[4]},
    'nativeProducer': {'branch': 'codex/native-api-blob-urls', 'head': head('C:/wk/native-blob'), 'abi': 52,
                       'runtime': 'native-blob artifacts/' + sys.argv[2] + '-frozen',
                       'evidence': 'Ranvier.WebCore docs/blob-urls-evidence/final-evidence.json',
                       'runtimeSourceHead': json.loads((runtime.parents[2] / 'runtime-receipt.json').read_text())['sourceHead']},
    'versions': {'native': '0.1.0-preview.44', 'dom': '0.1.0-alpha.47', 'views': '0.1.37', 'webcoreBackend': '0.1.0-alpha.37'},
    'allocationReceipt': 'Ranvier docs/research/native-api-delivery/blob-managed47-allocation.json',
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.iterdir()) if p.is_file()},
    'nativeAotExecutable': {'path': str(managed / 'aot/Native.exe'), 'sha256': sha(managed / 'aot/Native.exe')},
    'runs': {'native-jit-full': jit, 'native-aot-full': aot, 'chromium-full': chromium},
    'jitAotPassLinesIdentical': [l.split('=')[0] for l in jit_lines] == [l.split('=')[0] for l in aot_lines],
    'repeats': {'aotBlobUrlsOnly': (managed / 'aot-blob-urls-repeat30.txt').read_text().strip(),
                'chromiumBlobUrlsOnly': (managed / 'chromium-blob-urls-repeat30.txt').read_text().strip()},
    'wrapperRegeneration': {'objectsFs': sha(views / 'src/Ranvier.View.WebCore/Generated/Objects.fs'),
                            'reportTxt': sha(views / 'src/Ranvier.View.WebCore/Generated/report.txt'),
                            'result': 'identical to the generator output from preview.44 and alpha.47 (FileReaderObject, Kind 98)'},
    'fixture': {'shared': 'tests/NativeApi.Conformance/BlobUrlChecks.fs', 'nativeChecks': 42, 'chromiumChecks': 42, 'nativeLifetimeChecks': 11},
    'qualifications': [
        'readAsText without an encoding decodes with the blob type charset in WebKit (File API) and UTF-8 in Chromium; the fixture asserts each engine.',
        'A read in flight at document replacement is Invalidated natively (H05); Chromium leaves the removed document promise pending.',
        'Object URLs carry the document origin: blob:null for the native opaque documents, blob:http://127.0.0.1:<port> in the Chromium fixture page.',
        'An iframe loading a blob URL is browser-only: the native host has no subframes.',
        'BlobUrls.createFile (file-backed File from an app-supplied absolute path) is native-only.',
    ],
}
out = views / 'docs/research/native-api-delivery/blob-urls-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(jit['passLines'], aot['passLines'], chromium['passLines'], evidence['jitAotPassLinesIdentical'], sha(out))

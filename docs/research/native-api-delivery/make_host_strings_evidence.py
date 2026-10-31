"""Writes Ranvier.View docs/research/native-api-delivery/host-strings-runtime-evidence.json for the #145 private integration.
Usage: make_host_strings_evidence.py <managed-run-label> <native candidate> <stacked-on core note> <stacked-on views note>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

views = Path('C:/wk/rv-strings2')
managed = Path('C:/wk/str2-artifacts/managed') / sys.argv[1]
feed = Path('C:/wk/str2-local-feed')
runtime = Path('C:/wk/native-strings2/artifacts') / (sys.argv[2] + '-frozen') / 'runtimes/win-x64/native'


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
            'wc145': [line for line in lines if ' WC145 ' in line]}, lines


jit, jit_lines = run(managed / 'jit.log', 'NATIVE_API')
aot, aot_lines = run(managed / 'aot.log', 'NATIVE_API')
chromium, _ = run(managed / 'chromium.log', 'BROWSER_API')
evidence = {
    'schemaVersion': 1,
    'group': '#145 hand-written native string inputs with byte lengths',
    'status': 'green (private integration; not accepted)',
    'route': 'Every free-text input of the hand-written native header and RvClipboardWrite data carry a UTF-8 byte count '
             '(Native47 / ABI55 placeholders; root renumbers at batch integration)',
    'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/145',
    'coreSourceCommit': head('C:/wk/rv-strings2-core'),
    'viewsSourceCommit': head(views),
    'stackedOn': {'core': sys.argv[3], 'views': sys.argv[4]},
    'nativeProducer': {'branch': 'codex/native-api-host-string-inputs', 'head': head('C:/wk/native-strings2'), 'abi': 55,
                       'runtime': 'native-strings2 artifacts/' + sys.argv[2] + '-frozen',
                       'evidence': 'Ranvier.WebCore docs/host-string-input-evidence/final-evidence.json',
                       'runtimeSourceHead': json.loads((runtime.parents[2] / 'runtime-receipt.json').read_text())['sourceHead']},
    'versions': {'native': '0.1.0-preview.47', 'dom': '0.1.0-alpha.50', 'views': '0.1.40', 'webcoreBackend': '0.1.0-alpha.40',
                 'note': 'placeholders under pipeline v2'},
    'allocationReceipt': 'Ranvier docs/research/native-api-delivery/str2-managed50-allocation.json (released reservation)',
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.iterdir()) if p.is_file()},
    'nativeAotExecutable': {'path': str(managed / 'aot/Native.exe'), 'sha256': sha(managed / 'aot/Native.exe')},
    'runs': {'native-jit-full': jit, 'native-aot-full': aot, 'chromium-full': chromium},
    'baseline': {'run': 'C:/wk/cascade-artifacts/managed-c25/final-1 (C25 f06cb30)', 'jit': 119, 'aot': 119, 'chromium': 86,
                 'superset': json.loads((managed / 'superset.json').read_text())},
    'jitAotPassLinesIdentical': [l.split('=')[0] for l in jit_lines] == [l.split('=')[0] for l in aot_lines],
    'repeats': {'jitHostStringsOnly': (managed / 'jit-wc145-repeat30.txt').read_text().strip(),
                'aotHostStringsOnly': (managed / 'aot-wc145-repeat30.txt').read_text().strip(),
                'chromiumHostStringsOnly': (managed / 'chromium-wc145-repeat30.txt').read_text().strip()},
    'wrapperRegeneration': {'objectsFs': sha(views / 'src/Ranvier.View.WebCore/Generated/Objects.fs'),
                            'result': 'not regenerated: the native generated DOM (gen/, RanvierWebCoreDom.h) equals C25 f13cdbb'},
    'fixture': {'shared': 'tests/NativeApi.Conformance/HostStringChecks.fs', 'nativeChecks': 5, 'chromiumChecks': 5,
                'nativeHostChecks': 5, 'nativeLifetimeChecks': 1},
    'qualifications': [
        'Key text, composition commit, clipboard paste and copy, and SVG markup are native host inputs without a browser equivalent (WC145 HOST).',
        'Copied data carries no U+0000: WebKit drops it from the selected text and markup, and page-supplied data needs ClipboardEvent.clipboardData and DataTransfer from the clipboard and drag groups; the copy check uses multi-byte text.',
    ],
}
out = views / 'docs/research/native-api-delivery/host-strings-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(jit['passLines'], aot['passLines'], chromium['passLines'], evidence['jitAotPassLinesIdentical'], sha(out))

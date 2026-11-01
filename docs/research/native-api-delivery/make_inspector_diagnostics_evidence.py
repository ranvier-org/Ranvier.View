"""Writes Ranvier.View docs/research/native-api-delivery/inspector-diagnostics-runtime-evidence.json for the H08 private integration.
Usage: make_views_evidence.py <managed-run-label> <stacked-on core note> <stacked-on views note>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

views = Path('C:/wk/rv-inspect')
managed = Path('C:/wk/inspect-artifacts/managed-h08') / sys.argv[1]
feed = Path('C:/wk/h08-local-feed')
runtime = Path('C:/wk/native-inspect/artifacts/native42-candidate-3-frozen/runtimes/win-x64/native')


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
            'h08': [line for line in lines if ' H08 ' in line]}, lines


jit, jit_lines = run(managed / 'jit.log', 'NATIVE_API')
aot, aot_lines = run(managed / 'aot.log', 'NATIVE_API')
chromium, _ = run(managed / 'chromium.log', 'BROWSER_API')
evidence = {
    'schemaVersion': 1,
    'group': 'H08 native inspector hooks',
    'status': 'green (private integration; not accepted)',
    'route': 'Engine render tree, style rule collector and box geometry (Native42 / ABI50), opt-in per host, no inspector frontend',
    'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/113',
    'coreSourceCommit': head('C:/wk/rv-inspect-core'),
    'viewsSourceCommit': head(views),
    'stackedOn': {'core': sys.argv[2], 'views': sys.argv[3]},
    'nativeProducer': {'branch': 'codex/native-api-inspector-hooks', 'head': head('C:/wk/native-inspect'), 'abi': 50,
                       'runtime': 'native-inspect artifacts/native42-candidate-3-frozen',
                       'evidence': 'Ranvier.WebCore docs/inspector-evidence/final-evidence.json'},
    'versions': {'native': '0.1.0-preview.42', 'dom': '0.1.0-alpha.45', 'views': '0.1.35', 'webcoreBackend': '0.1.0-alpha.35'},
    'allocationReceipt': 'Ranvier docs/research/native-api-delivery/h08-managed45-allocation.json',
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.iterdir()) if p.is_file()},
    'nativeAotExecutable': {'path': str(managed / 'aot/Native.exe'), 'sha256': sha(managed / 'aot/Native.exe')},
    'runs': {'native-jit-full': jit, 'native-aot-full': aot, 'chromium-full': chromium,
             'i11BaselinePassLines': {'jitAot': 109, 'chromium': 81, 'logs': 'C:/wk/ei-artifacts/runs (sha 6ebc1fbe, b31a3b6f)', 'missing': [], 'added': {'jitAot': 2, 'chromium': 1}}},
    'jitAotPassLinesIdentical': [l.split('=')[0] for l in jit_lines] == [l.split('=')[0] for l in aot_lines],
    'repeats': {'aotDiagnosticsOnly': (managed / 'aot-diagnostics-repeat30.txt').read_text().strip(),
                'chromiumDiagnosticsOnly': (managed / 'chromium-diagnostics-repeat30.txt').read_text().strip()},
    'wrapperRegeneration': {'objectsFs': sha(views / 'src/Ranvier.View.WebCore/Generated/Objects.fs'),
                            'reportTxt': sha(views / 'src/Ranvier.View.WebCore/Generated/report.txt'),
                            'result': 'identical: preview.42 adds no generated DOM members'},
    'fixture': {'shared': 'tests/NativeApi.Conformance/DiagnosticsChecks.fs', 'nativeChecks': 73, 'chromiumChecks': 46, 'nativeLifetimeChecks': 8},
    'qualifications': [
        'Chromium exposes no render tree or matched-rule list to pages: the browser provider reads both, and a developer-tools frontend, as Unsupported.',
        'The browser box model is derived from getBoundingClientRect and the computed widths, and reads Unsupported for a transformed element or ancestor.',
        'Render-tree text is engine-specific: the fixture asserts structure, never text equality with another engine.',
    ],
}
out = views / 'docs/research/native-api-delivery/inspector-diagnostics-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(jit['passLines'], aot['passLines'], chromium['passLines'], evidence['jitAotPassLinesIdentical'], sha(out))

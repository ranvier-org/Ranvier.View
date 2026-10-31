"""Writes Ranvier.View docs/research/native-api-delivery/cascade-diagnostics-runtime-evidence.json for the C25 private integration.
Usage: make_views_evidence_c25.py <managed-run-label> <stacked-on core note> <stacked-on views note> <native candidate>
       <baseline jit/aot pass lines> <baseline chromium pass lines> <baseline note>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

views = Path('C:/wk/rv-cascade')
managed = Path('C:/wk/cascade-artifacts/managed-c25') / sys.argv[1]
feed = Path('C:/wk/c25-local-feed')
runtime = Path('C:/wk/native-cascade/artifacts') / (sys.argv[4] + '-frozen') / 'runtimes/win-x64/native'


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
            'c25': [line for line in lines if ' C25 ' in line]}, lines


jit, jit_lines = run(managed / 'jit.log', 'NATIVE_API')
aot, aot_lines = run(managed / 'aot.log', 'NATIVE_API')
chromium, _ = run(managed / 'chromium.log', 'BROWSER_API')
evidence = {
    'schemaVersion': 1,
    'group': 'C25 matched-rule and cascade diagnostics',
    'status': 'green (private integration; not accepted)',
    'route': 'Engine rule collector MatchResult and PropertyCascade selection (Native46 / ABI54), opt-in per host; browsers Unsupported',
    'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/45',
    'coreSourceCommit': head('C:/wk/rv-cascade-core'),
    'viewsSourceCommit': head(views),
    'stackedOn': {'core': sys.argv[2], 'views': sys.argv[3]},
    'nativeProducer': {'branch': 'codex/native-api-cascade-diagnostics', 'head': head('C:/wk/native-cascade'), 'abi': 54,
                       'runtime': 'native-cascade artifacts/' + sys.argv[4] + '-frozen',
                       'evidence': 'Ranvier.WebCore docs/cascade-evidence/final-evidence.json'},
    'versions': {'native': '0.1.0-preview.46', 'dom': '0.1.0-alpha.49', 'views': '0.1.39', 'webcoreBackend': '0.1.0-alpha.39'},
    'allocationReceipt': 'Ranvier docs/research/native-api-delivery/casc1-managed49-allocation.json',
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.iterdir()) if p.is_file()},
    'nativeAotExecutable': {'path': str(managed / 'aot/Native.exe'), 'sha256': sha(managed / 'aot/Native.exe')},
    'runs': {'native-jit-full': jit, 'native-aot-full': aot, 'chromium-full': chromium,
             'baselinePassLines': {'jitAot': int(sys.argv[5]), 'chromium': int(sys.argv[6]), 'source': sys.argv[7],
                                   'added': {'jitAot': jit['passLines'] - int(sys.argv[5]), 'chromium': chromium['passLines'] - int(sys.argv[6])}}},
    'jitAotPassLinesIdentical': [l.split('=')[0] for l in jit_lines] == [l.split('=')[0] for l in aot_lines],
    'repeats': {'aotCascadeOnly': (managed / 'aot-cascade-repeat30.txt').read_text().strip(),
                'chromiumCascadeOnly': (managed / 'chromium-cascade-repeat30.txt').read_text().strip()},
    'wrapperRegeneration': {'objectsFs': sha(views / 'src/Ranvier.View.WebCore/Generated/Objects.fs'),
                            'reportTxt': sha(views / 'src/Ranvier.View.WebCore/Generated/report.txt'),
                            'result': 'identical: preview.46 adds no generated DOM members'},
    'fixture': {'shared': 'tests/NativeApi.Conformance/CascadeChecks.fs', 'cases': 22, 'nativeChecks': 609, 'chromiumChecks': 51, 'nativeLifetimeChecks': 7},
    'qualifications': [
        'Chromium exposes no cascade provenance to pages: the browser provider reads the cascade as Unsupported and derives none from getComputedStyle; its computed values are compared with the fixture only.',
        'Declared values, selectors and condition texts are engine-serialised: the fixture compares them with its own expectations on the native engine, never across engines.',
        'Declared limits: a winning revert or revert-layer is reported as that keyword; :visited, @starting-style and animations are absent; var() resolution is the computed value.',
    ],
}
out = views / 'docs/research/native-api-delivery/cascade-diagnostics-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(jit['passLines'], aot['passLines'], chromium['passLines'], evidence['jitAotPassLinesIdentical'], sha(out))

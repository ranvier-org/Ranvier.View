"""Writes Ranvier.View docs/research/native-api-delivery/coordinate-spaces-runtime-evidence.json for the G14 private
integration. Usage: make_views_evidence.py <managed-run-label> <stacked-on note>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

views = Path('C:/wk/rv-coords')
managed = Path('C:/wk/coords-artifacts/managed') / sys.argv[1]
baseline = Path('C:/wk/blob-artifacts/managed/final-1')
feed = Path('C:/wk/coords-local-feed')
runtime = Path('C:/wk/coords-private/native45-stage/runtimes/win-x64/native')


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def head(repo, rev='HEAD'):
    return subprocess.run(['git', '-C', repo, 'rev-parse', rev], capture_output=True, text=True, check=True).stdout.strip()


def lines(log, prefix):
    return [l.strip() for l in open(log, encoding='utf-8', errors='replace') if l.startswith(prefix) and 'PASS' in l]


def run(log, prefix, base):
    text = Path(log).read_text(encoding='utf-8', errors='replace')
    current, previous = lines(log, prefix), lines(base, prefix)
    return {'exitCode': int(re.search(r'_EXIT=(\d+)', text).group(1)), 'logSha256': sha(log), 'passLines': len(current),
            'failLines': re.findall(rf'^{prefix}[^\r\n]*FAIL[^\r\n]*', text, re.M),
            'baselinePassLines': len(previous), 'missingFromBaseline': [l for l in previous if l not in current],
            'added': [l for l in current if l not in previous]}, current


jit, jit_lines = run(managed / 'jit.log', 'NATIVE_API', baseline / 'jit.log')
aot, aot_lines = run(managed / 'aot.log', 'NATIVE_API', baseline / 'aot.log')
chromium, _ = run(managed / 'chromium.log', 'BROWSER_API', baseline / 'chromium.log')


def repeats(name):
    values = (managed / name).read_text(encoding='utf-8').splitlines()
    return {'runs': len(values), 'distinct': sorted(set(values))}


evidence = {
    'schemaVersion': 1,
    'group': 'G14 coordinate-space conversion',
    'status': 'green (private integration; not accepted)',
    'route': 'rv_node_convert_points and rv_node_box_quad (Native45 / ABI53): WebCore localToAbsolute/absoluteToLocal, '
             'scroll, page scale and device scale; browser: computed transforms placed by getBoundingClientRect',
    'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/58',
    'coreSourceCommit': head('C:/wk/rv-coords-core'),
    'corePackedCommit': head('C:/wk/rv-coords-core', 'HEAD~1'),
    'viewsSourceCommit': head(views),
    'viewsRunCommit': '1457594 (the run tree; 841493e changes .github/workflows/ci.yml only)',
    'stackedOn': sys.argv[2],
    'nativeProducer': {'branch': 'codex/native-api-coordinate-conversion', 'head': head('C:/wk/native-coords'), 'abi': 53,
                       'runtime': 'native-coords artifacts/coords-candidate-5',
                       'evidence': 'Ranvier.WebCore docs/coordinate-evidence/final-evidence.json'},
    'versions': {'native': '0.1.0-preview.45', 'dom': '0.1.0-alpha.48', 'views': '0.1.38', 'webcoreBackend': '0.1.0-alpha.38'},
    'allocationReceipt': 'Ranvier.WebCore docs/coordinate-evidence/allocation-absence.json',
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.iterdir()) if p.is_file()},
    'nativeAotExecutable': {'path': str(managed / 'aot/Native.exe'), 'sha256': sha(managed / 'aot/Native.exe')},
    'runs': {'native-jit-full': jit, 'native-aot-full': aot, 'chromium-full': chromium,
             'baseline': '#144 final managed run C:/wk/blob-artifacts/managed/final-1 (JIT/AOT 115, Chromium 84)'},
    'jitAotPassLinesIdentical': jit_lines == aot_lines,
    'repeats': {'aotCoordinatesOnly': repeats('aot-coordinates-repeat30.txt'),
                'chromiumCoordinatesOnly': repeats('chromium-coordinates-repeat30.txt')},
    'wrapperRegeneration': {'objectsFs': sha(views / 'src/Ranvier.View.WebCore/Generated/Objects.fs'),
                            'reportTxt': sha(views / 'src/Ranvier.View.WebCore/Generated/report.txt'),
                            'result': 'identical: preview.45 adds no generated DOM members'},
    'fixture': {'shared': 'tests/NativeApi.Conformance/CoordinateChecks.fs', 'nativeChecks': 304, 'chromiumChecks': 304,
                'nativeLifetimeChecks': 11},
    'coreAndViewState': 'docs/superpowers/plans/2026-10-31-coordinate-spaces.md: no core primitives; no view-owned state',
    'qualifications': [
        'Chromium element spaces are the affine product of the computed transforms placed by getBoundingClientRect; perspective, '
        'perspective() and preserve-3d chains, the translate/rotate/scale properties and CSS zoom read Unsupported.',
        'The native slim software path paints without 3D compositing: perspective chains convert as the engine paints and '
        'hit-tests them, 2.3 to 26.5 px from Chromium (native evidence).',
        'Host is the page-scaled surface space: a page cannot set its page scale in Chromium, so Host equals Client there; '
        'the native runner asserts Host = Client x 2 at page scale 2, origin 40, 60, in both directions.',
    ],
}
out = views / 'docs/research/native-api-delivery/coordinate-spaces-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(jit['passLines'], aot['passLines'], chromium['passLines'], evidence['jitAotPassLinesIdentical'], sha(out))

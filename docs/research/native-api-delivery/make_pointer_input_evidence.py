"""Writes pointer-input-runtime-evidence.json for the I03/I04/I06 private integration from a managed run, the batch-1
Views baseline run and the 30x repeats. Usage: make_pointer_input_evidence.py <managed-run-label>"""
import hashlib, json, re, subprocess, sys
from pathlib import Path

views = Path(__file__).resolve().parents[3]
managed = Path('C:/wk/pointer-artifacts/managed') / sys.argv[1]
baseline = Path('C:/wk/int1-tools/runs/final-1')
runtime = Path('C:/wk/pointer-artifacts/stage/runtimes/win-x64/native')
feed = Path('C:/wk/pointer-local-feed')
sha = lambda path: hashlib.sha256(Path(path).read_bytes()).hexdigest()
head = lambda repo: subprocess.run(['git', '-C', str(repo), 'rev-parse', 'HEAD'], capture_output=True, text=True, check=True).stdout.strip()


def lines(log, prefix):
    return [l.strip() for l in open(log, encoding='utf-8', errors='replace') if l.startswith(prefix) and 'PASS' in l]


def run(name, prefix):
    log = managed / (name + '.log')
    text = log.read_text(encoding='utf-8', errors='replace')
    current, previous = lines(log, prefix), lines(baseline / (name + '.log'), prefix)
    return {'exitCode': int(re.search(r'_EXIT=(\d+)', text).group(1)), 'logSha256': sha(log), 'passLines': len(current),
            'baselinePassLines': len(previous), 'missingFromBaseline': [l for l in previous if l not in current],
            'added': [l for l in current if l not in previous]}


evidence = {
    'schemaVersion': 1,
    'groups': 'I03 touch payloads and injection, I04 pen/pointer fidelity, I06 relative pointer capture/lock',
    'status': 'green (private integration; not accepted)',
    'issues': ['https://github.com/ranvier-org/Ranvier.WebCore/issues/101', 'https://github.com/ranvier-org/Ranvier.WebCore/issues/102',
               'https://github.com/ranvier-org/Ranvier.WebCore/issues/103'],
    'viewsSourceCommit': head(views),
    'coreSourceCommit': head('C:/wk/rv-pointer-core'),
    'nativeProducer': {'branch': 'codex/native-api-pointer-input', 'head': head('C:/wk/native-pointer'), 'abi': '54 (placeholder)',
                       'runtime': 'C:/wk/pointer-artifacts/pointer-candidate-4 (full rebuild, ENABLE_TOUCH_EVENTS and ENABLE_POINTER_LOCK)',
                       'evidence': 'Ranvier.WebCore docs/pointer-input-evidence/final-evidence.json'},
    'versions': {'native': '0.1.0-preview.46 (private)', 'dom': '0.1.0-alpha.49 (private)'},
    'privatePackages': {p.name: sha(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: sha(p) for p in sorted(runtime.glob('*.dll'))},
    'jit': run('jit', 'NATIVE_API'),
    'aot': run('aot', 'NATIVE_API'),
    'chromium': run('chromium', 'BROWSER_API'),
    'fixture': {'shared': 61, 'nativeOnly': 13},
    'repeat30': (managed / 'repeat30/summary.txt').read_text().strip(),
    'qualifications': [
        'Chromium may dispatch pointerlockchange more than once for one grant; the fixture checks the distinct lock targets.',
        'Headless Chromium grants pointer lock without a gesture or embedder; the native host waits for resolveLock, and the gesture refusal and denial are native-only checks.',
        'Touch pointer ids are engine-assigned: native 2 + contact id (native-only check), Chromium its own; the shared fixture checks identity per contact.',
        'Kind.Touch 140 and Kind.TouchList 141 are placeholder release kinds for root renumbering.',
    ],
}
out = Path(__file__).resolve().parent / 'pointer-input-runtime-evidence.json'
out.write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: evidence[k]['passLines'] for k in ['jit', 'aot', 'chromium']}), evidence['repeat30'], sha(out))

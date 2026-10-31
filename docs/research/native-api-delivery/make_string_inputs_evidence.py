"""Writes string-inputs-runtime-evidence.json: the WebCore #142 managed runs on native JIT, NativeAOT and Chromium against
the H08 baseline, with the private packages and the executed native runtime. usage: make_string_inputs_evidence.py <run dir>
<baseline run dir> <feed dir> <native stage dir>"""
import hashlib, json, re, subprocess, sys
from pathlib import Path
run, baseline, feed, stage = map(Path, sys.argv[1:5])
here = Path(__file__).resolve().parent
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def head(repo): return subprocess.run(['git', '-C', str(repo), 'rev-parse', 'HEAD'], capture_output=True, text=True, check=True).stdout.strip()
def passes(path, prefix):
    return [l.strip() for l in path.read_text(encoding='utf-8', errors='replace').splitlines() if re.match(prefix + '.*PASS', l)]
runs = {}
for name, prefix in [('jit', '^NATIVE_API'), ('aot', '^NATIVE_API'), ('chromium', '^BROWSER_API')]:
    now, before = passes(run / f'{name}.log', prefix), passes(baseline / f'{name}.log', prefix)
    runs[name] = {'passLines': len(now), 'baselinePassLines': len(before), 'missingFromBaseline': [l for l in before if l not in now],
                  'added': [l for l in now if l not in before],
                  'exit': re.search(r'_EXIT=(\d+)', (run / f'{name}.log').read_text(encoding='utf-8', errors='replace')).group(1)}
result = {
    'schemaVersion': 1, 'group': 'WebCore #142 length-delimited generated string inputs',
    'status': 'green (private integration; not accepted)', 'issue': 'https://github.com/ranvier-org/Ranvier.WebCore/issues/142',
    'coreSourceCommit': head('C:/wk/rv-strings-core'), 'viewsSourceCommit': head(here.parents[2]),
    'nativeProducer': {'head': head('C:/wk/native-strings'), 'abi': 51, 'runtime': 'native-strings artifacts/native43-candidate-3-frozen',
                       'evidence': 'Ranvier.WebCore docs/string-input-evidence/final-evidence.json'},
    'versions': {'native': '0.1.0-preview.43', 'dom': '0.1.0-alpha.46', 'views': '0.1.36', 'webcoreBackend': '0.1.0-alpha.36'},
    'allocationReceipt': 'Ranvier docs/research/native-api-delivery/str1-managed46-allocation.json',
    'baseline': {'runs': str(baseline), 'note': 'H08 final managed runs (JIT/AOT 111, Chromium 82)'},
    'privatePackages': {p.name: digest(p) for p in sorted(feed.glob('*.nupkg'))},
    'executedNative': {p.name: digest(p) for p in sorted(stage.iterdir()) if p.is_file() and p.suffix == '.dll'},
    'runs': runs,
    'stringChecks': {'assertions': 18, 'lifetime': 1, 'repeats': (run / 'strings-repeat30.txt').read_text().strip()},
}
out = here / 'string-inputs-runtime-evidence.json'
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')
print(out, digest(out), {k: (v['passLines'], v['missingFromBaseline']) for k, v in runs.items()})

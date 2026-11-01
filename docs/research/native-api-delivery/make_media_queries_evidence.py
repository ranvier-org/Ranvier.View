"""Writes media-queries-runtime-evidence.json: the G01/G15 managed private integration proof.
usage: make_media_queries_evidence.py <managed run dir> <baseline run dir> <native evidence json>"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

run, baseline, native = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
here = Path(__file__).resolve().parent
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()


def lines(path, prefix):
    return sorted(line.strip() for line in path.read_text(encoding='utf-8', errors='replace').splitlines() if line.startswith(prefix) and ' PASS' in line)


modes = {}
for mode, prefix in [('jit', 'NATIVE_API'), ('aot', 'NATIVE_API'), ('chromium', 'BROWSER_API')]:
    now, before = lines(run / (mode + '.log'), prefix), lines(baseline / (mode + '.log'), prefix)
    modes[mode] = {'passLines': len(now), 'baselinePassLines': len(before), 'supersetOfBaseline': set(before) <= set(now),
                   'added': sorted(set(now) - set(before)), 'logSha256': digest(run / (mode + '.log'))}
repeats = {name: (run / name).read_text(encoding='utf-8').split('\n') for name in ['aot-media-repeat30.txt', 'chromium-media-repeat30.txt']}
head = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=here, capture_output=True, text=True, check=True).stdout.strip()
evidence = {
    'groups': {'G01 #46': 'lane-green (private, not accepted)', 'G15 #59': 'lane-green (private, not accepted)'},
    'viewsHead': head,
    'nativeEvidence': {'file': str(native), 'sha256': digest(native)},
    'feed': 'local-only Ranvier.Dom/Fable.Ranvier.Dom 0.1.0-alpha.49 and Ranvier.WebCore 0.1.0-preview.46 packed from the lane heads; root renumbers at batch integration',
    'modes': modes,
    'repeats30': {name: [line.strip() for line in text if line.strip()] for name, text in repeats.items()},
}
(here / 'media-queries-runtime-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8', newline='\n')
print(json.dumps({mode: (value['passLines'], value['supersetOfBaseline']) for mode, value in modes.items()}))

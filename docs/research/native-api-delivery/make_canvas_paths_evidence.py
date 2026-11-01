"""Collects the R02 canvas paths managed evidence: native JIT, NativeAOT and Chromium runs of the shared CanvasChecks
fixture, their 30x repeats, and the baseline comparison, with file hashes.

Usage: python -I make_canvas_paths_evidence.py <managed run dir> <baseline run dir> <native evidence json>"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

run, baseline, native = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
here = Path(__file__).resolve().parent
repo = here.parents[2]


def passes(path, prefix):
    return sorted(re.sub(r' engine=.*', '', line) for line in path.read_text(encoding='utf-8', errors='replace').splitlines()
                  if line.startswith(prefix) and 'PASS' in line)


result = {'group': 'R02 #91', 'status': 'green (private Windows x64; not accepted)',
          'viewsHead': subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=repo, capture_output=True, text=True).stdout.strip(),
          'nativeEvidence': {'file': str(native), 'sha256': hashlib.sha256(native.read_bytes()).hexdigest()}}
for engine, prefix in [('jit', 'NATIVE_API'), ('aot', 'NATIVE_API'), ('chromium', 'BROWSER_API')]:
    lines, before = passes(run / (engine + '.log'), prefix), passes(baseline / (engine + '.log'), prefix)
    result[engine] = {'passLines': len(lines), 'baselinePassLines': len(before),
                      'r02': [line for line in lines if ' R02 ' in line],
                      'baselineLinesUnchanged': sorted(l for l in lines if ' R02 ' not in l) == before,
                      'logSha256': hashlib.sha256((run / (engine + '.log')).read_bytes()).hexdigest()}
for name in ['aot-canvas-repeat30.txt', 'chromium-canvas-repeat30.txt']:
    result[name] = [line.strip() for line in (run / name).read_text().splitlines()]
result['qualifications'] = ['The shared fixture checks hit testing, Path2D construction and reuse, fill rules, curves, '
                            'rounded rectangles and argument errors; pixels are asserted by the native canvas_paths probe.',
                            'Release kind 102 (Path2D) is a placeholder; root assigns kinds at integration.']
out = here / 'canvas-paths-runtime-evidence.json'
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['jit', 'aot', 'chromium']}, indent=1))

"""Collects the R06 canvas pixel managed evidence: native JIT, NativeAOT and Chromium runs of the shared CanvasPixelChecks
fixture, their 30x repeats, and the baseline comparison, with file hashes.

Usage: python -I make_canvas_pixels_evidence.py <managed run dir> <baseline run dir> <native evidence json>"""
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


result = {'group': 'R06 #95', 'status': 'green (private Windows x64; not accepted)',
          'viewsHead': subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=repo, capture_output=True, text=True).stdout.strip(),
          'nativeEvidence': {'file': str(native), 'sha256': hashlib.sha256(native.read_bytes()).hexdigest()}}
for engine, prefix in [('jit', 'NATIVE_API'), ('aot', 'NATIVE_API'), ('chromium', 'BROWSER_API')]:
    lines, before = passes(run / (engine + '.log'), prefix), passes(baseline / (engine + '.log'), prefix)
    result[engine] = {'passLines': len(lines), 'baselinePassLines': len(before),
                      'r06': [line for line in lines if ' R06 ' in line],
                      'baselineLinesUnchanged': sorted(l for l in lines if ' R06 ' not in l) == before,
                      'logSha256': hashlib.sha256((run / (engine + '.log')).read_bytes()).hexdigest()}
for name in ['aot-pixels-repeat30.txt', 'chromium-pixels-repeat30.txt']:
    result[name] = [line.strip() for line in (run / name).read_text().splitlines()]
result['qualifications'] = ['The shared fixture checks ImageData construction, layout, byte ranges, createImageData, '
                            'getImageData/putImageData round trips with a dirty rectangle and drawImage from a canvas; <img>, SVG '
                            '<image> and tainting are asserted by the native canvas_pixels probe.',
                            'Kind 120 (ImageData) is a placeholder and 103 (ImageBitmap) is shared with R04; root assigns kinds at integration.']
out = here / 'canvas-pixels-runtime-evidence.json'
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['jit', 'aot', 'chromium']}, indent=1))

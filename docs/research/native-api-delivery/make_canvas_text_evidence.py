"""Collects the R05 canvas text managed evidence: native JIT, NativeAOT and Chromium runs of the shared CanvasChecks
fixture, their 30x repeats, and the baseline comparison, with file hashes.

Usage: python -I make_canvas_text_evidence.py <managed run dir> <native evidence json> <baseline run dir>...
The baseline is the union of the R04 and T01 runs, the two parents this branch merges."""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

run, native, baselines = Path(sys.argv[1]), Path(sys.argv[2]), [Path(a) for a in sys.argv[3:]]
here = Path(__file__).resolve().parent
repo = here.parents[2]


def passes(path, prefix):
    return sorted(re.sub(r' engine=.*', '', line) for line in path.read_text(encoding='utf-8', errors='replace').splitlines()
                  if line.startswith(prefix) and 'PASS' in line)


result = {'group': 'R05 #94', 'status': 'green (private Windows x64; not accepted)',
          'viewsHead': subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=repo, capture_output=True, text=True).stdout.strip(),
          'nativeEvidence': {'file': str(native), 'sha256': hashlib.sha256(native.read_bytes()).hexdigest()}}
for engine, prefix in [('jit', 'NATIVE_API'), ('aot', 'NATIVE_API'), ('chromium', 'BROWSER_API')]:
    lines = passes(run / (engine + '.log'), prefix)
    before = sorted({line for baseline in baselines for line in passes(baseline / (engine + '.log'), prefix)})
    result[engine] = {'passLines': len(lines), 'baselinePassLines': len(before),
                      'r05': [line for line in lines if ' R05 ' in line],
                      'baselineLinesUnchanged': sorted(l for l in lines if ' R05 ' not in l) == before,
                      'logSha256': hashlib.sha256((run / (engine + '.log')).read_bytes()).hexdigest()}
for name in ['aot-canvas-repeat30.txt', 'chromium-canvas-repeat30.txt']:
    result[name] = [line.strip() for line in (run / name).read_text().splitlines()]
result['qualifications'] = ['The shared CanvasTextChecks fixture checks text styles, their validation and Ahem metrics after the '
                            'font set loads the face; pixels are asserted by the native canvas_text probe.',
                            'Engine differences are expected per engine: WebKit reports emHeight members, the hanging baseline at the ascent '
                            'and a resolved direction; Chromium has no emHeight members (TextMeasurement None), 0.8 of the ascent and inherit.',
                            'Actual bounding boxes are compared within 1px (Chromium inflates glyph bounds by 1px).',
                            'Release kind 130 (TextMetrics) is a placeholder; root assigns the integrated number.',
                            'fontKerning, fontStretch, fontVariantCaps and textRendering are Unsupported on the native engine and not in the shared contract.']
out = here / 'canvas-text-runtime-evidence.json'
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['jit', 'aot', 'chromium']}, indent=1))

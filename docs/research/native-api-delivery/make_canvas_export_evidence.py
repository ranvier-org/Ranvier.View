"""Collects the R07 canvas export managed evidence: native JIT, NativeAOT and Chromium runs of the shared CanvasExportChecks
fixture, their 30x repeats, and the baseline comparison, with file hashes.

Usage: python -I make_canvas_export_evidence.py <managed run dir> <baseline run dir> <native evidence json>"""
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


result = {'group': 'R07 #96', 'status': 'green (private Windows x64; not accepted)',
          'viewsHead': subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=repo, capture_output=True, text=True).stdout.strip(),
          'nativeEvidence': {'file': str(native), 'sha256': hashlib.sha256(native.read_bytes()).hexdigest()}}
for engine, prefix in [('jit', 'NATIVE_API'), ('aot', 'NATIVE_API'), ('chromium', 'BROWSER_API')]:
    lines, before = passes(run / (engine + '.log'), prefix), passes(baseline / (engine + '.log'), prefix)
    result[engine] = {'passLines': len(lines), 'baselinePassLines': len(before),
                      'r07': [line for line in lines if ' R07 ' in line],
                      'baselineLinesUnchanged': sorted(l for l in lines if ' R07 ' not in l) == before,
                      'logSha256': hashlib.sha256((run / (engine + '.log')).read_bytes()).hexdigest()}
for name in ['aot-export-repeat30.txt', 'chromium-export-repeat30.txt']:
    result[name] = [line.strip() for line in (run / name).read_text().splitlines()]
result['qualifications'] = ['The shared fixture checks export bytes, data URLs, Blobs, RGBA8 and zero-size canvases, '
                            'createImageBitmap with crop, flipY, resize, a Blob and failure names, close, cancel and the '
                            'bitmaprenderer transfer; <img>, SVG <image>, WEBP and tainting are asserted by the native '
                            'canvas_export probe.',
                            'An unsupported encoding raises NotSupportedException on both providers; Chromium itself substitutes PNG.',
                            'Kind 121 (ImageBitmapRenderingContext) is a placeholder; root assigns kinds at integration.',
                            'The first NativeAOT full run stopped in the D16 dialog fixture ("second showModal: expected [()], got []", '
                            'aot-first-run-dialog-flake.log), which runs before R07; --dialogs-only then failed 1 of 5 and 0 of 10 on this '
                            'runtime and 0 of 10 on the R06 runtime, and the full rerun (aot.log) passed: a timing-dependent dialog check.',
                            'Views is deliberately not merged with R12 (rv-snapshot 40fe831): its snapshot provider needs native R12 '
                            'exports this native branch lacks; it reaches R07 at batch-5 integration.']
out = here / 'canvas-export-runtime-evidence.json'
out.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['jit', 'aot', 'chromium']}, indent=1))

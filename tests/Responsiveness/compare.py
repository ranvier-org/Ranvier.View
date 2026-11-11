"""Compare matched, fixed-operation cohorts; fail closed on incomplete evidence."""
import argparse
import json
import math
from pathlib import Path
from statistics import median

MATCH = ('schemaVersion', 'benchmarkProtocol', 'scenario', 'logicalLines',
         'requestedRenderer', 'hidden', 'tracing', 'interactive',
         'gpuFramePipeline', 'boundedImeContext', 'os', 'machineId', 'liveLines')


def compare(before, after, ratio=1.15, noise_ms=0.5):
    errors = []
    if len(before) < 5 or len(before) != len(after):
        return ['at least five matched repetitions required']
    try:
        identity = tuple(before[0][key] for key in MATCH)
        for run in before + after:
            if run.get('analysisFailures', 0) != 0:
                errors.append('analysis failures cannot qualify')
            if run.get('pendingAtMeasurementEnd', 0) != 0:
                errors.append('work deferred beyond measured interval')
            if tuple(run[key] for key in MATCH) != identity:
                errors.append('workload/environment mismatch')
            if run['schemaVersion'] != 1 or run['benchmarkProtocol'] != 'owner-turn-v1':
                errors.append('unsupported protocol')
            for key in ('turnP95Ms', 'allocatedBytes', 'iterations', 'pendingWork', 'cpuFrames', 'gpuFrames'):
                if not math.isfinite(run[key]) or run[key] < 0:
                    errors.append(f'invalid {key}')
            if run['iterations'] <= 0 or run['iterations'] != before[0]['iterations']:
                errors.append('completed operation count mismatch')
            if run['pendingWork'] != 0:
                errors.append('unfinished work cannot qualify')
            frames = run['cpuFrames'] + run['gpuFrames']
            baseline_frames = before[0]['cpuFrames'] + before[0]['gpuFrames']
            if frames <= 0 or frames < baseline_frames * 0.95:
                errors.append('presentation coverage decreased')
        if errors:
            return errors
        baseline = median(r['turnP95Ms'] for r in before)
        current = median(r['turnP95Ms'] for r in after)
        if current > max(baseline * ratio, baseline + noise_ms):
            errors.append(f'p95 regression: {baseline:.3f} -> {current:.3f} ms')
        allocation = median(r['allocatedBytes'] / r['iterations'] for r in before)
        current_allocation = median(r['allocatedBytes'] / r['iterations'] for r in after)
        if current_allocation > max(allocation * 1.20, allocation + 256):
            errors.append(f'allocation regression: {allocation:.1f} -> {current_allocation:.1f} bytes/op')
    except (KeyError, TypeError, ValueError) as error:
        errors.append(f'invalid evidence: {error}')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('--scenario', default='typing')
    args = parser.parse_args()
    def load(directory):
        return [json.loads(p.read_text()) for p in sorted(directory.glob(args.scenario + '-*.json'))
                if p.stem.rsplit('-', 1)[-1].isdigit()]
    failures = compare(load(args.before), load(args.after))
    print('\n'.join(failures) if failures else 'PASS: latency, allocation and completed-work gates')
    raise SystemExit(bool(failures))

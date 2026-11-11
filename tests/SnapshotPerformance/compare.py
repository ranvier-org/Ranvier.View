"""Five fresh-process PNG cohorts against the published pre-transfer managed release."""
import argparse
import json
from pathlib import Path
import statistics
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('baseline')
    parser.add_argument('candidate')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    root = Path(args.output)
    root.mkdir(parents=True, exist_ok=True)
    results = {}
    for width, height in ((800, 600), (1600, 1200)):
        for freshness in ('fresh', 'reuse'):
            case = f'{width}x{height}-{freshness}'
            cohorts = {'baseline': [], 'candidate': []}
            hashes = set()
            for repetition in range(5):
                for label, fixture in (('baseline', args.baseline), ('candidate', args.candidate)):
                    target = root / f'{case}-{label}-{repetition}.json'
                    subprocess.run(['dotnet', fixture, str(width), str(height), freshness, str(target)],
                                   check=True, timeout=180)
                    result = json.loads(target.read_text())
                    if label == 'candidate' and not result['nativePixels']:
                        raise RuntimeError('Candidate requires snapshot-pixels-v1')
                    hashes.add(result['pngSha256'])
                    cohorts[label].append(result['samples'])
            if len(hashes) != 1:
                raise RuntimeError(f'{case}: baseline and candidate PNG output differ: {hashes}')
            summary = {}
            for label, runs in cohorts.items():
                summary[label] = {
                    field: statistics.median(sorted(row[field] for row in run)[28] for run in runs)
                    for field in ('captureCallMs', 'ownerWorkMs', 'completionMs', 'allocatedBytes')}
            results[case] = summary
            (root / 'summary.json').write_text(json.dumps(results, indent=2))
            before, after = summary['baseline'], summary['candidate']
            if after['ownerWorkMs'] > before['ownerWorkMs'] * 1.35 + .5:
                raise RuntimeError(f'{case}: owner latency regression: {summary}')
            if after['completionMs'] > before['completionMs'] * 1.35 + 2.:
                raise RuntimeError(f'{case}: completion latency regression: {summary}')
            if after['allocatedBytes'] > before['allocatedBytes'] - width * height * 3:
                raise RuntimeError(f'{case}: intermediate RGBA allocation persists: {summary}')
    print(json.dumps(results, indent=2))


if __name__ == '__main__':
    main()

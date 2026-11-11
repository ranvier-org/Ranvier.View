"""Run interleaved baseline/candidate fixed-work processes on this machine."""
import argparse
import json
import random
import subprocess
from pathlib import Path
from compare import compare


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('baseline', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--scenario', default='typing')
    args = parser.parse_args()
    rng = random.Random(1701)
    results = {'baseline': [], 'candidate': []}
    for repetition in range(5):
        labels = list(results)
        rng.shuffle(labels)
        for label in labels:
            destination = args.output / label / str(repetition)
            destination.mkdir(parents=True, exist_ok=True)
            command = ['dotnet', str(getattr(args, label).resolve()), '--scenario', args.scenario,
                       '--iterations', '600', '--repetitions', '1', '--lines', '1000',
                       '--renderer', 'software', '--no-trace', '--interactive', '--bounded-ime',
                       '--output', str(destination.resolve())]
            result = subprocess.run(command, capture_output=True, text=True, timeout=180)
            (destination / 'process.log').write_text(result.stdout + result.stderr, encoding='utf-8')
            result.check_returncode()
            results[label].append(json.loads((destination / (args.scenario + '-1.json')).read_text()))
    errors = compare(results['baseline'], results['candidate'])
    (args.output / 'comparison.json').write_text(json.dumps(dict(results=results, failures=errors), indent=2))
    print('\n'.join(errors) if errors else 'PASS: five interleaved matched runs')
    return bool(errors)


if __name__ == '__main__':
    raise SystemExit(main())

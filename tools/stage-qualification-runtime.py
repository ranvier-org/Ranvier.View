"""Extract a selected CI GPU package and record its exact native payload hashes."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

packages, output, rid = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
matches = list(packages.glob('Ranvier.WebCore.Native.' + rid + '.*.nupkg'))
if len(matches) != 1:
    raise SystemExit('Expected exactly one GPU native package for ' + rid)
prefix = 'runtimes/' + rid + '/native/'
files = {}
with zipfile.ZipFile(matches[0]) as archive:
    for entry in archive.infolist():
        if entry.is_dir() or not entry.filename.startswith(prefix):
            continue
        destination = (output / entry.filename).resolve()
        if not destination.is_relative_to(output.resolve()):
            raise SystemExit('Package member escapes staging directory')
        destination.parent.mkdir(parents=True, exist_ok=True)
        data = archive.read(entry)
        destination.write_bytes(data)
        files[entry.filename] = hashlib.sha256(data).hexdigest()
if not files:
    raise SystemExit('Native package had no matching runtime files')
(output.parent / 'native-receipt.json').write_text(json.dumps({
    'package': matches[0].name,
    'packageSha256': hashlib.sha256(matches[0].read_bytes()).hexdigest(),
    'files': files}, indent=2))

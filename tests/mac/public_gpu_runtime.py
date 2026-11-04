"""Stage and check the signed public macOS GPU runtime for the IOSurface transport job.

  stage  --version V --sha256 H --output DIR   download the exact public package, check its hash, extract runtimes/
  expecto --log FILE --passed N --ignored M    require an exact Expecto pass/ignore count with no failure or error
  output --stage DIR --output DIR              require the output's dylibs to equal the staged package payload
"""
import argparse
import hashlib
import io
import json
import re
import sys
import urllib.request
import zipfile
from pathlib import Path

PACKAGE = 'ranvier.webcore.native.gpu.osx-arm64'
NATIVE = 'runtimes/osx-arm64/native/'
DYLIBS = {'libEGL.dylib', 'libGLESv2.dylib', 'libJavaScriptCore.dylib', 'libRanvierWebCoreHost.dylib',
          'libWebCore.dylib', 'libWebGPU.dylib'}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stage(a):
    url = f'https://api.nuget.org/v3-flatcontainer/{PACKAGE}/{a.version}/{PACKAGE}.{a.version}.nupkg'
    data = urllib.request.urlopen(url, timeout=300).read()
    if sha(data) != a.sha256:
        sys.exit(f'{url}: sha256 {sha(data)} differs from pinned {a.sha256}')
    archive = zipfile.ZipFile(io.BytesIO(data))
    if '.signature.p7s' not in archive.namelist():
        sys.exit('the public package carries no repository signature')
    members = {}
    for info in archive.infolist():
        if info.filename.startswith(NATIVE) and not info.is_dir():
            target = a.output / info.filename
            if not target.resolve().is_relative_to(a.output.resolve()):
                sys.exit(f'unsafe member {info.filename}')
            target.parent.mkdir(parents=True, exist_ok=True)
            payload = archive.read(info)
            target.write_bytes(payload)
            members[info.filename] = sha(payload)
    names = {Path(name).name for name in members}
    if names != DYLIBS:
        sys.exit(f'native payload {sorted(names)} differs from the six-dylib GPU closure')
    receipt = dict(package=PACKAGE, version=a.version, url=url, sha256=a.sha256, members=members)
    (a.output / 'public-gpu-runtime.json').write_text(json.dumps(receipt, indent=2))
    print(json.dumps(receipt, indent=2))


def expecto(a):
    text = a.log.read_text(encoding='utf-8', errors='replace')
    text = re.sub(r'\[[0-9;?]*[A-Za-z]', '', text)
    found = re.findall(r'(\d+) passed, (\d+) ignored, (\d+) failed, (\d+) errored', text)
    if len(found) != 1:
        sys.exit(f'{a.log}: expected one Expecto summary, found {len(found)}')
    passed, ignored, failed, errored = map(int, found[0])
    if (passed, ignored, failed, errored) != (a.passed, a.ignored, 0, 0):
        sys.exit(f'{a.log}: {passed} passed, {ignored} ignored, {failed} failed, {errored} errored; '
                 f'required {a.passed} passed, {a.ignored} ignored')
    print(f'{a.log}: {passed} passed, {ignored} ignored')


def output(a):
    receipt = json.loads((a.stage / 'public-gpu-runtime.json').read_text())
    expected = {Path(name).name: digest for name, digest in receipt['members'].items()}
    found = [path for path in a.output.rglob('*.dylib') if path.name in DYLIBS]
    if len(found) != len({path.name for path in found}):
        sys.exit(f'{a.output}: duplicate GPU dylib names {sorted(str(path) for path in found)}')
    actual = {path.name: sha(path.read_bytes()) for path in found}
    if actual != expected:
        sys.exit(f'{a.output}: dylibs {json.dumps(actual, indent=1)} differ from {json.dumps(expected, indent=1)}')
    print(f'{a.output}: six public GPU dylibs match the package payload')


parser = argparse.ArgumentParser()
commands = parser.add_subparsers(dest='command', required=True)
p = commands.add_parser('stage')
p.add_argument('--version', required=True)
p.add_argument('--sha256', required=True)
p.add_argument('--output', required=True, type=Path)
p.set_defaults(run=stage)
p = commands.add_parser('expecto')
p.add_argument('--log', required=True, type=Path)
p.add_argument('--passed', required=True, type=int)
p.add_argument('--ignored', required=True, type=int)
p.set_defaults(run=expecto)
p = commands.add_parser('output')
p.add_argument('--stage', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
p.set_defaults(run=output)
args = parser.parse_args()
args.run(args)

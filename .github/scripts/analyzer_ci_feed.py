"""Verify the source-built Host artifact before adding its exact NuGet route."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import runpy
import subprocess
import xml.etree.ElementTree as ET
import zipfile

HOST = 'Ranvier.Analyzers.Host'
ANALYZER = 'Ranvier.Analyzers'


def inspect_package(root, feed, source):
    if not re.fullmatch('[0-9a-f]{40}', source):
        raise ValueError('Expected an exact source commit')
    paths = list(feed.glob('*.nupkg'))
    if len(paths) != 2 or any(path.is_symlink() for path in paths):
        raise ValueError('The bootstrap feed must contain exactly the regular analyzer/Host pair')
    project = root / 'tools/Ranvier.Analyzers.Host'
    versions = {HOST: ET.parse(project / 'Ranvier.Analyzers.Host.csproj').findtext('.//Version'),
                ANALYZER: ET.parse(root / 'tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj').findtext('.//Version')}
    spec = json.loads((project / 'vendor-spec.json').read_text())
    packages = []
    for package in sorted(paths):
        with zipfile.ZipFile(package) as archive:
            names = archive.namelist()
            nuspecs = [name for name in names if name.lower().endswith('.nuspec')]
            if len(names) != len(set(names)) or len(nuspecs) != 1:
                raise ValueError('Ambiguous bootstrap package')
            metadata = ET.fromstring(archive.read(nuspecs[0])).find('{*}metadata')
            identity = metadata.findtext('{*}id') if metadata is not None else None
            if identity not in versions or metadata.findtext('{*}version') != versions[identity]:
                raise ValueError('Unexpected bootstrap package identity')
            repository = metadata.find('{*}repository')
            if repository is None or repository.get('commit') != source:
                raise ValueError('Bootstrap package was not built from this CI source')
            if identity == HOST:
                compiler = archive.read('tools/net10.0/any/FSharp.Compiler.Service.dll')
                if hashlib.sha256(compiler).hexdigest() != spec['compilerSha256']:
                    raise ValueError('Host compiler differs from the pinned source build')
        packages.append(dict(id=identity, version=versions[identity], file=package.name,
                             sha256=hashlib.sha256(package.read_bytes()).hexdigest()))
    if {entry['id'] for entry in packages} != set(versions):
        raise ValueError('Bootstrap pair is incomplete or duplicated')
    return dict(source=source, packages=packages, compilerSha256=spec['compilerSha256'])


def record(root, feed, source):
    identity = inspect_package(root, feed, source)
    (feed / 'bootstrap-identity.json').write_text(json.dumps(identity, indent=2) + '\n', encoding='utf-8')
    return identity


def configure(root, feed, output, source):
    identity = inspect_package(root, feed, source)
    recorded = json.loads((feed / 'bootstrap-identity.json').read_text())
    if recorded != identity:
        raise ValueError('Bootstrap package changed after the source build')
    helper = runpy.run_path(str(root / 'tools/Ranvier.Analyzers.Host/tests/write_repository_nuget_config.py'))
    helper['build_repository_config'](root / 'nuget.config', output,
                                     [dict(key='ci-analyzer-host', path=str(feed.resolve()), packageIds=[HOST, ANALYZER])])
    return identity


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['record', 'configure'])
    parser.add_argument('--feed', type=Path, required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    actual = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != args.source:
        raise ValueError('The bootstrap source does not match this checkout')
    if args.mode == 'record':
        identity = record(root, args.feed, args.source)
    else:
        if args.output is None:
            parser.error('--output is required for configure')
        identity = configure(root, args.feed, args.output, args.source)
    print(json.dumps(identity))


if __name__ == '__main__':
    main()

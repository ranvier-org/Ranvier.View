"""Fetch and verify one exact Windows GPU NuGet runtime into a fresh local stage."""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import urlsplit
from urllib.request import urlopen
import xml.etree.ElementTree as ET
import zipfile


def stage_package(data: bytes, package: str, version: str, destination: Path):
    if destination.exists():
        raise ValueError('The GPU stage must be a new directory; choose a fresh --out path.')
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if len(names) != len({name.casefold() for name in names}):
            raise ValueError('Duplicate package entries.')
        for name in names:
            path = PurePosixPath(name)
            if (path.is_absolute() or '..' in path.parts or '\\' in name
                or path.as_posix() != name.rstrip('/')
                or any(':' in part or part.endswith(('.', ' ')) for part in path.parts)):
                raise ValueError(f'Invalid package path: {name}')
        nuspecs = [name for name in names if name.endswith('.nuspec')]
        if len(nuspecs) != 1:
            raise ValueError('Expected one NuGet package specification.')
        metadata = ET.fromstring(archive.read(nuspecs[0])).find('{*}metadata')
        if metadata is None or metadata.findtext('{*}id', '').lower() != package.lower() or metadata.findtext('{*}version', '').lower() != version.lower():
            raise ValueError('NuGet identity/version does not match the requested GPU package.')
        manifest_bytes = archive.read('manifest.json')
        manifest = json.loads(manifest_bytes)
        source = manifest.get('source', {})
        if manifest.get('flavor') != 'gpu' or manifest.get('rid') != 'win-x64' or manifest.get('version') != version:
            raise ValueError('Expected the exact requested win-x64 GPU manifest.')
        if source.get('dirty') is not False or not re.fullmatch('[0-9a-fA-F]{40}', source.get('commit', '')):
            raise ValueError('GPU package must record a clean source commit.')
        if not isinstance(manifest.get('abi'), int) or manifest['abi'] <= 0:
            raise ValueError('GPU package has no ABI version.')
        prefix = 'runtimes/win-x64/native/'
        records = manifest.get('files', [])
        if len({record['name'] for record in records}) != len(records):
            raise ValueError('Duplicate manifest files.')
        expected = {prefix + record['name'] for record in records}
        actual = {name for name in names if name.startswith(prefix) and not name.endswith('/')}
        if expected != actual:
            raise ValueError('Native package files do not match its manifest.')
        if not {'RanvierWebCoreHost.dll', 'WebCore.dll', 'libEGL.dll', 'libGLESv2.dll'} <= {record['name'] for record in records}:
            raise ValueError('GPU package is missing its host, WebCore or ANGLE libraries.')
        payload = {}
        for record in records:
            name = prefix + record['name']
            content = archive.read(name)
            if len(content) != record['bytes'] or hashlib.sha256(content).hexdigest() != record['sha256']:
                raise ValueError(f'GPU file failed size/hash validation: {name}')
            payload[name] = content
        if sum(len(content) for content in payload.values()) != manifest.get('total_bytes'):
            raise ValueError('GPU manifest total size does not match.')
    destination.mkdir(parents=True)
    for name, content in payload.items():
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
    (destination / 'manifest.json').write_bytes(manifest_bytes)
    print(f"Verified {package} {version}: win-x64 gpu, ABI {manifest['abi']}, {len(payload)} files")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', default='Ranvier.WebCore.Native.Gpu.win-x64')
    parser.add_argument('--version', required=True)
    parser.add_argument('--source', default='https://api.nuget.org/v3/index.json')
    parser.add_argument('--out', type=Path, default=Path('artifacts/gpu-runtime'))
    args = parser.parse_args()
    if not re.fullmatch('[A-Za-z0-9_.-]+', args.package) or not re.fullmatch(r'\d+\.\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?', args.version):
        parser.error('Invalid NuGet package identity or version.')
    source = urlsplit(args.source)
    if source.scheme != 'https' or source.username or source.password:
        parser.error('--source must be a public HTTPS NuGet v3 service index.')
    with urlopen(args.source, timeout=60) as response:
        index = json.load(response)
    base = next(resource['@id'] for resource in index['resources'] if resource['@type'] == 'PackageBaseAddress/3.0.0')
    if urlsplit(base).scheme != 'https':
        parser.error('The package content endpoint must use HTTPS.')
    package, version = args.package.lower(), args.version.lower()
    with urlopen(f'{base.rstrip("/")}/{package}/{version}/{package}.{version}.nupkg', timeout=120) as response:
        data = response.read()
    stage_package(data, args.package, args.version, args.out)


if __name__ == '__main__':
    main()

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import zipfile


def digest(content):
    return hashlib.sha256(content).hexdigest()


def prepare(package_root, compiler, output):
    source = Path(__file__).resolve().parent
    spec = json.loads((source / 'vendor-spec.json').read_text())
    assert digest((source / 'compiler.patch').read_bytes()) == spec['sourceGuardSha256'], 'The compiler guard differs from its specification.'
    assert digest(compiler.read_bytes()) == spec['compilerSha256'], 'The compiler differs from the reviewed vendor build.'
    package = package_root / 'fsharp-analyzers' / '0.41.0' / 'fsharp-analyzers.0.41.0.nupkg'
    assert digest(package.read_bytes()) == spec['cliPackageSha256'], 'The upstream CLI package differs from its pin.'
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    prefix = 'tools/net10.0/any/'
    with zipfile.ZipFile(package) as archive:
        for item in archive.infolist():
            if item.is_dir() or not item.filename.startswith(prefix):
                continue
            relative = PurePosixPath(item.filename[len(prefix):])
            target = (output / str(relative)).resolve()
            assert target.is_relative_to(output), item.filename
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(item))
        notices = output / 'vendor-notices'
        notices.mkdir(exist_ok=True)
        for name in ['fsharp-analyzers.nuspec', 'README.md']:
            if name in archive.namelist():
                (notices / name).write_bytes(archive.read(name))
    shutil.copyfile(compiler, output / 'FSharp.Compiler.Service.dll')
    for notice in (source / 'licenses').iterdir():
        shutil.copyfile(notice, notices / notice.name)
    shutil.copyfile(source / 'compiler.patch', notices / 'compiler.patch')
    (output / 'vendor-manifest.json').write_text(json.dumps(spec, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--package-root', type=Path, required=True)
    parser.add_argument('--compiler', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.package_root, args.compiler, args.output)

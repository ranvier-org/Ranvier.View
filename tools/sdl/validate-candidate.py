import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

VERSION = '0.1.0-alpha.1'
SDL_REVISION = 'ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1'
PATCH_BLOB = '4d8abcac20792f8e548de4523e30433ac3691969'
PACKAGE = 'Ranvier.View.WebCore.Sdl.Ibus.' + VERSION + '.nupkg'
SOURCE_BLOBS = {
    'tools/sdl/ibus-surrounding.patch': PATCH_BLOB,
    'tools/sdl/build-linux.sh': '7ba5967e0f15ea01034666751dabb3e351448ca7',
    'tools/sdl/package/Ranvier.View.WebCore.Sdl.Ibus.csproj': '9471e2d7f624a4b4d3ce971883eec3655d8dcfa5',
    'tools/sdl/package/Ranvier.View.WebCore.Sdl.Ibus.targets': '912c3059c9eb84b78d4eac4f43c1197891d52565',
    'tools/sdl/package/SDL-LICENSE.txt': 'e9adee4484511b4b1730461679fed21d9b13d88e',
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def validate_config(header, cache):
    lines = set(header.splitlines())
    required = {'#define SDL_VIDEO_DRIVER_X11 1', '#define HAVE_DBUS_DBUS_H 1', '#define HAVE_IBUS_IBUS_H 1'}
    if not required <= lines or not {'SDL_DBUS:BOOL=ON', 'SDL_IBUS:BOOL=ON'} <= set(cache.splitlines()):
        raise ValueError('SDL X11, D-Bus or IBus configuration is not enabled')


def validate(package, binary, targets, license_file):
    header = binary.read_bytes()[:20]
    if len(header) < 20 or header[:6] != b'\x7fELF\x02\x01' or header[18:20] != b'\x3e\x00':
        raise ValueError('The SDL payload is not little-endian ELF64 x86-64')
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError('Duplicate package members')
        fixed = {'_rels/.rels', 'Ranvier.View.WebCore.Sdl.Ibus.nuspec', '[Content_Types].xml',
                 'tools/linux-x64/libSDL3.so', 'buildTransitive/Ranvier.View.WebCore.Sdl.Ibus.targets',
                 'licenses/SDL-LICENSE.txt'}
        metadata = [n for n in names if n.startswith('package/services/metadata/core-properties/') and n.endswith('.psmdcp')]
        if len(metadata) != 1 or set(names) != fixed | set(metadata):
            raise ValueError('Unexpected package member set')
        expected = {
            'tools/linux-x64/libSDL3.so': binary.read_bytes(),
            'buildTransitive/Ranvier.View.WebCore.Sdl.Ibus.targets': targets.read_bytes(),
            'licenses/SDL-LICENSE.txt': license_file.read_bytes(),
        }
        for name, data in expected.items():
            if archive.read(name) != data:
                raise ValueError('Package payload differs: ' + name)
        if {n for n in names if n.endswith('.so')} != {'tools/linux-x64/libSDL3.so'}:
            raise ValueError('Unexpected native payload')
        nuspec = archive.read('Ranvier.View.WebCore.Sdl.Ibus.nuspec').decode('utf8')
        if '<id>Ranvier.View.WebCore.Sdl.Ibus</id>' not in nuspec or '<version>' + VERSION + '</version>' not in nuspec:
            raise ValueError('Unexpected package identity')
    return {'packageSha256':digest(package.read_bytes()), 'sdlSha256':digest(binary.read_bytes()),
            'targetsSha256':digest(targets.read_bytes()), 'licenseSha256':digest(license_file.read_bytes())}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    if args.package.name != PACKAGE:
        raise ValueError('Unexpected package filename')
    if subprocess.check_output(['git','status','--porcelain','--untracked-files=all','--','tools/sdl'],cwd=root,text=True).strip():
        raise ValueError('SDL source tree must be clean')
    for name, expected in SOURCE_BLOBS.items():
        blob = subprocess.check_output(['git','hash-object',str(root/name)],text=True).strip()
        if blob != expected:
            raise ValueError('SDL source input differs: '+name)
    header = args.binary.parent/'SDL_build_config.h'
    cache = args.binary.parent/'CMakeCache.txt'
    validate_config(header.read_text(),cache.read_text())
    if (args.binary.parent/'SDL-LICENSE.txt').read_bytes() != (root/'tools/sdl/package/SDL-LICENSE.txt').read_bytes():
        raise ValueError('Packaged license differs from upstream SDL license')
    result = validate(args.package, args.binary, root/'tools/sdl/package/Ranvier.View.WebCore.Sdl.Ibus.targets', root/'tools/sdl/package/SDL-LICENSE.txt')
    result.update(id='Ranvier.View.WebCore.Sdl.Ibus', version=VERSION, sdlRevision=SDL_REVISION,
                  sourceGitBlobs=SOURCE_BLOBS, configSha256=digest(header.read_bytes()), cacheSha256=digest(cache.read_bytes()),
                  viewSource=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),
                  scope='SDL-only candidate; real input-method qualification and publication are separate')
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()

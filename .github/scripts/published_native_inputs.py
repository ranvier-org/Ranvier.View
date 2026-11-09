"""Print the versions and native ABI of this commit's native packages as shell assignments."""

import io
import json
from pathlib import Path
import re
from urllib.request import urlopen
import zipfile


def project_property(path, name):
    values = re.findall(rf'<{name}(?: [^>]*)?>([^<$]+)</{name}>', Path(path).read_text(encoding='utf-8'))
    if len(values) != 1 or not re.fullmatch(r'[0-9A-Za-z.+-]+', values[0]):
        raise ValueError(f'Expected one {name} in {path}: {values}')
    return values[0]


def native_abi(version):
    # The smallest package that carries the cohort's manifest.
    package = 'ranvier.webcore.native.cpu.win-x64'
    url = f'https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg'
    with urlopen(url, timeout=300) as response:
        with zipfile.ZipFile(io.BytesIO(response.read())) as archive:
            abi = json.loads(archive.read('manifest.json'))['abi']
    if type(abi) is not int or abi <= 0:
        raise ValueError(f'Invalid ABI in {package} {version}: {abi}')
    return abi


def main():
    view = 'src/Ranvier.View.WebCore/Ranvier.View.WebCore.fsproj'
    native = project_property(view, 'RanvierWebCoreNativeVersion')
    print(f"VIEW_VERSION={project_property(view, 'Version')}")
    print(f"SDL_VERSION={project_property('src/Ranvier.View.WebCore.Sdl/Ranvier.View.WebCore.Sdl.fsproj', 'Version')}")
    print(f'NATIVE_VERSION={native}')
    print(f'NATIVE_ABI={native_abi(native)}')


if __name__ == '__main__':
    main()

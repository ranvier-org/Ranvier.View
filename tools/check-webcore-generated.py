"""Check native wrappers against the restored canonical interop model and DOM contracts."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parents[1]
    project = root / 'src/Ranvier.View.WebCore/Ranvier.View.WebCore.fsproj'
    result = subprocess.run([
        'dotnet', 'msbuild', str(project), '-nologo', '-p:Configuration=Release',
        '-getProperty:NuGetPackageRoot,RanvierWebCoreNativeVersion,RanvierDomVersion,RanvierPackageSuffix,UseLocalRanvier,UseLocalRanvierWebCore,RanvierWebCoreNativeDir',
    ], cwd=root, check=True, text=True, capture_output=True)
    properties = json.loads(result.stdout)['Properties']
    if (properties['UseLocalRanvier'].lower() != 'false'
        or properties['UseLocalRanvierWebCore'].lower() != 'false'
        or properties['RanvierWebCoreNativeDir']):
        raise ValueError('Wrapper verification requires restored packages, without local interop/runtime overrides.')
    packages = Path(properties['NuGetPackageRoot'])
    native = packages / 'ranvier.webcore' / properties['RanvierWebCoreNativeVersion'].lower()
    dom = packages / ('ranvier.dom' + properties['RanvierPackageSuffix']).lower() / properties['RanvierDomVersion'].lower()
    inputs = [native / 'model/dom-api.json', dom / 'lib/net10.0/Ranvier.Dom.dll', native / 'lib/net10.0/Ranvier.WebCore.dll']
    generator = root / 'tools/Ranvier.View.WebCore.Generate/bin/Release/net10.0/Ranvier.View.WebCore.Generate.dll'
    for path in [generator, *inputs]:
        if not path.is_file():
            raise ValueError(f'Missing restored generator input: {path}')
    with tempfile.TemporaryDirectory(prefix='ranvier-webcore-generated-') as output:
        subprocess.run(['dotnet', str(generator), *(str(path) for path in inputs), output], cwd=root, check=True)
        for name in ['Objects.fs', 'report.txt']:
            expected = (root / 'src/Ranvier.View.WebCore/Generated' / name).read_text(encoding='utf-8')
            actual = (Path(output) / name).read_text(encoding='utf-8')
            if actual != expected:
                raise ValueError(f'{name} differs from the restored canonical model/contracts; regenerate it.')
            digest = hashlib.sha256(actual.encode('utf-8')).hexdigest()
            print(f'PASS canonical wrapper generation: {name} {digest}')


if __name__ == '__main__':
    main()

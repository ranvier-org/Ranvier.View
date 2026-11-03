"""Build the test-only Metal producer; no WebCore or SDL producer build."""
import argparse,hashlib,json,platform,subprocess
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--output',required=True,type=Path)
a=p.parse_args()
if platform.system()!='Darwin' or platform.machine()!='arm64':
    raise SystemExit('This qualified recipe requires macOS ARM64; it is not a cross-platform runtime proof.')
if int(platform.mac_ver()[0].split('.')[0])<26:
    raise SystemExit('This bounded recipe requires macOS 26 or newer.')
sdk=subprocess.check_output(['xcrun','--sdk','macosx','--show-sdk-version'],text=True).strip()
if int(sdk.split('.')[0])<26:
    raise SystemExit('This bounded recipe requires the macOS 26 SDK or newer.')
source=Path(__file__).with_name('metal_input.mm').resolve()
a.output.mkdir(parents=True,exist_ok=True)
library=a.output/'libRanvierMacInputFixture.dylib'
command=['xcrun','--sdk','macosx','clang++','-std=c++20','-O2','-fobjc-arc','-dynamiclib',str(source),'-framework','Foundation','-framework','Metal','-framework','IOSurface','-o',str(library)]
result=subprocess.run(command,check=True)
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
(a.output/'metal-input-build.json').write_text(json.dumps({'command':command,'sourceSha256':sha(source),'librarySha256':sha(library),'platform':platform.platform(),'machine':platform.machine(),'sdk':sdk},indent=2))
print(str(library.resolve()))

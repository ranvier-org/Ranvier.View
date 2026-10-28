"""Build the independent Windows UI Automation client with an installed C++ toolchain."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--compiler-environment', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/native-window'
out.mkdir(parents=True, exist_ok=True)
env = dict(os.environ)
if args.compiler_environment or not shutil.which('cl'):
    environment = args.compiler_environment
    if not environment:
        vswhere = Path(os.environ.get('ProgramFiles(x86)', 'C:/Program Files (x86)')) / 'Microsoft Visual Studio/Installer/vswhere.exe'
        installation = subprocess.check_output([str(vswhere), '-latest', '-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-property', 'installationPath'], text=True).strip()
        if not installation:
            parser.error('No installed Visual Studio C++ compiler found; pass --compiler-environment.')
        environment = Path(installation) / 'VC/Auxiliary/Build/vcvars64.bat'
    script = out / 'compiler-environment.cmd'
    script.write_text(f'@call "{environment}" >nul\n@set PATH\n@set INCLUDE\n@set LIB\n', newline='\r\n')
    for line in subprocess.check_output(['cmd', '/d', '/c', str(script)], text=True).splitlines():
        if '=' in line and not line.startswith('='):
            key, value = line.split('=', 1)
            env[key.upper()] = value
compiler = shutil.which('cl', path=env.get('PATH'))
if not compiler:
    parser.error('Selected compiler environment did not provide cl.exe.')
subprocess.run([compiler, '/nologo', '/std:c++20', '/EHsc', '/W4', str(root / 'tests/native-window/UiAutomationProbe.cpp'),
                f'/Fe:{out}/UiAutomationProbe.exe', f'/Fo:{out}/', '/link', 'Uiautomationcore.lib', 'Ole32.lib', 'OleAut32.lib', 'User32.lib', 'Uuid.lib'], cwd=out, env=env, check=True)

import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
import sys

sys.dont_write_bytecode = True

root = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('gpu_runtime', root / 'tools/fetch-gpu-runtime.py')
gpu = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gpu)
package = 'Ranvier.WebCore.Native.Gpu.win-x64'
version = '0.1.0-preview.test'


def archive(flavor='gpu', corrupt=False, extra=None):
    files = {name: name.encode() for name in ['RanvierWebCoreHost.dll', 'WebCore.dll', 'libEGL.dll', 'libGLESv2.dll']}
    manifest = dict(flavor=flavor, rid='win-x64', version=version, abi=21,
                    source=dict(commit='a' * 40, dirty=False), total_bytes=sum(map(len, files.values())),
                    files=[dict(name=name, bytes=len(content), sha256=hashlib.sha256(content).hexdigest()) for name, content in files.items()])
    data = io.BytesIO()
    with zipfile.ZipFile(data, 'w') as output:
        output.writestr('runtime.nuspec', f'<package><metadata><id>{package}</id><version>{version}</version></metadata></package>')
        output.writestr('manifest.json', json.dumps(manifest))
        for name, content in files.items():
            output.writestr('runtimes/win-x64/native/' + name, content + (b'changed' if corrupt else b''))
        if extra:
            output.writestr(extra, b'extra')
    return data.getvalue()


class GpuRuntimeTests(unittest.TestCase):
    def test_exact_gpu_cohort_is_staged(self):
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / 'stage'
            gpu.stage_package(archive(), package, version, destination)
            self.assertEqual((destination / 'runtimes/win-x64/native/WebCore.dll').read_bytes(), b'WebCore.dll')

    def test_rejects_cpu_cohort_wrong_version_corruption_and_unlisted_native_files(self):
        cases = [(archive(flavor='slim'), version), (archive(), '0.1.0-other'),
                 (archive(corrupt=True), version), (archive(extra='runtimes/win-x64/native/extra.dll'), version),
                 (archive(extra='../outside.dll'), version)]
        for data, requested_version in cases:
            with self.subTest(version=requested_version), tempfile.TemporaryDirectory() as temporary:
                destination = Path(temporary) / 'stage'
                with self.assertRaises(ValueError):
                    gpu.stage_package(data, package, requested_version, destination)
                self.assertFalse(destination.exists())

    def test_rejects_reusing_a_stage_with_stale_libraries(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                gpu.stage_package(archive(), package, version, Path(temporary))


if __name__ == '__main__':
    unittest.main()

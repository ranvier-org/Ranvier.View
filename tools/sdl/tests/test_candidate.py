import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('candidate', ROOT/'tools/sdl/validate-candidate.py')
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)


class CandidateTests(unittest.TestCase):
    def test_generated_config_requires_each_backend(self):
        header='\n'.join(['#define SDL_VIDEO_DRIVER_X11 1','#define HAVE_DBUS_DBUS_H 1','#define HAVE_IBUS_IBUS_H 1'])
        cache='SDL_DBUS:BOOL=ON\nSDL_IBUS:BOOL=ON\n'
        candidate.validate_config(header,cache)
        for value in ('#define SDL_VIDEO_DRIVER_X11 1','#define HAVE_DBUS_DBUS_H 1','#define HAVE_IBUS_IBUS_H 1'):
            with self.subTest(missing=value),self.assertRaises(ValueError): candidate.validate_config(header.replace(value,''),cache)
        for value in ('SDL_DBUS:BOOL=ON','SDL_IBUS:BOOL=ON'):
            with self.subTest(disabled=value),self.assertRaises(ValueError): candidate.validate_config(header,cache.replace(value,value.replace('ON','OFF')))

    def test_exact_package_payload_and_mismatches(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)
            elf=b'\x7fELF\x02\x01'+b'\0'*12+b'\x3e\x00'+b'\0'*44
            binary=root/'libSDL3.so';binary.write_bytes(elf)
            targets=root/'bridge.targets';targets.write_bytes(b'targets')
            license_file=root/'license';license_file.write_bytes(b'license')
            package=root/candidate.PACKAGE
            def make(binary_data=elf, extra=None, version=candidate.VERSION):
                with zipfile.ZipFile(package,'w') as z:
                    z.writestr('Ranvier.View.WebCore.Sdl.Ibus.nuspec',f'<id>Ranvier.View.WebCore.Sdl.Ibus</id><version>{version}</version>')
                    z.writestr('tools/linux-x64/libSDL3.so',binary_data)
                    z.writestr('buildTransitive/Ranvier.View.WebCore.Sdl.Ibus.targets',b'targets')
                    z.writestr('licenses/SDL-LICENSE.txt',b'license')
                    z.writestr('_rels/.rels',b'links')
                    z.writestr('[Content_Types].xml',b'types')
                    z.writestr('package/services/metadata/core-properties/test.psmdcp',b'metadata')
                    if extra: z.writestr(extra,b'foreign')
            make()
            self.assertEqual(candidate.validate(package,binary,targets,license_file)['sdlSha256'],candidate.digest(binary.read_bytes()))
            for kwargs in ({'binary_data':b'wrong'}, {'extra':'runtimes/linux-x64/native/libRanvierWebCoreHost.so'}, {'version':'0.1.0-alpha.2'}):
                with self.subTest(kwargs=kwargs):
                    make(**kwargs)
                    with self.assertRaises(ValueError): candidate.validate(package,binary,targets,license_file)

    def test_workflow_is_disabled_and_never_publishes(self):
        text=(ROOT/'.github/workflows/sdl-ibus-candidate.yml').read_text()
        self.assertIn('if: ${{ false &&',text)
        self.assertIn("github.event_name == 'workflow_dispatch'",text)
        self.assertIn("github.ref == 'refs/heads/main'",text)
        self.assertIn(candidate.SDL_REVISION,text)
        self.assertNotIn('id-token:',text.lower())
        self.assertNotIn('nuget push',text.lower())
        self.assertNotIn('webcore',text.lower().replace('ranvier.view.webcore.sdl.ibus',''))


if __name__ == '__main__':
    unittest.main()

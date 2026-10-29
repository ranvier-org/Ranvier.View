import importlib.util
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('shared_api', ROOT / 'tools/check-published-shared-api.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class PublishedSharedApiTests(unittest.TestCase):
    def test_release_profile_and_traced_browser_recipe(self):
        profile = json.loads((ROOT / 'tests/native-packages/shared-api-0.1.2.json').read_text())
        gate.validate_profile(profile)
        references = gate.package_references(profile, 'browser', '.Traced')
        project = gate.ET.fromstring('<Project>' + references + '</Project>')
        items = {item.get('Include'): item for item in project.iter('PackageReference') if item.get('Include')}
        self.assertEqual(items['Ranvier.Dom.Traced'].get('ExcludeAssets'), 'all')
        self.assertEqual(items['Fable.Ranvier.Dom.Traced'].get('Version'), '[0.1.0-alpha.11]')
        self.assertNotIn('Ranvier.Dom', items)

    def test_fixture_revision_must_be_immutable(self):
        with self.assertRaises(ValueError):
            gate.validate_profile({'source_commit': 'main'})

    def test_source_compile_list_cannot_escape_fixture_directory(self):
        with self.assertRaises(ValueError):
            gate.compile_names('<Project><Compile Include="../private.fs" /></Project>')

    def test_package_commit_must_match_fixture_revision(self):
        valid = ('<package><metadata><repository url="https://github.com/ranvier-org/Ranvier.View" commit="'
                 + 'a' * 40 + '" /></metadata></package>')
        gate.verify_nuspec(valid, 'a' * 40)
        with self.assertRaisesRegex(ValueError, 'commit'):
            gate.verify_nuspec(valid.replace('a' * 40, 'b' * 40), 'a' * 40)

    def test_traced_graph_cannot_include_normal_parent(self):
        with self.assertRaisesRegex(ValueError, 'trace'):
            gate.verify_variant(['Ranvier.View.Traced/0.1.2', 'Ranvier/0.1.1-preview.1'], 'traced')

    def test_count_does_not_include_context_or_lifetime(self):
        profile = {'shared_assertions': 50, 'native_lifetime_assertions': 2,
                   'browser_context_assertions': 4, 'css_escape_oracle': 196609}
        log = ('NATIVE_API D11_VIEW_HYPERLINKS PASS assertions=50\n'
               'NATIVE_API C04 LIFETIME PASS assertions=2\nNATIVE_API TEARDOWN PASS\n'
               'NATIVE_API D09 TAB PASS\nOFFICIAL_TRACE_METADATA PASS expected=False assemblies=5\n')
        gate.verify_assertions(log, 'native', 'normal', profile)
        with self.assertRaises(ValueError):
            gate.verify_assertions(log.replace('assertions=50', 'assertions=49'), 'native', 'normal', profile)

    def test_browser_rejects_active_native_dom_assembly(self):
        with self.assertRaisesRegex(ValueError, 'native DOM'):
            gate.verify_browser_assets({'net10.0': {
                'Ranvier.Dom/0.1.0-alpha.11': {'compile': {'lib/net10.0/Ranvier.Dom.dll': {}}},
                'Fable.Ranvier.Dom/0.1.0-alpha.11': {'compile': {'lib/net10.0/Ranvier.Dom.dll': {}}}}}, '')


if __name__ == '__main__':
    unittest.main()

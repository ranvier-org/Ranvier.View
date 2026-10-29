import importlib.util
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('shared_api', ROOT / 'tools/check-published-shared-api.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class PublishedSharedApiTests(unittest.TestCase):
    def test_release_profiles_keep_independent_target_totals(self):
        old = json.loads((ROOT / 'tests/native-packages/shared-api-0.1.2.json').read_text())
        new = json.loads((ROOT / 'tests/native-packages/shared-api-0.1.3.json').read_text())
        gate.validate_profile(old)
        gate.validate_profile(new)
        self.assertEqual(gate.expected_shared(old, 'native'), 685)
        self.assertEqual(gate.expected_shared(old, 'browser'), 685)
        self.assertEqual(gate.expected_shared(new, 'native'), 1086)
        self.assertEqual(gate.expected_shared(new, 'browser'), 1099)
        self.assertNotEqual(old['source_commit'], new['source_commit'])
        new['module_assertions']['browser']['C09'] = 41
        with self.assertRaisesRegex(ValueError, 'Module'):
            gate.validate_profile(new)

    def test_distinct_target_counts_and_compensating_module_drift(self):
        profile = {'shared_assertions': {'native': 185, 'browser': 187},
                   'module_assertions': {
                       'native': {'D11_VIEW_HYPERLINKS': 50, 'C09': 44, 'C10': 91},
                       'browser': {'D11_VIEW_HYPERLINKS': 50, 'C09': 44, 'C10': 93}},
                   'native_lifetime_assertions': 2, 'browser_context_assertions': 4,
                   'css_escape_oracle': 196609}
        log = ('NATIVE_API D11_VIEW_HYPERLINKS PASS assertions=50\n'
               'NATIVE_API C09 PASS assertions=44\nNATIVE_API C10 PASS assertions=91\n'
               'NATIVE_API C04 LIFETIME PASS assertions=2\nNATIVE_API TEARDOWN PASS\n'
               'NATIVE_API D09 TAB PASS\nOFFICIAL_TRACE_METADATA PASS expected=False assemblies=5\n')
        gate.verify_assertions(log, 'native', 'normal', profile)
        browser_log = ('BROWSER_API D11_VIEW_HYPERLINKS PASS assertions=50 oracle=196609\n'
                       'BROWSER_API C09 PASS assertions=44\nBROWSER_API C10 PASS assertions=93\n'
                       'BROWSER_API C03 CONTEXT PASS assertions=2\n'
                       'BROWSER_API C01_C02 CONTEXT PASS assertions=2\nBROWSER_API D09 TAB PASS\n')
        gate.verify_assertions(browser_log, 'browser', 'normal', profile)
        with self.assertRaisesRegex(ValueError, 'module'):
            gate.verify_assertions(log.replace('assertions=44', 'assertions=43')
                                   .replace('assertions=91', 'assertions=92'), 'native', 'normal', profile)
        with self.assertRaisesRegex(ValueError, 'module'):
            gate.verify_assertions(log.replace('NATIVE_API C09 PASS assertions=44',
                                               'NATIVE_API C09 PASS assertions=22\nNATIVE_API C09 PASS assertions=22'),
                                   'native', 'normal', profile)

    def test_windows_console_preserves_unicode_child_progress(self):
        data = io.BytesIO()
        console = io.TextIOWrapper(data, encoding='cp1252', newline='\n')
        with self.assertRaises(UnicodeEncodeError):
            console.write('████████')
        with patch.object(sys, 'stdout', console), patch.object(sys, 'stderr', console):
            gate.configure_console()
            print('Playwright progress: ████████', flush=True)
        self.assertEqual(data.getvalue().decode('utf-8'), 'Playwright progress: ████████\n')

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

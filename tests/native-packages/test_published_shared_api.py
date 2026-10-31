import importlib.util
import io
import json
from pathlib import Path
import sys
import struct
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('shared_api', ROOT / 'tools/check-published-shared-api.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class PublishedSharedApiTests(unittest.TestCase):
    def test_aot_receipt_rejects_successful_publish_of_managed_apphost(self):
        self.assertTrue(hasattr(gate, 'verify_aot_publish'), 'A successful publish and CLR-free apphost do not prove NativeAOT.')
        image = bytearray(512)
        image[:2] = b'MZ'
        struct.pack_into('<I', image, 60, 128)
        image[128:132] = b'PE\0\0'
        struct.pack_into('<H', image, 132, 0x8664)
        struct.pack_into('<H', image, 148, 240)
        struct.pack_into('<H', image, 152, 0x20b)
        struct.pack_into('<I', image, 152 + 108, 16)
        with tempfile.TemporaryDirectory() as temporary:
            executable = Path(temporary) / 'Consumer.exe'
            executable.write_bytes(image)
            with self.assertRaisesRegex(ValueError, 'native code generation'):
                gate.verify_aot_publish('Publish succeeded; apphost copied.\n', executable)
            receipt = gate.verify_aot_publish('  Generating native code\n', executable)
            self.assertEqual(receipt['sha256'], gate.hashlib.sha256(image).hexdigest())
            self.assertEqual(receipt['bytes'], len(image))
            self.assertEqual(receipt['clr_directory'], [0, 0])
            struct.pack_into('<II', image, 152 + 112 + 14 * 8, 100, 72)
            executable.write_bytes(image)
            with self.assertRaisesRegex(ValueError, 'CLR'):
                gate.verify_aot_publish('  Generating native code\n', executable)

    def test_native_consumers_verify_each_payload_before_execution(self):
        self.assertTrue(hasattr(gate, 'execute_native'), 'Native payload verification must precede each process execution.')
        profile = {'shared_assertions': 50, 'native_lifetime_assertions': 2}
        log = ('NATIVE_API D11_VIEW_HYPERLINKS PASS assertions=50\n'
               'NATIVE_API C04 LIFETIME PASS assertions=2\nNATIVE_API TEARDOWN PASS\n'
               'NATIVE_API D09 TAB PASS\nOFFICIAL_TRACE_METADATA PASS expected=False assemblies=5\n')
        for corrupt in [None, 'win-x64', 'published-aot']:
            events = []
            def run(label, *command):
                events.append(label)
                if label == 'jit':
                    self.assertIn('--no-build', command)
                return log
            def payload(directory):
                events.append('payload-' + directory.name)
                if directory.name == corrupt:
                    raise ValueError('corrupt payload')
            if corrupt:
                with self.assertRaisesRegex(ValueError, 'corrupt payload'):
                    gate.execute_native(run, payload, Path('Consumer.fsproj'), Path('consumer'), 'normal', profile)
                self.assertNotIn('aot', events)
                if corrupt == 'win-x64':
                    self.assertNotIn('jit', events)
            else:
                with patch.object(gate, 'verify_aot_publish', return_value={}):
                    gate.execute_native(run, payload, Path('Consumer.fsproj'), Path('consumer'), 'normal', profile)
                self.assertEqual(events, ['build', 'payload-win-x64', 'jit', 'publish', 'payload-published-aot', 'aot'])

    def test_views14_profile_uses_immutable_release_and_complete_modules(self):
        path = ROOT / 'tests/native-packages/shared-api-0.1.4.json'
        self.assertTrue(path.is_file(), 'The released Views14 cohort needs an independent immutable profile.')
        profile = json.loads(path.read_text())
        gate.validate_profile(profile)
        self.assertEqual(profile['source_commit'], '7fa387551e86f0c7a4a6b1c8224fadbcbb7cf517')
        self.assertEqual(profile['shared_assertions'], {'native': 1628, 'browser': 1641})
        self.assertEqual(profile['native_lifetime_assertions'], 54)
        self.assertEqual(profile['browser_context_assertions'], 10)
        for target in ['native', 'browser']:
            for group, count in {'C11': 82, 'C12': 50, 'C14': 99, 'C19': 64, 'C21': 44, 'D12': 109}.items():
                self.assertEqual(profile['module_assertions'][target][group], count)

    def test_views15_profile_uses_immutable_release_and_complete_modules(self):
        path = ROOT / 'tests/native-packages/shared-api-0.1.5.json'
        self.assertTrue(path.is_file(), 'The released Views15 cohort needs an independent immutable profile.')
        profile = json.loads(path.read_text())
        gate.validate_profile(profile)
        self.assertEqual(profile['source_commit'], '65c0711c03c73fde809462db4571e529f881b183')
        self.assertEqual((profile['view_version'], profile['backend_version'], profile['dom_version'],
                          profile['native_version'], profile['native_abi']),
                         ('0.1.5', '0.1.0-alpha.5', '0.1.0-alpha.15', '0.1.0-preview.15', 23))
        self.assertEqual(profile['shared_assertions'], {'native': 1755, 'browser': 1768})
        self.assertEqual(profile['native_lifetime_assertions'], 87)
        self.assertEqual(profile['browser_context_assertions'], 13)
        previous = json.loads((ROOT / 'tests/native-packages/shared-api-0.1.4.json').read_text())
        for target in ['native', 'browser']:
            self.assertEqual(profile['module_assertions'][target]['C13'], 127)
            self.assertEqual(set(profile['module_assertions'][target]),
                             set(previous['module_assertions'][target]) | {'C13'})

    def test_trace_precheck_preserves_argument_aware_and_original_entries(self):
        self.assertTrue(hasattr(gate, 'instrument_native_runner'), 'Argument-aware released entry point needs a checked trace precheck.')
        for entry in ['let main _ =\n', 'let main args =\n']:
            source = '[<EntryPoint>]\n' + entry + '    originalBody ()\n'
            patched = gate.instrument_native_runner(source)
            self.assertEqual(patched, source.replace(entry, entry + '    OfficialTraceProbe.verify ()\n'))
            with self.assertRaisesRegex(ValueError, 'entry point'):
                gate.instrument_native_runner(source + source)
            with self.assertRaisesRegex(ValueError, 'precheck'):
                gate.instrument_native_runner(patched)
        with self.assertRaisesRegex(ValueError, 'entry point'):
            gate.instrument_native_runner('let main unsupported =\n    0\n')

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

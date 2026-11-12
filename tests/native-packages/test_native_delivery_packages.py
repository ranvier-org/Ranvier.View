import hashlib
import json
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / '.github/scripts/native_delivery_packages.py'
POLICY = SCRIPT.with_name('native-delivery-packages.json')
NATIVE_VIEW = (
    'Ranvier.View', 'Ranvier.View.Traced', 'Ranvier.View.Dom', 'Ranvier.View.Dom.Traced',
    'Ranvier.View.WebCore', 'Ranvier.View.WebCore.Traced',
    'Ranvier.View.WebCore.Sdl', 'Ranvier.View.WebCore.Sdl.Traced',
)
ANIME = ('Ranvier.View.Anime', 'Ranvier.View.Anime.Traced', 'Fable.Ranvier.View.Anime', 'Fable.Ranvier.View.Anime.Traced',
         'Fable.Ranvier.Anime', 'Fable.Ranvier.Anime.Traced')


def package(path, identity):
    metadata = f'<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>{identity}</id><version>0.1.45</version><authors>fixture</authors><description>package selection fixture</description></metadata></package>'
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr('package.nuspec', metadata)
        archive.writestr('lib/net10.0/fixture.dll', b'unchanged package payload')


def identities(directory):
    import xml.etree.ElementTree as ET
    result = set()
    for path in directory.glob('*.nupkg'):
        with zipfile.ZipFile(path) as archive:
            name = next(n for n in archive.namelist() if n.endswith('.nuspec'))
            result.add(ET.fromstring(archive.read(name)).findtext('{*}metadata/{*}id'))
    return result


class NativeDeliveryPackagesTests(unittest.TestCase):
    def test_analyzer_pair_is_reserved_for_its_qualified_publication_route(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.seed(root)
            package(directory / 'renamed-host.nupkg', 'Ranvier.Analyzers.Host')
            package(directory / 'renamed-analyzer.nupkg', 'Ranvier.Analyzers')
            package(directory / 'unrelated.nupkg', 'Ranvier.Analyzers.Extension')
            result = self.run_filter(root, require_complete=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(identities(directory), set(NATIVE_VIEW) | {'Ranvier.Analyzers.Extension'})
            self.assertEqual(identities(root / 'excluded'), set(ANIME) | {'Ranvier.Analyzers', 'Ranvier.Analyzers.Host'})

    def seed(self, root):
        directory = root / 'bin'
        directory.mkdir()
        for index, identity in enumerate(NATIVE_VIEW + ANIME):
            package(directory / f'renamed-{index}.nupkg', identity)
        return directory

    def run_filter(self, root, require_complete=False):
        self.assertTrue(SCRIPT.is_file(), 'The native-delivery selector is missing; the current publisher pushes every artifact.')
        return subprocess.run(
            [sys.executable, '-I', str(SCRIPT), '--directory', str(root / 'bin'),
             '--excluded-directory', str(root / 'excluded'), '--report', str(root / 'report.json')]
            + (['--require-view-cohort'] if require_complete else []),
            capture_output=True, text=True, timeout=15,
        )

    def test_exact_declared_anime_ids_are_removed_even_when_filenames_are_renamed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.seed(root)
            package(directory / 'Ranvier.View.Anime.fake-name.nupkg', 'Useful.Anime.Extension')
            before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in directory.glob('*.nupkg')}
            result = self.run_filter(root)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(identities(directory), set(NATIVE_VIEW) | {'Useful.Anime.Extension'})
            self.assertEqual(identities(root / 'excluded'), set(ANIME))
            after = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for folder in (directory, root / 'excluded') for p in folder.glob('*.nupkg')}
            self.assertEqual(after, before)
            report = json.loads((root / 'report.json').read_text())
            self.assertEqual({r['id'] for r in report['excluded']}, set(ANIME))

    def test_malformed_or_ambiguous_archive_cannot_partially_remove_valid_inputs(self):
        for mode in ('corrupt', 'multiple-nuspecs'):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                directory = self.seed(root)
                bad = directory / 'last-malformed.nupkg'
                if mode == 'corrupt':
                    bad.write_bytes(b'not a zip archive')
                else:
                    package(bad, 'Other.Package')
                    with zipfile.ZipFile(bad, 'a') as archive:
                        archive.writestr('another.nuspec', '<package/>')
                before = {p.name: p.read_bytes() for p in directory.iterdir()}
                result = self.run_filter(root)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual({p.name: p.read_bytes() for p in directory.iterdir()}, before)
                self.assertFalse((root / 'excluded').exists())

    def test_missing_native_view_package_blocks_selection_before_any_move(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.seed(root)
            (directory / 'renamed-7.nupkg').unlink()
            before = {p.name: p.read_bytes() for p in directory.iterdir()}
            result = self.run_filter(root, require_complete=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual({p.name: p.read_bytes() for p in directory.iterdir()}, before)
            self.assertFalse((root / 'excluded').exists())

    def test_case_insensitive_duplicate_package_id_blocks_selection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.seed(root)
            package(directory / 'duplicate.nupkg', 'ranvier.view')
            before = {p.name: p.read_bytes() for p in directory.iterdir()}
            result = self.run_filter(root)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual({p.name: p.read_bytes() for p in directory.iterdir()}, before)

    def test_selection_is_idempotent_for_already_filtered_ci_artifacts(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.seed(root)
            self.assertEqual(self.run_filter(root).returncode, 0)
            self.assertEqual(self.run_filter(root).returncode, 0)
            self.assertEqual(identities(root / 'bin'), set(NATIVE_VIEW))
            self.assertEqual(json.loads((root / 'report.json').read_text())['excluded'], [])

    def test_single_pack_filter_reports_incomplete_cohort_without_requiring_other_pack_invocations(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'bin').mkdir()
            package(root / 'bin/one.nupkg', 'Ranvier.View.Anime')
            package(root / 'bin/two.nupkg', 'Other.Package')
            result = self.run_filter(root)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(identities(root / 'bin'), {'Other.Package'})
            self.assertFalse(json.loads((root / 'report.json').read_text())['view_cohort_complete'])

    def test_both_workflow_routes_execute_selection_before_artifact_upload_or_login(self):
        for workflow, job in (('ci.yml', 'packages'), ('publish.yml', 'publish')):
            with self.subTest(workflow=workflow):
                text = (ROOT / '.github/workflows' / workflow).read_text()
                block = re.search(r'(?ms)^  ' + re.escape(job) + r':\n(.*?)(?=^  [A-Za-z0-9_-]+:|\Z)', text).group(1)
                lines = block.splitlines()
                commands = [(i, shlex.split(line.split('run:', 1)[1].strip())) for i, line in enumerate(lines) if line.lstrip().startswith('run: ')]
                selectors = [(i, command) for i, command in commands if '.github/scripts/native_delivery_packages.py' in command]
                self.assertEqual(len(selectors), 1, f'{workflow} has no native-delivery selection boundary')
                index, command = selectors[0]
                boundary = next(i for i, line in enumerate(lines) if ('uses: actions/upload-artifact' in line if job == 'packages' else 'uses: NuGet/login' in line))
                self.assertLess(index, boundary)
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    self.seed(root)
                    target = root / '.github/scripts'
                    target.mkdir(parents=True)
                    shutil.copy2(SCRIPT, target / SCRIPT.name)
                    shutil.copy2(POLICY, target / POLICY.name)
                    command = command.copy()
                    command[0] = sys.executable
                    result = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=15)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual(identities(root / 'bin'), set(NATIVE_VIEW))
                    if job == 'publish':
                        (root / 'bin/renamed-7.nupkg').unlink()
                        incomplete = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=15)
                        self.assertNotEqual(incomplete.returncode, 0, 'The publication route accepted an incomplete aggregate cohort')


if __name__ == '__main__':
    unittest.main()

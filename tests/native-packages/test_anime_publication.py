import re
import shlex
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / '.github/scripts/anime_packages.py'
PROJECT = 'src/Ranvier.View.Anime/Ranvier.View.Anime.fsproj'
ANIME = ('Ranvier.View.Anime', 'Ranvier.View.Anime.Traced', 'Fable.Ranvier.View.Anime', 'Fable.Ranvier.View.Anime.Traced')


def package(path, identity, version):
    metadata = f'<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>{identity}</id><version>{version}</version><authors>fixture</authors><description>anime publication fixture</description></metadata></package>'
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr('package.nuspec', metadata)
        archive.writestr('lib/net10.0/fixture.dll', b'payload')


def job_block(workflow, job):
    text = (ROOT / '.github/workflows' / workflow).read_text()
    match = re.search(r'(?ms)^  ' + re.escape(job) + r':\n(.*?)(?=^  [A-Za-z0-9_-]+:|\Z)', text)
    if match is None:
        raise AssertionError(f'{workflow} has no {job} job')
    return match.group(1)


class AnimePublicationTests(unittest.TestCase):
    def workspace(self, root, version='0.1.46-rc.1'):
        project = root / PROJECT
        project.parent.mkdir(parents=True)
        project.write_text(f'<Project><PropertyGroup><PackageId>Ranvier.View.Anime</PackageId><Version>{version}</Version></PropertyGroup></Project>')
        directory = root / 'anime'
        directory.mkdir()
        return directory

    def select(self, root):
        self.assertTrue(SCRIPT.is_file(), 'The Anime publication selector is missing.')
        return subprocess.run(
            [sys.executable, '-I', str(SCRIPT), '--directory', str(root / 'anime'), '--project', str(root / PROJECT)],
            capture_output=True, text=True, timeout=15,
        )

    def test_exact_set_at_the_project_version_is_selected_native_first_and_untraced_first(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.workspace(root)
            package(directory / 'd.nupkg', 'Fable.Ranvier.View.Anime.Traced', '0.1.46-rc.1')
            package(directory / 'c.nupkg', 'Fable.Ranvier.View.Anime', '0.1.46-rc.1')
            package(directory / 'b.nupkg', 'Ranvier.View.Anime.Traced', '0.1.46-rc.1')
            package(directory / 'a-renamed.nupkg', 'Ranvier.View.Anime', '0.1.46-rc.1')
            result = self.select(root)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual([Path(line).name for line in result.stdout.splitlines()], ['a-renamed.nupkg', 'b.nupkg', 'c.nupkg', 'd.nupkg'])

    def test_incomplete_foreign_duplicate_or_stale_packages_are_rejected(self):
        complete = [(identity, '0.1.46-rc.1') for identity in ANIME]
        cases = {
            'missing traced': [row for row in complete if row[0] != 'Ranvier.View.Anime.Traced'],
            'missing Fable': [row for row in complete if not row[0].startswith('Fable.')],
            'foreign package': complete + [('Fable.Ranvier.Anime', '0.1.46-rc.1')],
            'duplicate id': complete + [('ranvier.view.anime', '0.1.46-rc.1')],
            'stale version': [('Ranvier.View.Anime', '0.1.45')] + complete[1:],
        }
        for name, packages in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                directory = self.workspace(root)
                for index, (identity, version) in enumerate(packages):
                    package(directory / f'{index}.nupkg', identity, version)
                result = self.select(root)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, '')

    def test_ci_uploads_the_excluded_anime_packages_after_selection(self):
        lines = job_block('ci.yml', 'packages').splitlines()
        selector = next(i for i, line in enumerate(lines) if '.github/scripts/native_delivery_packages.py' in line)
        upload = next(i for i, line in enumerate(lines) if line.strip() == 'name: anime-packages')
        self.assertLess(selector, upload)
        path = next(line for line in lines[upload:] if line.strip().startswith('path:'))
        self.assertIn('artifacts/native-delivery-excluded/*Ranvier.View.Anime*.nupkg', path)
        self.assertTrue(any('tests/native-packages/test_anime_publication.py' in line for line in lines))

    def test_publish_route_verifies_the_set_before_login_and_follows_the_native_cohort(self):
        block = job_block('publish.yml', 'publish-anime')
        native = job_block('publish.yml', 'publish')
        condition = lambda text: re.search(r'(?ms)^    if: >-\n(.*?)^    runs-on:', text).group(1)
        self.assertEqual(condition(block), condition(native))
        self.assertRegex(block, r'(?m)^    needs: publish$')
        lines = block.splitlines()
        download = next(i for i, line in enumerate(lines) if line.strip() == 'name: anime-packages')
        selector = next(i for i, line in enumerate(lines) if '.github/scripts/anime_packages.py' in line)
        login = next(i for i, line in enumerate(lines) if 'uses: NuGet/login' in line)
        self.assertLess(download, selector)
        self.assertLess(selector, login)
        command = shlex.split(lines[selector].split('run:', 1)[1].split('>', 1)[0].strip())
        push = [line for line in lines[login:] if 'dotnet nuget push' in line]
        self.assertEqual(len(push), 1)
        self.assertIn('--skip-duplicate', push[0])
        command[0] = sys.executable
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            directory = self.workspace(root)
            shutil.copytree(ROOT / '.github/scripts', root / '.github/scripts')
            empty = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=15)
            self.assertNotEqual(empty.returncode, 0, 'The publication route accepted an empty Anime artifact')
            self.assertIn('found nothing', empty.stderr)
            for identity in ANIME:
                package(directory / f'{identity}.nupkg', identity, '0.1.46-rc.1')
            complete = subprocess.run(command, cwd=root, capture_output=True, text=True, timeout=15)
            self.assertEqual(complete.returncode, 0, complete.stderr)
            self.assertEqual(len(complete.stdout.splitlines()), len(ANIME))


if __name__ == '__main__':
    unittest.main()

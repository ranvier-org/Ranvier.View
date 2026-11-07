"""Core source resolution from the published Ranvier.Dom nuspec; fixtures never reach the network."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('core_source', ROOT / 'tools/core-source.py')
core = importlib.util.module_from_spec(spec)
spec.loader.exec_module(core)
COMMIT = '0123456789abcdef0123456789abcdef01234567'


def nuspec(repository):
    return ('<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>'
            f'<id>Ranvier.Dom</id>{repository}</metadata></package>').encode()


class CoreSourceTests(unittest.TestCase):
    def test_commit_comes_from_the_requested_version_nuspec(self):
        urls = []

        def get(url):
            urls.append(url)
            return nuspec(f'<repository type="git" url="https://github.com/ranvier-org/Ranvier" commit="{COMMIT}" />')

        self.assertEqual(core.source_commit('0.1.0-RC.7', get), COMMIT)
        self.assertEqual(urls, ['https://api.nuget.org/v3-flatcontainer/ranvier.dom/0.1.0-rc.7/ranvier.dom.nuspec'])

    def test_missing_foreign_or_short_repository_metadata_is_rejected(self):
        for repository in ['', '<repository type="git" url="https://github.com/other/Ranvier" commit="' + COMMIT + '" />',
                           '<repository type="git" url="https://github.com/ranvier-org/Ranvier" commit="abc123" />',
                           '<repository type="git" url="https://github.com/ranvier-org/Ranvier" />']:
            with self.subTest(repository=repository), self.assertRaises(ValueError):
                core.source_commit('0.1.0-rc.1', lambda url: nuspec(repository))

    def test_version_defaults_to_the_props_value(self):
        with tempfile.TemporaryDirectory() as directory:
            props = Path(directory) / 'Directory.Build.props'
            props.write_text('<Project><PropertyGroup><RanvierDomVersion>0.1.0-rc.9</RanvierDomVersion></PropertyGroup></Project>')
            self.assertEqual(core.dom_version(props), '0.1.0-rc.9')
        self.assertEqual(core.dom_version(), core.dom_version(ROOT / 'Directory.Build.props'))

    def test_the_core_package_commit_is_read_from_its_own_nuspec(self):
        urls = []

        def get(url):
            urls.append(url)
            return nuspec(f'<repository type="git" url="https://github.com/ranvier-org/Ranvier" commit="{COMMIT}" />')

        self.assertEqual(core.source_commit('0.1.1-preview.2', get, package=core.CORE_PACKAGE), COMMIT)
        self.assertEqual(urls, ['https://api.nuget.org/v3-flatcontainer/ranvier/0.1.1-preview.2/ranvier.nuspec'])

    def test_the_later_commit_contains_the_other(self):
        other = 'f' * 40

        def compare(status):
            return lambda url: ('{"status": "%s"}' % status).encode()

        self.assertEqual(core.later(COMMIT, COMMIT, compare('boom')), COMMIT)
        self.assertEqual(core.later(COMMIT, other, compare('ahead')), other)
        self.assertEqual(core.later(COMMIT, other, compare('identical')), other)
        self.assertEqual(core.later(COMMIT, other, compare('behind')), COMMIT)
        with self.assertRaises(ValueError):
            core.later(COMMIT, other, compare('diverged'))

    def test_workflows_resolve_the_core_source_instead_of_pinning_a_commit(self):
        for name in ['ci.yml', 'analyzer-host.yml']:
            workflow = (ROOT / '.github/workflows' / name).read_text(encoding='utf-8')
            with self.subTest(workflow=name):
                self.assertIn('tools/core-source.py', workflow)
                self.assertIn('ref: ${{ steps.core.outputs.commit }}', workflow)
                self.assertIn('GH_TOKEN: ${{ github.token }}', workflow)
                self.assertNotRegex(workflow, r'ref: [0-9a-f]{40}')


if __name__ == '__main__':
    unittest.main()

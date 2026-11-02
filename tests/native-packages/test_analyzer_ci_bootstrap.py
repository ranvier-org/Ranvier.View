import hashlib
import json
from pathlib import Path
import runpy
import shutil
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
helper = runpy.run_path(str(ROOT / '.github/scripts/analyzer_ci_feed.py'))


class AnalyzerBootstrap(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = 'a' * 40
        self.compiler = b'pinned compiler'
        self.feed = self.root / 'feed'
        self.feed.mkdir()
        project = self.root / 'tools/Ranvier.Analyzers.Host'
        (project / 'tests').mkdir(parents=True)
        (project / 'Ranvier.Analyzers.Host.csproj').write_text('<Project><PropertyGroup><Version>0.1.0-preview.1</Version></PropertyGroup></Project>')
        (project / 'vendor-spec.json').write_text(json.dumps(dict(compilerSha256=hashlib.sha256(self.compiler).hexdigest())))
        analyzer = self.root / 'tools/Ranvier.Analyzers'
        analyzer.mkdir()
        (analyzer / 'Ranvier.Analyzers.fsproj').write_text('<Project><PropertyGroup><Version>0.1.1-preview.2</Version></PropertyGroup></Project>')
        shutil.copy2(ROOT / 'nuget.config', self.root / 'nuget.config')
        shutil.copy2(ROOT / 'tools/Ranvier.Analyzers.Host/tests/write_repository_nuget_config.py', project / 'tests')
        self.package = self.feed / 'renamed.nupkg'
        self.write_package()
        with zipfile.ZipFile(self.feed / 'analyzer.nupkg', 'w') as archive:
            archive.writestr('analyzer.nuspec', '<package><metadata><id>Ranvier.Analyzers</id>'
                             '<version>0.1.1-preview.2</version><repository commit="' + self.source + '"/></metadata></package>')

    def write_package(self, source=None, compiler=None):
        with zipfile.ZipFile(self.package, 'w') as archive:
            archive.writestr('host.nuspec', '<package><metadata><id>Ranvier.Analyzers.Host</id>'
                             '<version>0.1.0-preview.1</version><repository commit="' + (source or self.source) + '"/></metadata></package>')
            archive.writestr('tools/net10.0/any/FSharp.Compiler.Service.dll', self.compiler if compiler is None else compiler)

    def test_exact_source_host_is_the_only_private_route_and_public_feeds_survive(self):
        helper['record'](self.root, self.feed, self.source)
        output = self.root / 'restore.config'
        helper['configure'](self.root, self.feed, output, self.source)
        config = ET.parse(output).getroot()
        sources = {x.get('key'): x.get('value') for x in config.find('packageSources').findall('add')}
        self.assertIn('nuget.org', sources)
        self.assertIn('partas-build', sources)
        self.assertIn('xantham-cloudsmith', sources)
        mapping = config.find('packageSourceMapping').findall('packageSource')
        private = next(x for x in mapping if x.get('key') == 'ci-analyzer-host')
        self.assertEqual([x.get('pattern') for x in private], ['Ranvier.Analyzers.Host', 'Ranvier.Analyzers'])

    def test_other_source_compiler_or_extra_package_is_rejected(self):
        for source, compiler in [('b' * 40, self.compiler), (self.source, b'changed compiler')]:
            with self.subTest(source=source, compiler=compiler):
                self.write_package(source, compiler)
                with self.assertRaises(ValueError):
                    helper['record'](self.root, self.feed, self.source)
        self.write_package()
        shutil.copy2(self.package, self.feed / 'extra.nupkg')
        with self.assertRaises(ValueError):
            helper['record'](self.root, self.feed, self.source)

    def test_changed_package_cannot_use_an_old_producer_receipt(self):
        helper['record'](self.root, self.feed, self.source)
        with zipfile.ZipFile(self.package, 'a') as archive:
            archive.writestr('new-payload', b'changed after upload')
        with self.assertRaisesRegex(ValueError, 'changed after'):
            helper['configure'](self.root, self.feed, self.root / 'restore.config', self.source)

    def test_ci_analyzer_restores_require_the_shared_bootstrap_before_building(self):
        text = (ROOT / '.github/workflows/ci.yml').read_text()
        import re
        for job in ['analyzer-tests', 'analyze', 'packages']:
            with self.subTest(job=job):
                match = re.search(r'(?ms)^  ' + job + r':\n(.*?)(?=^  [A-Za-z0-9_-]+:|\Z)', text)
                self.assertIsNotNone(match)
                block = match.group(1)
                self.assertIn('needs: analyzer-bootstrap', block)
                self.assertLess(block.index('uses: ./.github/actions/use-analyzer-bootstrap'), block.index('run: dotnet'))
        views = re.search(r'(?ms)^  views:\n(.*?)(?=^  [A-Za-z0-9_-]+:|\Z)', text).group(1)
        self.assertNotIn('needs: analyzer-bootstrap', views)
        self.assertNotIn('arms, analyzers]', views)


if __name__ == '__main__':
    unittest.main()

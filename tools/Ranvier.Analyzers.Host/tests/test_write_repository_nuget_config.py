"""XML and routing controls for repository-wide NuGet qualification config generation."""

from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile
from unittest import mock


SCRIPT = Path(__file__).with_name("write_repository_nuget_config.py")
SPEC = importlib.util.spec_from_file_location("write_repository_nuget_config", SCRIPT)
CONFIG = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(CONFIG)
ROOT_CONFIG = Path(__file__).resolve().parents[3] / "nuget.config"


def local(element, name):
    return [child for child in element if CONFIG._local_name(child.tag) == name]


def write_nupkg(path: Path, package_id: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec",
                         f"<package><metadata><id>{package_id}</id><version>0.1.0-test</version></metadata></package>")


class RepositoryNuGetConfigTests(unittest.TestCase):
    def test_cloudsmith_mappings_are_preserved_and_private_ids_route_exactly(self):
        self.assertTrue(ROOT_CONFIG.is_file())
        with tempfile.TemporaryDirectory(prefix="repository-feed-config-") as temporary:
            root = Path(temporary)
            analyzer_feed = root / "private-analyzers"
            dom_feed = root / "private-dom55"
            write_nupkg(analyzer_feed / "analyzers.nupkg", "Ranvier.Analyzers")
            write_nupkg(analyzer_feed / "host.nupkg", "Ranvier.Analyzers.Host")
            write_nupkg(dom_feed / "dom.nupkg", "Ranvier.Dom")
            write_nupkg(dom_feed / "dom-traced.nupkg", "Ranvier.Dom.Traced")
            specs = [
                {"key": "ranvier-analyzer-feed", "path": str(analyzer_feed),
                 "packageIds": ["Ranvier.Analyzers", "Ranvier.Analyzers.Host"]},
                {"key": "ranvier-dom55-feed", "path": str(dom_feed),
                 "packageIds": ["Ranvier.Dom", "Ranvier.Dom.Traced"]},
            ]
            output = root / "artifacts" / "repository.NuGet.Config"
            result = CONFIG.build_repository_config(ROOT_CONFIG, output, specs)
            original = ET.parse(ROOT_CONFIG).getroot()
            generated = ET.parse(output).getroot()
            original_sources = {node.get("key"): node.get("value")
                                for node in local(local(original, "packageSources")[0], "add")}
            generated_sources = {node.get("key"): node.get("value")
                                 for node in local(local(generated, "packageSources")[0], "add")}
            for key, value in original_sources.items():
                self.assertEqual(generated_sources[key], value)
            self.assertIn("xantham-cloudsmith", generated_sources)

            original_mapping = {
                source.get("key"): [(item.get("pattern")) for item in local(source, "package")]
                for source in local(local(original, "packageSourceMapping")[0], "packageSource")
            }
            generated_mapping = {
                source.get("key"): [(item.get("pattern")) for item in local(source, "package")]
                for source in local(local(generated, "packageSourceMapping")[0], "packageSource")
            }
            for key, patterns in original_mapping.items():
                for pattern in patterns:
                    self.assertIn(pattern, generated_mapping[key])
            self.assertIn("Xantham.*", generated_mapping["xantham-cloudsmith"])
            self.assertIn("*", generated_mapping["nuget.org"])
            self.assertEqual(generated_mapping["ranvier-analyzer-feed"],
                             ["Ranvier.Analyzers", "Ranvier.Analyzers.Host"])
            self.assertEqual(generated_mapping["ranvier-dom55-feed"], ["Ranvier.Dom", "Ranvier.Dom.Traced"])
            self.assertEqual(result["privatePackages"]["Ranvier.Dom"]["source"],
                             "ranvier-dom55-feed")

    def test_private_ids_must_be_present_in_feed_and_never_wildcard_routes(self):
        with tempfile.TemporaryDirectory(prefix="repository-feed-negative-") as temporary:
            root = Path(temporary)
            feed = root / "feed"
            write_nupkg(feed / "analyzers.nupkg", "Ranvier.Analyzers")
            write_nupkg(feed / "host.nupkg", "Ranvier.Analyzers.Host")
            output = root / "out.config"
            missing = [{"key": "private", "path": str(feed),
                        "packageIds": ["Ranvier.Analyzers", "Ranvier.Analyzers.Host", "Ranvier.View.Dom"]}]
            with self.assertRaisesRegex(ValueError, "does not contain"):
                CONFIG.build_repository_config(ROOT_CONFIG, output, missing)
            wildcard = [{"key": "private", "path": str(feed), "packageIds": ["Ranvier.*"]}]
            with self.assertRaisesRegex(ValueError, "exact IDs"):
                CONFIG.build_repository_config(ROOT_CONFIG, output, wildcard)

    def test_machine_package_cache_cannot_be_used_as_private_source(self):
        with tempfile.TemporaryDirectory(prefix="repository-feed-cache-") as temporary:
            feed = Path(temporary) / ".nuget" / "packages"
            write_nupkg(feed / "a.nupkg", "Ranvier.Analyzers")
            write_nupkg(feed / "h.nupkg", "Ranvier.Analyzers.Host")
            specs = [{"key": "private", "path": str(feed),
                      "packageIds": ["Ranvier.Analyzers", "Ranvier.Analyzers.Host"]}]
            with mock.patch.dict(os.environ, {"NUGET_PACKAGES": str(feed)}):
                with self.assertRaisesRegex(ValueError, "machine/package cache"):
                    CONFIG.build_repository_config(ROOT_CONFIG, Path(temporary) / "out.config", specs)

    def test_ambiguous_exact_mapping_to_public_source_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="repository-feed-ambiguous-") as temporary:
            root = Path(temporary)
            feed = root / "feed"
            write_nupkg(feed / "a.nupkg", "Ranvier.Analyzers")
            write_nupkg(feed / "h.nupkg", "Ranvier.Analyzers.Host")
            source = root / "nuget.config"
            source.write_text('''<configuration>
  <packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources>
  <packageSourceMapping><clear/><packageSource key="nuget.org"><package pattern="*"/><package pattern="Ranvier.Analyzers"/></packageSource></packageSourceMapping>
</configuration>''', encoding="utf-8")
            specs = [{"key": "private", "path": str(feed),
                      "packageIds": ["Ranvier.Analyzers", "Ranvier.Analyzers.Host"]}]
            with self.assertRaisesRegex(ValueError, "already mapped to another source"):
                CONFIG.build_repository_config(source, root / "out.config", specs)


if __name__ == "__main__":
    unittest.main(verbosity=2)

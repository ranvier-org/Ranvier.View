"""Controls that reject incomplete full-group evidence."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).with_name("verify_analyzer_group.py")
SPEC = importlib.util.spec_from_file_location("verify_analyzer_group", SCRIPT)
VERIFY = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(VERIFY)


class GroupVerifierControls(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="analyzer-group-verifier-")
        self.root = Path(self.temp.name).resolve()
        self.projects = [self.root / "src" / "One.fsproj", self.root / "src" / "Two.fsproj"]
        for project in self.projects:
            project.parent.mkdir(parents=True, exist_ok=True)
            project.write_text("<Project />", encoding="utf-8")
        self.report = self.root / "artifacts" / "analyzers" / "ranvier-library.sarif"
        self.report.parent.mkdir(parents=True)
        self._write_sarif()
        self.feed = self.root / "feed"
        self.feed.mkdir()
        self.cache = self.root / "cache"
        self.cache.mkdir()
        self.provenance = self.root / "provenance"
        self.provenance.mkdir()
        self.evidence = self.root / "verified"
        self.manifest = {
            "group": "library",
            "projects": [str(project.relative_to(self.root)) for project in self.projects],
            "excludeFiles": ["**/obj/**"],
        }
        self.arguments = [
            "--project", *(str(project) for project in self.projects),
            "--exclude-files", "**/obj/**",
            "-c", "Release",
            "--fail-on-type-errors",
            "--treat-as-error", "RAN*",
            "--code-root", str(self.root),
            "--report", str(self.report),
        ]
        self._write_receipt()
        self.calls = []

    def tearDown(self):
        self.temp.cleanup()

    def _write_sarif(self, *, successful=True, runs=True):
        document = {"version": "2.1.0", "runs": ([{
            "tool": {"driver": {"name": "Ionide.Analyzers.Cli"}},
            "invocations": [{"executionSuccessful": successful}],
            # Empty artifacts/results are valid when the analyzers produce no findings.
            "artifacts": [], "results": [],
        }] if runs else [])}
        self.report.write_text(json.dumps(document), encoding="utf-8")

    def _write_receipt(self, *, arguments=None):
        receipt = {"arguments": list(self.arguments if arguments is None else arguments),
                   "workingDirectory": str(self.root)}
        (self.provenance / "host-123.json").write_text(json.dumps(receipt), encoding="utf-8")

    def _fake_consumer_helpers(self):
        def discover(feed):
            self.calls.append(("discover", Path(feed)))
            return object()

        def installed(cache, plan):
            self.calls.append(("installed", Path(cache), plan))
            return {"compilerSha256": "a" * 64}

        def verify(provenance, payload, expected_heap, evidence):
            self.calls.append(("verify", Path(provenance), payload, expected_heap, Path(evidence)))
            return [{"processId": 123, "compilerSha256": payload["compilerSha256"]}]

        return {"discover_private_packages": discover,
                "_installed_host_assets": installed,
                "_verify_host_receipts": verify}

    def _verify(self):
        return VERIFY.verify_group("library", self.manifest, self.root, self.report,
                                   self.feed, self.cache, self.provenance, self.evidence)

    def test_requires_complete_project_selection_and_reuses_identity_verifier(self):
        with mock.patch.object(VERIFY, "_load_consumer_helpers", return_value=self._fake_consumer_helpers()):
            result = self._verify()
        self.assertEqual(set(result["expectedProjects"]), {str(p) for p in self.projects})
        self.assertEqual(result["groupInvocationReceipt"], str((self.provenance / "host-123.json").resolve()))
        self.assertEqual([call[0] for call in self.calls], ["discover", "installed", "verify"])
        self.assertEqual(self.calls[-1][3], 4 * 1024**3)

    def test_successful_sarif_and_exit_like_state_cannot_hide_missing_project(self):
        self._write_receipt(arguments=[
            "--project", str(self.projects[0]),
            "--exclude-files", "**/obj/**", "-c", "Release",
            "--fail-on-type-errors", "--treat-as-error", "RAN*",
            "--code-root", str(self.root), "--report", str(self.report),
        ])
        with mock.patch.object(VERIFY, "_load_consumer_helpers", return_value=self._fake_consumer_helpers()):
            with self.assertRaisesRegex(ValueError, "full-group CLI invocation"):
                self._verify()

    def test_project_filters_or_changed_file_excludes_are_rejected(self):
        self._write_receipt(arguments=self.arguments + ["--exclude-projects", "Two.fsproj"])
        with self.assertRaises(ValueError):
            VERIFY.validate_host_arguments("library", self.arguments + ["--exclude-projects", "Two.fsproj"],
                                            self.manifest, self.root, self.report)
        altered = list(self.arguments)
        altered[altered.index("**/obj/**")] = "**/*.fs"
        with self.assertRaisesRegex(ValueError, "excluded-file selection"):
            VERIFY.validate_host_arguments("library", altered, self.manifest, self.root, self.report)

    def test_invalid_or_unsuccessful_sarif_is_rejected(self):
        self._write_sarif(successful=False)
        with self.assertRaisesRegex(ValueError, "failed or unverified"):
            VERIFY.validate_sarif(self.report)
        self._write_sarif(runs=False)
        with self.assertRaisesRegex(ValueError, "at least one run"):
            VERIFY.validate_sarif(self.report)

    def test_relative_project_arguments_cannot_use_another_working_directory(self):
        arguments = list(self.arguments)
        arguments[1:3] = [str(project.relative_to(self.root)) for project in self.projects]
        self._write_receipt(arguments=arguments)
        path = self.provenance / "host-123.json"
        receipt = json.loads(path.read_text(encoding="utf-8"))
        receipt["workingDirectory"] = str(self.root / "another-root")
        path.write_text(json.dumps(receipt), encoding="utf-8")
        with mock.patch.object(VERIFY, "_load_consumer_helpers", return_value=self._fake_consumer_helpers()):
            with self.assertRaisesRegex(ValueError, "full-group CLI invocation"):
                self._verify()


if __name__ == "__main__":
    unittest.main(verbosity=2)

"""Focused Python-only controls for the provider gate."""

from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


RUNNER_PATH = Path(__file__).with_name("run_provider_qualification.py")
SPEC = importlib.util.spec_from_file_location("provider_qualification", RUNNER_PATH)
RUNNER = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(RUNNER)

RETAINED_LOG = Path(__file__).parent / "fixtures" / "expecto234-summary.log"


class ProviderQualificationControls(unittest.TestCase):
    def test_real_ansi_expecto_transcript_is_exact_full_run(self):
        self.assertTrue(RETAINED_LOG.is_file(), f"missing retained proof input: {RETAINED_LOG}")
        text = RETAINED_LOG.read_text(encoding="utf-8", errors="replace")
        summary = RUNNER.parse_expecto_summary(text)
        RUNNER.require_full_expecto(summary)
        self.assertEqual(summary, {"run": 234, "passed": 234, "ignored": 0,
                                   "failed": 0, "errored": 0})

    def test_expecto_count_skip_and_failure_controls_are_rejected(self):
        real = RETAINED_LOG.read_text(encoding="utf-8", errors="replace")
        clean = RUNNER.ANSI.sub("", real)
        mutations = (
            clean.replace("234 tests run", "233 tests run", 1),
            clean.replace("234 passed", "233 passed", 1),
            clean.replace("0 ignored", "1 ignored", 1),
            clean.replace("0 failed", "1 failed", 1),
            clean.replace("0 errored", "1 errored", 1),
        )
        for mutated in mutations:
            with self.subTest(mutated=mutated[:48]):
                with self.assertRaises(ValueError):
                    RUNNER.require_full_expecto(RUNNER.parse_expecto_summary(mutated))
        with self.assertRaises(ValueError):
            RUNNER.parse_expecto_summary("output with no Expecto summary")

    def test_success_gate_rejects_timeout_missing_timeout_result_and_nonzero_exit(self):
        RUNNER.require_process_success({"exitCode": 0, "timedOut": False})
        for receipt in ({"exitCode": 0, "timedOut": True},
                        {"exitCode": 0}, {"exitCode": 7, "timedOut": False}):
            with self.subTest(receipt=receipt):
                with self.assertRaises(ValueError):
                    RUNNER.require_process_success(receipt)

    def test_provider_receipt_must_match_actual_targetdir_assembly(self):
        with tempfile.TemporaryDirectory(prefix="provider-receipt-control-") as directory:
            root = Path(directory)
            project = root / "tools" / "Ranvier.Analyzers.Tests" / "Ranvier.Analyzers.Tests.fsproj"
            project.parent.mkdir(parents=True)
            project.write_text("<Project />", encoding="utf-8")
            output = project.parent / "bin" / "Release" / "net10.0"
            output.mkdir(parents=True)
            compiler = output / "FSharp.Compiler.Service.dll"
            compiler.write_bytes(b"private compiler test bytes")
            digest = hashlib.sha256(compiler.read_bytes()).hexdigest()
            provenance = root / "provenance"
            provenance.mkdir()
            receipt_path = provenance / "provider-123.json"
            receipt = {
                "compilerPath": str(compiler),
                "compilerVersion": "43.12.402.0",
                "compilerPublicKeyToken": "",
                "compilerSha256": digest,
                "expectedCompilerSha256": digest,
                "matches": True,
                "arguments": ["--sequenced"],
                "gcTotalAvailableMemoryBytes": 4 * 1024**3,
                "gcIsServer": False,
                "gcHeapHardLimitEnvironment": "0x100000000",
            }
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            self.assertEqual(RUNNER._provider_receipt(provenance, digest, project)["value"], receipt)

            receipt["matches"] = False
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "identity gate"):
                RUNNER._provider_receipt(provenance, digest, project)

            receipt["matches"] = True
            receipt["gcTotalAvailableMemoryBytes"] = 1
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "4 GiB"):
                RUNNER._provider_receipt(provenance, digest, project)


if __name__ == "__main__":
    unittest.main(verbosity=2)

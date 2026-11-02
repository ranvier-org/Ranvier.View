#!/usr/bin/env python3
"""Run the complete unfiltered analyzer provider suite with runtime identity evidence.

This command does not build or restore: CI must first produce the Release test output with the
private Host package reference and copied FSharp.Compiler.Service.dll in TargetDir.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import sys
import time


EXPECTED_TESTS = 234
EXPECTED_COMPILER_VERSION = "43.12.402.0"
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
EXPECTO_SUMMARY = re.compile(
    r"EXPECTO!\s*(\d+)\s+tests?\s+run\b.*?"
    r"(\d+)\s+passed,\s*(\d+)\s+ignored,\s*"
    r"(\d+)\s+failed,\s*(\d+)\s+errored",
    re.IGNORECASE | re.DOTALL,
)


def require_full_expecto(summary: dict[str, int]) -> None:
    expected = {"run": EXPECTED_TESTS, "passed": EXPECTED_TESTS,
                "ignored": 0, "failed": 0, "errored": 0}
    if summary != expected:
        raise ValueError(f"Expected exactly {expected}; got {summary}")


def require_process_success(process_receipt: dict) -> None:
    if process_receipt.get("timedOut") is not False:
        raise ValueError("provider test process timed out or has no timeout result")
    if process_receipt.get("exitCode") != 0:
        raise ValueError(f"provider test process exited with {process_receipt.get('exitCode')!r}")


def parse_expecto_summary(log_text: str) -> dict[str, int]:
    """Parse the actual Expecto summary shape used by retained 234-test logs."""
    text = ANSI.sub("", log_text).replace("\r", "")
    matches = list(EXPECTO_SUMMARY.finditer(text))
    if len(matches) != 1:
        raise ValueError(f"Expected exactly one Expecto summary; found {len(matches)}")
    total, passed, ignored, failed, errored = (int(value) for value in matches[0].groups())
    return {"run": total, "passed": passed, "ignored": ignored,
            "failed": failed, "errored": errored}


def _load_bounded_runner(project: Path):
    helper = project.parent.parent / "Ranvier.Analyzers.Host" / "tests" / "test_consumer.py"
    if not helper.is_file():
        raise FileNotFoundError(f"Existing owned-process runner not found: {helper}")
    # run_path with a non-main name defines the existing helpers without running its CLI/tests.
    module = runpy.run_path(str(helper), run_name="provider_qualification_helpers")
    return (module["run_command"], module["_remove_child_environment_overrides"],
            module["REPO_ROOT"], module["ensure_outside_checkout"])


def _provider_receipt(provenance_dir: Path, expected_sha: str, project: Path) -> dict:
    receipts = sorted(provenance_dir.glob("provider-*.json"))
    if len(receipts) != 1:
        raise ValueError(f"Expected exactly one fresh provider-*.json receipt; found {len(receipts)}")
    receipt_path = receipts[0]
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    required = ("compilerPath", "compilerVersion", "compilerPublicKeyToken",
                "compilerSha256", "expectedCompilerSha256", "matches", "arguments",
                "gcTotalAvailableMemoryBytes", "gcIsServer", "gcHeapHardLimitEnvironment")
    missing = [key for key in required if key not in receipt]
    if missing:
        raise ValueError(f"Provider receipt is missing fields: {', '.join(missing)}")
    actual_sha = str(receipt["compilerSha256"]).lower()
    receipt_expected = str(receipt["expectedCompilerSha256"]).lower()
    if actual_sha != expected_sha:
        raise ValueError(f"Loaded compiler hash {actual_sha} does not match requested {expected_sha}")
    if receipt_expected != expected_sha or receipt["matches"] is not True:
        raise ValueError("Provider's in-process compiler identity gate did not match the expected SHA256")
    if receipt["compilerVersion"] != EXPECTED_COMPILER_VERSION:
        raise ValueError(f"Unexpected FSharp.Compiler.Service version: {receipt['compilerVersion']!r}")
    if receipt["compilerPublicKeyToken"] != "":
        raise ValueError("Expected the private candidate assembly with an empty public key token")
    if receipt["arguments"] != ["--sequenced"]:
        raise ValueError(f"Provider did not receive the unfiltered sequenced arguments: {receipt['arguments']!r}")
    if receipt["gcTotalAvailableMemoryBytes"] != 4 * 1024**3:
        raise ValueError("Provider runtime did not observe the required 4 GiB GC memory limit")
    if receipt["gcIsServer"] is not False:
        raise ValueError("Provider runtime unexpectedly enabled server GC")
    if receipt["gcHeapHardLimitEnvironment"] != "0x100000000":
        raise ValueError("Provider did not observe the exact child-only GC heap limit environment")
    compiler_path = Path(receipt["compilerPath"]).resolve()
    output_dir = (project.parent / "bin" / "Release" / "net10.0").resolve()
    try:
        compiler_path.relative_to(output_dir)
    except ValueError as error:
        raise ValueError(f"Loaded compiler is outside the test project's Release TargetDir: {compiler_path}") from error
    if compiler_path.name.lower() != "fsharp.compiler.service.dll" or not compiler_path.is_file():
        raise ValueError(f"Loaded compiler path is absent or has an unexpected filename: {compiler_path}")
    on_disk_sha = hashlib.sha256(compiler_path.read_bytes()).hexdigest()
    if on_disk_sha != expected_sha:
        raise ValueError(f"Release TargetDir compiler bytes hash to {on_disk_sha}, expected {expected_sha}")
    return {"path": str(receipt_path.resolve()),
            "sha256": hashlib.sha256(receipt_path.read_bytes()).hexdigest(),
            "value": receipt}


def _child_environment(remove_overrides, provenance_dir: Path) -> dict[str, str]:
    env = os.environ.copy()
    remove_overrides(env)
    # Set a child-only 4 GiB cap. Other inherited GC-limit spellings remain removed.
    env["DOTNET_GCHeapHardLimit"] = "0x100000000"
    env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    env["MSBUILDDISABLENODEREUSE"] = "1"
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    env["RANVIER_ANALYZER_TEST_COMPILER_SHA256"] = ""
    env["RANVIER_ANALYZER_TEST_PROVENANCE_DIR"] = str(provenance_dir.resolve())
    return env


def run_qualification(args: argparse.Namespace) -> dict:
    dotnet = str(Path(args.dotnet).resolve()) if Path(args.dotnet).exists() else args.dotnet
    project = args.project.resolve()
    evidence_dir = args.evidence_dir.resolve()
    expected_sha = args.compiler_sha256.lower()
    if not re.fullmatch(r"[0-9a-f]{64}", expected_sha):
        raise ValueError("--compiler-sha256 must be exactly 64 hexadecimal characters")
    if not project.is_file():
        raise FileNotFoundError(f"Test project does not exist: {project}")
    run_command, remove_overrides, repo_root, ensure_outside = _load_bounded_runner(project)
    ensure_outside(evidence_dir, repo_root)
    if evidence_dir.exists() and any(evidence_dir.iterdir()):
        raise ValueError(f"Evidence directory must be new or empty to prevent stale-receipt acceptance: {evidence_dir}")
    evidence_dir.mkdir(parents=True, exist_ok=True)
    provenance_dir = evidence_dir / "provider-provenance"
    provenance_dir.mkdir()
    log_path = evidence_dir / "provider-tests.log"
    command = [dotnet, "run", "--project", str(project), "-c", "Release", "--no-build", "--", "--sequenced"]
    env = _child_environment(remove_overrides, provenance_dir)
    env["RANVIER_ANALYZER_TEST_COMPILER_SHA256"] = expected_sha
    started = time.monotonic()
    try:
        process_receipt = run_command(command, project.parent, env, log_path, args.timeout_seconds)
    except BaseException as error:
        failure = {
            "command": command,
            "workingDirectory": str(project.parent),
            "durationSeconds": round(time.monotonic() - started, 3),
            "logPath": str(log_path),
            "logSha256": hashlib.sha256(log_path.read_bytes()).hexdigest() if log_path.is_file() else None,
            "expectedCompilerSha256": expected_sha,
            "passed": False,
            "failures": [f"bounded runner failed: {type(error).__name__}: {error}"],
        }
        (evidence_dir / "provider-qualification.json").write_text(
            json.dumps(failure, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        raise RuntimeError(f"Bounded provider process failed; see {evidence_dir / 'provider-qualification.json'}") from error
    log_text = log_path.read_text(encoding="utf-8", errors="replace")
    result = {
        "command": command,
        "workingDirectory": str(project.parent),
        "exitCode": process_receipt["exitCode"],
        "timedOut": process_receipt["timedOut"],
        "timeoutReason": process_receipt["timeoutReason"],
        "durationSeconds": round(time.monotonic() - started, 3),
        "logPath": str(log_path),
        "logSha256": hashlib.sha256(log_path.read_bytes()).hexdigest(),
        "process": process_receipt,
        "expectedCompilerSha256": expected_sha,
    }
    failures = []
    try:
        require_process_success(process_receipt)
    except ValueError as error:
        failures.append(str(error))
    try:
        summary = parse_expecto_summary(log_text)
        result["expecto"] = summary
        require_full_expecto(summary)
    except ValueError as error:
        failures.append(str(error))
        result["expectoError"] = str(error)
    try:
        receipt = _provider_receipt(provenance_dir, expected_sha, project)
        result["providerReceipt"] = receipt
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        failures.append(str(error))
        result["providerReceiptError"] = str(error)
    result["passed"] = not failures
    result["failures"] = failures
    summary_path = evidence_dir / "provider-qualification.json"
    summary_path.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if failures:
        raise RuntimeError(f"Provider qualification failed; see {summary_path}: {'; '.join(failures)}")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True, help="dotnet executable")
    parser.add_argument("--project", required=True, type=Path, help="Ranvier.Analyzers.Tests.fsproj")
    parser.add_argument("--evidence-dir", required=True, type=Path, help="new directory outside the checkout")
    parser.add_argument("--compiler-sha256", required=True, help="expected loaded candidate compiler SHA256")
    parser.add_argument("--timeout-seconds", type=float, default=540,
                        help="positive whole-provider deadline (default: 540 seconds)")
    parser.add_argument("--validate-retained-log", type=Path,
                        help="parse a retained real Expecto transcript without running .NET")
    args = parser.parse_args(argv)
    if args.timeout_seconds <= 0:
        parser.error("--timeout-seconds must be positive")
    try:
        if args.validate_retained_log:
            summary = parse_expecto_summary(args.validate_retained_log.read_text(encoding="utf-8", errors="replace"))
            print(json.dumps(summary, sort_keys=True))
            return 0 if summary == {"run": EXPECTED_TESTS, "passed": EXPECTED_TESTS,
                                   "ignored": 0, "failed": 0, "errored": 0} else 1
        result = run_qualification(args)
        print(f"Provider qualification PASS: {result['expecto']['passed']} tests; receipt hash matched.")
        return 0
    except Exception as error:
        print(f"Provider qualification ERROR: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

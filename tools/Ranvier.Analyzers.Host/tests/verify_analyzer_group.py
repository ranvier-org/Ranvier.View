#!/usr/bin/env python3
"""Verify actual execution for an unfiltered analyzer group CI leg.

The expected project manifest and exact launched command are explicit inputs. Runtime
identity is delegated to the repository's established external-consumer verifier.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys


GROUPS = {"library", "view", "examples"}


def _load_consumer_helpers(repo_root: Path):
    helper_path = repo_root / "tools/Ranvier.Analyzers.Host/tests/test_consumer.py"
    if not helper_path.is_file():
        raise FileNotFoundError(f"Existing package/receipt verifier is missing: {helper_path}")
    return runpy.run_path(str(helper_path), run_name="analyzer_group_verifier_helpers")


def _option_values(arguments: list[str], option: str) -> list[str]:
    values: list[str] = []
    index = 0
    while index < len(arguments):
        token = arguments[index]
        if token.startswith(option + "="):
            values.append(token.split("=", 1)[1])
            index += 1
            continue
        if token != option:
            index += 1
            continue
        index += 1
        count = 0
        while index < len(arguments) and not arguments[index].startswith("-"):
            values.append(arguments[index])
            index += 1
            count += 1
        if count == 0:
            raise ValueError(f"Analyzer CLI option {option} has no value")
    return values


def _has_pair(arguments: list[str], option: str, value: str) -> bool:
    return any(token == f"{option}{value}" for token in arguments) or any(
        arguments[index] == option and index + 1 < len(arguments) and arguments[index + 1] == value
        for index in range(len(arguments)))


def _resolve_cli_path(value: str, project_root: Path) -> Path:
    path = Path(value)
    return (path if path.is_absolute() else project_root / path).resolve()


def validate_host_arguments(group: str, arguments: list[str], project_manifest: dict,
                            project_root: Path, report_path: Path) -> None:
    if group not in GROUPS:
        raise ValueError(f"Unknown analyzer group: {group!r}")
    expected = _validated_expected_projects(project_manifest, group, project_root)
    project_values = _option_values(arguments, "--project")
    actual_projects = {_resolve_cli_path(value, project_root) for value in project_values}
    if len(project_values) != len(actual_projects) or actual_projects != expected:
        raise ValueError(f"Host CLI project selection mismatch; expected={sorted(map(str, expected))}, "
                         f"actual={sorted(map(str, actual_projects))}")
    expected_excludes = project_manifest.get("excludeFiles")
    actual_excludes = _option_values(arguments, "--exclude-files")
    if not isinstance(expected_excludes, list) or actual_excludes != expected_excludes:
        raise ValueError(f"Host CLI excluded-file selection mismatch; expected={expected_excludes!r}, "
                         f"actual={actual_excludes!r}")
    lowered = [argument.casefold() for argument in arguments]
    disallowed = {"--filter", "--filter-test-case", "--list-tests", "--exclude-project",
                  "--exclude-projects", "--only-project", "--only-projects", "--project-filter"}
    if any(argument in disallowed or any(argument.startswith(option + "=") for option in disallowed)
           for argument in lowered):
        raise ValueError("Host CLI contains a test/project selection filter")
    if not _has_pair(arguments, "-c", "Release"):
        raise ValueError("Host CLI is missing the required Release configuration")
    if "--fail-on-type-errors" not in arguments:
        raise ValueError("Host CLI is missing --fail-on-type-errors")
    if not _has_pair(arguments, "--treat-as-error", "RAN*"):
        raise ValueError("Host CLI is missing the required RAN* treat-as-error policy")
    code_root = _option_values(arguments, "--code-root")
    if len(code_root) != 1 or _resolve_cli_path(code_root[0], project_root) != project_root.resolve():
        raise ValueError(f"Host CLI code root does not match repository: {code_root!r}")
    report_values = _option_values(arguments, "--report")
    if len(report_values) != 1 or _resolve_cli_path(report_values[0], project_root) != _resolve_cli_path(str(report_path), project_root):
        raise ValueError(f"Host CLI report path does not match expected SARIF: {report_values!r}")


def validate_sarif(report_path: Path) -> dict:
    document = json.loads(report_path.read_text(encoding="utf-8-sig"))
    if document.get("version") != "2.1.0":
        raise ValueError(f"SARIF version must be 2.1.0: {document.get('version')!r}")
    runs = document.get("runs")
    if not isinstance(runs, list) or not runs:
        raise ValueError("SARIF must contain at least one run")
    for index, run in enumerate(runs):
        driver = run.get("tool", {}).get("driver", {})
        if driver.get("name") != "Ionide.Analyzers.Cli":
            raise ValueError(f"SARIF run {index} is not from Ionide.Analyzers.Cli")
        invocations = run.get("invocations")
        if not isinstance(invocations, list) or not invocations:
            raise ValueError(f"SARIF run {index} has no invocation evidence")
        if any(invocation.get("executionSuccessful") is not True for invocation in invocations):
            raise ValueError(f"SARIF run {index} includes a failed or unverified invocation")
    return document


def _validated_expected_projects(project_manifest: dict, group: str, project_root: Path) -> set[Path]:
    if project_manifest.get("group") != group:
        raise ValueError(f"Project manifest group {project_manifest.get('group')!r} does not match {group!r}")
    items = project_manifest.get("projects")
    if not isinstance(items, list) or not items or any(not isinstance(item, str) or not item for item in items):
        raise ValueError("Project manifest must contain a nonempty list of project paths")
    root = project_root.resolve()
    result: set[Path] = set()
    for item in items:
        path = (root / item).resolve()
        try:
            path.relative_to(root)
        except ValueError as error:
            raise ValueError(f"Expected project path escapes the repository: {item}") from error
        if path.suffix.lower() != ".fsproj":
            raise ValueError(f"Expected project entry is not an .fsproj: {item}")
        if path in result:
            raise ValueError(f"Duplicate expected project path: {item}")
        result.add(path)
    return result


def verify_group(group: str, project_manifest: dict,
                 project_root: Path, report_path: Path, feed: Path, cache: Path,
                 provenance_dir: Path, evidence_dir: Path) -> dict:
    if group not in GROUPS:
        raise ValueError(f"Unknown analyzer group: {group!r}")
    expected_projects = _validated_expected_projects(project_manifest, group, project_root)
    for project in expected_projects:
        if not project.is_file():
            raise ValueError(f"Expected project does not exist: {project}")

    sarif = validate_sarif(report_path)
    helpers = _load_consumer_helpers(project_root.resolve())
    plan = helpers["discover_private_packages"](feed)
    payload = helpers["_installed_host_assets"](cache, plan)
    evidence_dir.mkdir(parents=True, exist_ok=True)
    # Keep all package/compiler/GC assertions in the existing shared verifier.
    copied_receipts = helpers["_verify_host_receipts"](
        provenance_dir, payload, 4 * 1024**3, evidence_dir)

    source_receipts = sorted(provenance_dir.glob("host-*.json"))
    if not source_receipts:
        raise ValueError("No actual host process receipts were produced")
    matching_group_invocations = []
    receipt_digests = []
    for path in source_receipts:
        receipt = json.loads(path.read_text(encoding="utf-8"))
        args = receipt.get("arguments")
        if not isinstance(args, list) or any(not isinstance(arg, str) for arg in args):
            raise ValueError(f"Host receipt lacks actual argv strings: {path.name}")
        try:
            if not isinstance(receipt.get("workingDirectory"), str) or Path(receipt["workingDirectory"]).resolve() != project_root.resolve():
                raise ValueError("The full-group host did not run from the repository root")
            validate_host_arguments(group, args, project_manifest, project_root, report_path)
            matching_group_invocations.append(path)
        except ValueError:
            # Other valid host starts (e.g. source builds) may share the evidence directory.
            pass
        receipt_digests.append({"path": str(path.resolve()),
                                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                "workingDirectory": receipt.get("workingDirectory"), "arguments": args})
    if len(matching_group_invocations) != 1:
        raise ValueError(f"Expected exactly one full-group CLI invocation receipt; "
                         f"found {len(matching_group_invocations)}")

    return {
        "group": group,
        "expectedProjects": sorted(str(path) for path in expected_projects),
        "groupInvocationReceipt": str(matching_group_invocations[0].resolve()),
        "hostReceipts": copied_receipts,
        "hostInvocationEvidence": receipt_digests,
        "sarifPath": str(report_path.resolve()),
        "sarifSha256": hashlib.sha256(report_path.read_bytes()).hexdigest(),
        "sarifRunCount": len(sarif["runs"]),
        "compilerSha256": payload["compilerSha256"],
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--group", required=True, choices=sorted(GROUPS))
    parser.add_argument("--project-manifest", required=True, type=Path,
                        help="JSON object {group, projects:[repo-relative .fsproj paths], excludeFiles:[...]}")
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--feed", required=True, type=Path)
    parser.add_argument("--cache", required=True, type=Path)
    parser.add_argument("--provenance-dir", required=True, type=Path)
    parser.add_argument("--evidence-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        manifest = json.loads(args.project_manifest.read_text(encoding="utf-8"))
        summary = verify_group(args.group, manifest, args.project_root,
                               args.report, args.feed, args.cache,
                               args.provenance_dir, args.evidence_dir)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return 0
    except Exception as error:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps({
            "group": args.group,
            "passed": False,
            "errorType": type(error).__name__,
            "error": str(error),
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Analyzer group verification failed: {type(error).__name__}: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

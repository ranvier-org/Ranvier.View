import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import runpy

EXPECTED_JOBS = {"compiler", "core-dependencies"}
for platform in ("ubuntu-latest", "windows-latest"):
    EXPECTED_JOBS.update(f"{job} ({platform})" for job in ("host", "candidate-consumers", "providers"))
    EXPECTED_JOBS.update(f"analyze ({platform}, {group})" for group in ("library", "view", "examples"))


def verify_run(run, jobs, expected_source):
    if not re.fullmatch(r"[0-9a-f]{40}", expected_source):
        raise ValueError("Expected source must be a full Git SHA")
    expected = {"name": "Analyzer host qualification", "path": ".github/workflows/analyzer-host.yml",
                "event": "push", "head_sha": expected_source,
                "status": "completed", "conclusion": "success"}
    for field, value in expected.items():
        if run.get(field) != value:
            raise ValueError(f"Qualification run has unexpected {field}")
    if run.get("head_repository", {}).get("full_name") != "ranvier-org/Ranvier.View":
        raise ValueError("Qualification must originate in the owning repository")
    if run.get("head_branch") not in ("main", "master"):
        raise ValueError("Only a qualified main/master release may publish")
    if not run.get("head_commit", {}).get("message", "").startswith(("chore: release ", "chore(release): ")):
        raise ValueError("Qualification source is not a release commit")
    if type(run.get("id")) is not int or run["id"] <= 0:
        raise ValueError("Qualification run ID is missing or invalid")
    names = Counter(job.get("name") for job in jobs)
    if names != Counter({name: 1 for name in EXPECTED_JOBS}):
        raise ValueError("Qualification jobs are incomplete, duplicated or unexpected")
    if any(job.get("status") != "completed" or job.get("conclusion") != "success" for job in jobs):
        raise ValueError("Every qualification job must have completed successfully")
    return {"runId": run["id"], "source": expected_source, "jobs": sorted(names)}


def verify_pair(feed, vendor_spec, consumer_proofs):
    harness = runpy.run_path(str(Path(__file__).with_name("test_consumer.py")), run_name="publication_consumer")
    plan = harness["discover_private_packages"](feed)
    paths = list(Path(feed).glob("*.nupkg"))
    if len(paths) != 2 or {path.resolve() for path in paths} != {plan.host.path.resolve(), plan.analyzers.path.resolve()}:
        raise ValueError("Publication feed must contain exactly the qualified pair")
    root = Path(__file__).resolve().parents[3]
    expected_versions = tuple(
        re.search(r"<Version>([^<]+)</Version>", (root / project).read_text(encoding="utf-8")).group(1)
        for project in ("tools/Ranvier.Analyzers.Host/Ranvier.Analyzers.Host.csproj",
                        "tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj"))
    if (plan.host.version, plan.analyzers.version) != expected_versions:
        raise ValueError("Unexpected publication package versions")
    if plan.manifest != vendor_spec:
        raise ValueError("Host vendor manifest differs from the qualified source specification")
    packages = [{"id": package.package_id, "version": package.version, "file": package.path.name,
                 "sha256": hashlib.sha256(package.path.read_bytes()).hexdigest()}
                for package in (plan.analyzers, plan.host)]
    fingerprint = harness["feed_fingerprint"](feed)
    expected_scenarios = {"positive-default-4g": 0, "positive-override-2g": 0,
                          "invalid-heap-zero": 1, "negative-ran010": 1}
    if set(consumer_proofs) != {"ubuntu-latest", "windows-latest"}:
        raise ValueError("Both platform candidate proofs are required")
    for platform, proof in consumer_proofs.items():
        if proof.get("passed") is not True or proof.get("feedFingerprintSha256") != fingerprint:
            raise ValueError(f"Candidate qualification failed or tested different bytes on {platform}")
        if proof.get("privatePackages") != packages or proof.get("sdkVersion") != "10.0.403":
            raise ValueError(f"Candidate package identities or SDK differ on {platform}")
        if proof.get("restoredHostPayload", {}).get("compilerSha256") != vendor_spec["compilerSha256"]:
            raise ValueError(f"Actual restored compiler differs on {platform}")
        scenarios = proof.get("scenarios", [])
        if Counter(case.get("name") for case in scenarios) != Counter({name: 1 for name in expected_scenarios}):
            raise ValueError(f"Candidate scenarios are incomplete on {platform}")
        for case in scenarios:
            if (case.get("passed") is not True or case.get("restoreExit") != 0
                    or case.get("timedOut") is not False or case.get("error") is not None
                    or case.get("buildExit") != expected_scenarios[case["name"]]):
                raise ValueError(f"Candidate scenario failed on {platform}: {case['name']}")
    return {"packages": packages, "feedFingerprintSha256": fingerprint,
            "compilerSha256": vendor_spec["compilerSha256"], "candidatePlatforms": sorted(consumer_proofs)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-json", type=Path, required=True)
    parser.add_argument("--jobs-json", type=Path, required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--feed", type=Path, required=True)
    parser.add_argument("--vendor-spec", type=Path, required=True)
    parser.add_argument("--candidate-proofs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    run = json.loads(args.run_json.read_text(encoding="utf-8"))
    job_page = json.loads(args.jobs_json.read_text(encoding="utf-8"))
    if job_page.get("total_count") != len(job_page.get("jobs", [])):
        raise ValueError("Incomplete qualification jobs API response")
    result = verify_run(run, job_page["jobs"], args.source)
    proofs = {platform: json.loads((args.candidate_proofs / platform / "external-consumer-proof.json").read_text(encoding="utf-8"))
              for platform in ("ubuntu-latest", "windows-latest")}
    result.update(verify_pair(args.feed, json.loads(args.vendor_spec.read_text(encoding="utf-8")), proofs))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()

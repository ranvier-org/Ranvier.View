import copy
import hashlib
from pathlib import Path
import runpy
import tempfile
import unittest

publication = runpy.run_path(str(Path(__file__).with_name("verify_publication.py")))


class PublicationContract(unittest.TestCase):
    def setUp(self):
        self.sha = "a" * 40
        self.run = {"id": 42, "name": "Analyzer host qualification",
                    "path": ".github/workflows/analyzer-host.yml", "event": "push",
                    "head_branch": "main", "head_sha": self.sha,
                    "head_repository": {"full_name": "ranvier-org/Ranvier.View"},
                    "head_commit": {"message": "chore: release analyzers"},
                    "status": "completed", "conclusion": "success"}
        self.jobs = [{"name": name, "status": "completed", "conclusion": "success"}
                     for name in publication["EXPECTED_JOBS"]]

    def test_accepts_only_the_complete_matching_main_release(self):
        result = publication["verify_run"](self.run, self.jobs, self.sha)
        self.assertEqual(result["runId"], 42)
        self.assertEqual(result["source"], self.sha)
        self.assertEqual(len(result["jobs"]), 14)

    def test_rejects_wrong_origin_workflow_event_branch_source_or_completion(self):
        cases = [("name", "CI"), ("path", ".github/workflows/ci.yml"),
                 ("event", "pull_request"), ("head_branch", "codex/candidate"),
                 ("head_sha", "b" * 40), ("status", "in_progress"),
                 ("conclusion", "failure"),
                 ("head_repository", {"full_name": "someone/Ranvier.View"}),
                 ("head_commit", {"message": "unqualified source change"})]
        for field, value in cases:
            with self.subTest(field=field):
                candidate = copy.deepcopy(self.run)
                candidate[field] = value
                with self.assertRaises(ValueError):
                    publication["verify_run"](candidate, self.jobs, self.sha)

    def test_rejects_missing_duplicate_unexpected_skipped_or_failed_jobs(self):
        for kind in ("missing", "duplicate", "unexpected", "skipped", "failed", "running"):
            with self.subTest(kind=kind):
                jobs = copy.deepcopy(self.jobs)
                if kind == "missing":
                    jobs.pop()
                elif kind == "duplicate":
                    jobs.append(copy.deepcopy(jobs[0]))
                elif kind == "unexpected":
                    jobs[0]["name"] = "different-job"
                elif kind == "running":
                    jobs[0]["status"] = "in_progress"
                else:
                    jobs[0]["conclusion"] = "skipped" if kind == "skipped" else "failure"
                with self.assertRaises(ValueError):
                    publication["verify_run"](self.run, jobs, self.sha)

    def test_rejects_invalid_expected_source_and_missing_run_identity(self):
        for sha in ("", "short", "z" * 40):
            with self.subTest(sha=sha), self.assertRaises(ValueError):
                publication["verify_run"](self.run, self.jobs, sha)
        candidate = copy.deepcopy(self.run)
        candidate.pop("id")
        with self.assertRaises(ValueError):
            publication["verify_run"](candidate, self.jobs, self.sha)

    def test_both_platform_proofs_must_describe_the_exact_pair_and_all_four_scenarios(self):
        harness = runpy.run_path(str(Path(__file__).with_name("test_consumer.py")), run_name="fixture_consumer")
        with tempfile.TemporaryDirectory(prefix="analyzer-publication-test-") as temporary:
            feed = Path(temporary)
            harness["create_fake_feed"](feed)
            plan = harness["discover_private_packages"](feed)
            packages = [{"id": package.package_id, "version": package.version, "file": package.path.name,
                         "sha256": hashlib.sha256(package.path.read_bytes()).hexdigest()}
                        for package in (plan.analyzers, plan.host)]
            proof = {"passed": True, "feedFingerprintSha256": harness["feed_fingerprint"](feed),
                     "privatePackages": packages, "sdkVersion": "10.0.403",
                     "restoredHostPayload": {"compilerSha256": plan.manifest["compilerSha256"]},
                     "scenarios": [{"name": name, "passed": True, "restoreExit": 0,
                                    "buildExit": code, "timedOut": False, "error": None}
                                   for name, code in (("positive-default-4g", 0), ("positive-override-2g", 0),
                                                      ("invalid-heap-zero", 1), ("negative-ran010", 1))]}
            proofs = {platform: copy.deepcopy(proof) for platform in ("ubuntu-latest", "windows-latest")}
            result = publication["verify_pair"](feed, plan.manifest, proofs)
            self.assertEqual(result["packages"], packages)
            for kind in ("platform", "fingerprint", "package", "sdk", "compiler", "scenario", "result", "timeout"):
                with self.subTest(kind=kind):
                    changed = copy.deepcopy(proofs)
                    target = changed["windows-latest"]
                    if kind == "platform":
                        changed.pop("windows-latest")
                    elif kind == "fingerprint":
                        target["feedFingerprintSha256"] = "different"
                    elif kind == "package":
                        target["privatePackages"][0]["sha256"] = "different"
                    elif kind == "sdk":
                        target["sdkVersion"] = "10.0.100"
                    elif kind == "compiler":
                        target["restoredHostPayload"]["compilerSha256"] = "different"
                    elif kind == "scenario":
                        target["scenarios"].pop()
                    elif kind == "result":
                        target["scenarios"][-1]["buildExit"] = 0
                    else:
                        target["scenarios"][0]["timedOut"] = True
                    with self.assertRaises(ValueError):
                        publication["verify_pair"](feed, plan.manifest, changed)
            with self.assertRaisesRegex(ValueError, "specification"):
                publication["verify_pair"](feed, {**plan.manifest, "compilerSourceCommit": "wrong"}, proofs)
            (feed / "unexpected.nupkg").write_bytes(plan.analyzers.path.read_bytes())
            with self.assertRaises(ValueError):
                publication["verify_pair"](feed, plan.manifest, proofs)


if __name__ == "__main__":
    unittest.main()

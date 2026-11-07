"""The analyzer publication accepts a release that keeps already-published analyzer versions."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class AnalyzerPublicationTests(unittest.TestCase):
    def test_both_analyzer_pushes_skip_published_versions(self):
        workflow = (ROOT / '.github/workflows/publish.yml').read_text(encoding='utf-8')
        job = workflow.split('\n  publish-analyzers:\n', 1)[1].split('\n  qualify-public-analyzers:\n', 1)[0]
        pushes = [line for line in job.splitlines() if 'dotnet nuget push' in line]
        self.assertEqual(len(pushes), 2)
        for push in pushes:
            with self.subTest(push=push.strip()):
                self.assertIn('--skip-duplicate', push)

    def test_the_public_pair_is_qualified_after_publication(self):
        workflow = (ROOT / '.github/workflows/publish.yml').read_text(encoding='utf-8')
        job = workflow.split('\n  qualify-public-analyzers:\n', 1)[1]
        self.assertIn('needs: publish-analyzers', job)

    def test_only_a_newly_pushed_pair_is_qualified_against_this_build(self):
        workflow = (ROOT / '.github/workflows/publish.yml').read_text(encoding='utf-8')
        publish = workflow.split('\n  publish-analyzers:\n', 1)[1].split('\n  qualify-public-analyzers:\n', 1)[0]
        self.assertIn('pushed: ${{ steps.push.outputs.pushed }}', publish)
        self.assertIn('"pushed=$($pushed.ToString().ToLowerInvariant())" >> $env:GITHUB_OUTPUT', publish)
        qualify = workflow.split('\n  qualify-public-analyzers:\n', 1)[1]
        self.assertIn("if: needs.publish-analyzers.outputs.pushed == 'true'", qualify.split('\n    runs-on:', 1)[0])


if __name__ == '__main__':
    unittest.main()

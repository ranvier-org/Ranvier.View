import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
RUNNER = ROOT / '.github/scripts/run_analyzer_ci.py'


class AnalyzerRunnerTests(unittest.TestCase):
    def invoke(self, body, seconds=5):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'evidence'
            result = subprocess.run(
                [sys.executable, str(RUNNER), '--output', str(output),
                 '--timeout-seconds', str(seconds), '--interval-seconds', '0.02',
                 '--', sys.executable, '-c', body], capture_output=True, text=True)
            self.assertTrue((output / 'receipt.json').exists(), result.stderr)
            samples = [json.loads(row) for row in (output / 'resources.jsonl').read_text().splitlines()]
            return result, json.loads((output / 'receipt.json').read_text()), samples

    def test_preserves_failure_exit_and_output(self):
        result, receipt, _ = self.invoke("print('actual analyzer diagnostic', flush=True); raise SystemExit(7)")
        self.assertEqual(result.returncode, 7)
        self.assertEqual(receipt['exitCode'], 7)
        self.assertFalse(receipt['timedOut'])
        self.assertIn('actual analyzer diagnostic', result.stdout)
        self.assertGreater(receipt['sampleCount'], 0)

    def test_timeout_is_a_failure_with_evidence(self):
        result, receipt, _ = self.invoke('import time; time.sleep(30)', seconds=0.15)
        self.assertEqual(result.returncode, 124)
        self.assertTrue(receipt['timedOut'])
        self.assertEqual(receipt['exitCode'], 124)

    def test_success_is_not_retried(self):
        result, receipt, _ = self.invoke("print('one invocation')")
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout.count('one invocation'), 1)
        self.assertEqual(receipt['command'][-1], "print('one invocation')")

    @unittest.skipUnless(sys.platform.startswith('linux'), 'CI uses Linux process groups')
    def test_timeout_terminates_descendant_and_records_linux_resources(self):
        body = ("import subprocess,sys,time; child=subprocess.Popen([sys.executable,'-c',"
                "'import time; time.sleep(30)']); print(child.pid,flush=True); time.sleep(30)")
        result, receipt, samples = self.invoke(body, seconds=0.4)
        pid = int(result.stdout.strip())
        state = Path('/proc') / str(pid) / 'stat'
        self.assertTrue(not state.exists() or state.read_text().split(') ', 1)[1].startswith('Z'))
        self.assertEqual(receipt['exitCode'], 124)
        rows = [row for sample in samples for row in sample['processes'] if row['pid'] == pid]
        self.assertTrue(rows)
        self.assertGreater(rows[-1]['rssBytes'], 0)
        self.assertGreater(rows[-1]['startTicks'], 0)
        self.assertIn('meminfo', samples[-1]['system'])

    def test_disables_reusable_build_servers(self):
        body = ("import os; assert os.environ['MSBUILDDISABLENODEREUSE']=='1'; "
                "assert os.environ['DOTNET_CLI_USE_MSBUILD_SERVER']=='0'; "
                "assert os.environ['UseSharedCompilation']=='false'")
        result, _, _ = self.invoke(body)
        self.assertEqual(result.returncode, 0, result.stderr)


if __name__ == '__main__':
    unittest.main()

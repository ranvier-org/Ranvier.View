import unittest
from compare import compare


def sample(p95=10., iterations=600, pending=0):
    return dict(schemaVersion=1, scenario='typing', logicalLines=1000,
                requestedRenderer='software', hidden=True, tracing=False,
                interactive=True, gpuFramePipeline=False, boundedImeContext=True,
                iterations=iterations, pendingWork=pending, turnP95Ms=p95,
                allocatedBytes=60000, os='test', machineId='machine',
                runtimeHash='host', benchmarkProtocol='owner-turn-v1',
                liveLines=125, cpuFrames=602, gpuFrames=0)


class ComparisonTests(unittest.TestCase):
    def test_accepts_small_variance(self):
        self.assertEqual(compare([sample()] * 5, [sample(11)] * 5), [])

    def test_rejects_regression(self):
        self.assertTrue(compare([sample()] * 5, [sample(13)] * 5))

    def test_rejects_lost_work(self):
        self.assertTrue(compare([sample()] * 5, [sample(5, 300)] * 5))
        self.assertTrue(compare([sample()] * 5, [sample(5, pending=1)] * 5))

    def test_rejects_invalid_evidence(self):
        for value in [float('nan'), float('inf'), -1]:
            self.assertTrue(compare([sample()] * 5, [sample(value)] * 5))
        self.assertTrue(compare([sample()] * 5, [sample()] * 4))

    def test_rejects_mismatched_workload(self):
        changed = sample()
        changed['logicalLines'] = 10
        self.assertTrue(compare([sample()] * 5, [changed] * 5))

    def test_checks_all_runs_not_just_first(self):
        runs = [sample() for _ in range(5)]
        runs[-1]['pendingWork'] = 10
        self.assertTrue(compare([sample()] * 5, runs))

    def test_rejects_missing_rendering_work(self):
        for changes in [dict(liveLines=0), dict(cpuFrames=0), dict(cpuFrames=300)]:
            changed = sample(5)
            changed.update(changes)
            self.assertTrue(compare([sample()] * 5, [changed] * 5))


if __name__ == '__main__':
    unittest.main()

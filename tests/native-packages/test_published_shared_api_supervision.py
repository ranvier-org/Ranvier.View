import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "shared_api_supervision", ROOT / "tools/check-published-shared-api.py"
)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


def pid_is_running(pid: int) -> bool:
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        handle = kernel32.OpenProcess(0x1000, False, pid)  # SYNCHRONIZE
        if not handle:
            return ctypes.get_last_error() not in (6, 87, 1168)
        try:
            exit_code = wintypes.DWORD()
            if not kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                return True
            return exit_code.value == 259  # STILL_ACTIVE
        finally:
            kernel32.CloseHandle(handle)

    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def wait_until_processes_exit(pids, timeout=5.0) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if not any(pid_is_running(pid) for pid in pids):
            return True
        time.sleep(0.05)
    return not any(pid_is_running(pid) for pid in pids)


class PublishedSharedApiSupervisionTests(unittest.TestCase):
    def test_command_timeout_is_positive_and_has_a_conservative_default(self):
        self.assertIsNotNone(
            getattr(gate, "DEFAULT_COMMAND_TIMEOUT_SECONDS", None),
            "the checker needs a configurable per-command deadline",
        )
        self.assertGreater(gate.DEFAULT_COMMAND_TIMEOUT_SECONDS, 0)
        self.assertIsNotNone(getattr(gate, "make_argument_parser", None))
        parser = gate.make_argument_parser()
        args = parser.parse_args(
            ["--profile", "profile.json", "--output", "out", "--variant", "normal", "--target", "native"]
        )
        self.assertEqual(args.command_timeout_seconds, gate.DEFAULT_COMMAND_TIMEOUT_SECONDS)
        self.assertEqual(
            parser.parse_args(
                ["--profile", "profile.json", "--output", "out", "--variant", "normal", "--target", "native",
                 "--command-timeout-seconds", "17"]
            ).command_timeout_seconds,
            17,
        )
        with self.assertRaises(SystemExit):
            parser.parse_args(
                ["--profile", "profile.json", "--output", "out", "--variant", "normal", "--target", "native",
                 "--command-timeout-seconds", "0"]
            )
        self.assertIn("per-command deadline", parser.format_help())

    def test_success_preserves_stdout_and_nonzero_status_propagates(self):
        self.assertIsNotNone(
            getattr(gate, "run_logged_command", None),
            "the checker needs a logged, bounded child-process runner",
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            success_log = root / "success.log"
            success_step = {}
            success = gate.run_logged_command(
                [sys.executable, "-c", "print('finished')"], cwd=root, environment=os.environ.copy(),
                timeout_seconds=5, log_path=success_log, step=success_step
            )
            self.assertEqual(success, "finished\n")
            self.assertEqual(success_log.read_text(encoding="utf-8"), "finished\n")
            self.assertEqual(success_step["status"], "passed")
            self.assertEqual(success_step["exit_code"], 0)
            self.assertGreaterEqual(success_step["duration_seconds"], 0)

            failure_log = root / "failure.log"
            failure_step = {}
            with self.assertRaises(subprocess.CalledProcessError) as raised:
                gate.run_logged_command(
                    [sys.executable, "-c", "print('failure-output', flush=True); raise SystemExit(23)"],
                    cwd=root, environment=os.environ.copy(), timeout_seconds=5,
                    log_path=failure_log, step=failure_step
                )
            self.assertEqual(raised.exception.returncode, 23)
            self.assertIn("failure-output", raised.exception.output)
            self.assertEqual(failure_step["status"], "failed")
            self.assertEqual(failure_step["exit_code"], 23)
            self.assertEqual(failure_log.read_text(encoding="utf-8"), "failure-output\n")

    def _parent_with_child(self, root: Path):
        parent_pid_file = root / "parent.pid"
        child_pid_file = root / "child.pid"
        child_code = (
            "import os,sys,time; from pathlib import Path; "
            "Path(sys.argv[1]).write_text(str(os.getpid())); time.sleep(60)"
        )
        parent_code = (
            "import os,subprocess,sys,time; from pathlib import Path; "
            f"child=subprocess.Popen([sys.executable,'-c',{child_code!r},{str(child_pid_file)!r}],"
            "stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); "
            f"Path({str(parent_pid_file)!r}).write_text(str(os.getpid())); "
            "print('parent-ready-before-timeout',flush=True); time.sleep(60)"
        )
        return [sys.executable, "-c", parent_code], parent_pid_file, child_pid_file

    def _assert_tree_stopped(self, parent_pid_file: Path, child_pid_file: Path) -> None:
        self.assertTrue(parent_pid_file.exists(), "parent did not start before interruption")
        self.assertTrue(child_pid_file.exists(), "child did not start before interruption")
        pids = [int(parent_pid_file.read_text()), int(child_pid_file.read_text())]
        self.assertTrue(wait_until_processes_exit(pids), f"owned child tree remains alive: {pids}")

    def test_timeout_streams_partial_log_and_stops_only_owned_process_tree(self):
        self.assertIsNotNone(
            getattr(gate, "run_logged_command", None),
            "the checker needs a logged, bounded child-process runner",
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            command, parent_pid_file, child_pid_file = self._parent_with_child(root)
            log_path = root / "timeout.log"
            step = {}
            with self.assertRaises(subprocess.TimeoutExpired) as raised:
                gate.run_logged_command(
                    command, cwd=root, environment=os.environ.copy(), timeout_seconds=2,
                    log_path=log_path, step=step
                )

            self.assertEqual(raised.exception.cmd, command)
            self.assertEqual(raised.exception.timeout, 2)
            self.assertEqual(step["status"], "timed_out")
            self.assertEqual(step["command"], command)
            self.assertEqual(step["timeout_seconds"], 2)
            self.assertIn("2", step["timeout_reason"])
            self.assertGreater(step["duration_seconds"], 0)
            self.assertIn("parent-ready-before-timeout", log_path.read_text(encoding="utf-8"))
            self._assert_tree_stopped(parent_pid_file, child_pid_file)

    def test_interruption_stops_owned_process_tree_and_records_reason(self):
        self.assertIsNotNone(
            getattr(gate, "run_logged_command", None),
            "the checker needs an interrupt-safe child-process runner",
        )
        self.assertIsNotNone(getattr(gate, "wait_for_child", None))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            command, parent_pid_file, child_pid_file = self._parent_with_child(root)
            log_path = root / "interrupted.log"
            step = {}

            def interrupt_after_start(process, timeout_seconds):
                deadline = time.monotonic() + 3
                while time.monotonic() < deadline:
                    if parent_pid_file.exists() and child_pid_file.exists():
                        raise KeyboardInterrupt("test interruption")
                    time.sleep(0.02)
                self.fail("synthetic parent and child did not start")

            with patch.object(gate, "wait_for_child", side_effect=interrupt_after_start):
                with self.assertRaises(KeyboardInterrupt):
                    gate.run_logged_command(
                        command, cwd=root, environment=os.environ.copy(), timeout_seconds=10,
                        log_path=log_path, step=step
                    )

            self.assertEqual(step["status"], "interrupted")
            self.assertIn("KeyboardInterrupt", step["interruption_reason"])
            self.assertIn("parent-ready-before-timeout", log_path.read_text(encoding="utf-8"))
            self._assert_tree_stopped(parent_pid_file, child_pid_file)


if __name__ == "__main__":
    unittest.main()

import ctypes
from ctypes import wintypes
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "tools/Ranvier.Analyzers.Host/build_compiler.py"
SPEC = importlib.util.spec_from_file_location("build_compiler", SCRIPT)
producer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(producer)


def pid_is_running(pid: int) -> bool:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel32.GetExitCodeProcess.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    handle = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not handle:
        return ctypes.get_last_error() not in (6, 87, 1168)
    try:
        exit_code = wintypes.DWORD()
        if not kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
            return True
        return exit_code.value == 259  # STILL_ACTIVE
    finally:
        kernel32.CloseHandle(handle)


def wait_for_exit(pids, timeout=5.0):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if not any(pid_is_running(pid) for pid in pids):
            return True
        time.sleep(0.05)
    return not any(pid_is_running(pid) for pid in pids)


def kill_fixture_pids(pids):
    for pid in pids:
        subprocess.run(
            ["taskkill", "/PID", str(pid), "/T", "/F"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False, timeout=10,
        )


class ImmediateTimeoutProcess:
    """Let build() enter its real timeout path without waiting 540 seconds."""

    def __init__(self, process, command, child_pid_file):
        self.process = process
        self.command = command
        self.child_pid_file = child_pid_file
        self.first_wait = True

    @property
    def pid(self):
        return self.process.pid

    @property
    def returncode(self):
        return self.process.returncode

    def poll(self):
        return self.process.poll()

    def kill(self):
        return self.process.kill()

    def wait(self, timeout=None):
        if self.first_wait and timeout == 540:
            self.first_wait = False
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                if self.child_pid_file.is_file():
                    raise subprocess.TimeoutExpired(self.command, timeout)
                if self.process.poll() is not None:
                    break
                time.sleep(0.02)
            raise AssertionError("synthetic compiler parent and child did not start before timeout")
        return self.process.wait(timeout=timeout)


@unittest.skipUnless(os.name == "nt", "Exercises the producer's Windows taskkill /T path.")
class WindowsCompilerProcessTreeTests(unittest.TestCase):
    def fixture(self, root):
        work = root / "work"
        source = work / "source"
        sdk = root / "fake-sdk" / producer.SDK
        source.mkdir(parents=True)
        sdk.mkdir(parents=True)
        parent_pid_file = root / "parent.pid"
        child_pid_file = root / "child.pid"
        child_code = (
            "import os,sys,time; from pathlib import Path; "
            f"Path({str(child_pid_file)!r}).write_text(str(os.getpid())); time.sleep(60)"
        )
        parent_code = (
            "import os,subprocess,sys,time; from pathlib import Path; "
            f"Path({str(parent_pid_file)!r}).write_text(str(os.getpid())); "
            f"child=subprocess.Popen([sys.executable,'-c',{child_code!r}],"
            "stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); "
            f"Path({str(child_pid_file)!r}).write_text(str(child.pid)); "
            "print('synthetic-build-ready',flush=True); time.sleep(60)"
        )
        command = [sys.executable, "-c", parent_code]
        return work, source, sdk, parent_pid_file, child_pid_file, command

    def run_build(self, work, source, sdk, parent_pid_file, child_pid_file, command,
                  *, taskkill_failure=False):
        fake_dotnet = "synthetic-dotnet"
        real_popen = subprocess.Popen
        spawned = []

        def spawn(actual_command, *args, **kwargs):
            if str(actual_command[0]) == fake_dotnet:
                process = real_popen(command, *args, **kwargs)
                wrapped = ImmediateTimeoutProcess(process, actual_command, child_pid_file)
                spawned.append(wrapped)
                return wrapped
            return real_popen(actual_command, *args, **kwargs)

        spec = {
            "compilerSourceCommit": "test-commit",
            "sourceGuardSha256": "test-guard",
            "compilerSha256": "test-compiler",
        }
        with patch.object(producer, "sdk_path", return_value=sdk), \
             patch.object(producer.subprocess, "Popen", side_effect=spawn):
            if taskkill_failure:
                def fail_taskkill(args, **kwargs):
                    self.assertEqual(args[0], "taskkill")
                    self.assertEqual(kwargs["timeout"], 10)
                    self.assertTrue(kwargs["check"])
                    raise subprocess.CalledProcessError(1, args)

                with patch.object(producer.subprocess, "run", side_effect=fail_taskkill):
                    return producer.build(source, work, fake_dotnet, spec)
            return producer.build(source, work, fake_dotnet, spec)

    def assert_fixture_processes_stopped(self, parent_pid_file, child_pid_file):
        self.assertTrue(parent_pid_file.is_file(), "synthetic parent did not start")
        self.assertTrue(child_pid_file.is_file(), "synthetic child did not start")
        pids = [int(parent_pid_file.read_text()), int(child_pid_file.read_text())]
        self.assertTrue(wait_for_exit(pids), f"synthetic process tree remains alive: {pids}")

    def test_timeout_kills_the_real_synthetic_process_tree(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work, source, sdk, parent_pid_file, child_pid_file, command = self.fixture(root)
            try:
                with self.assertRaises(subprocess.TimeoutExpired):
                    self.run_build(work, source, sdk, parent_pid_file, child_pid_file, command)
                self.assert_fixture_processes_stopped(parent_pid_file, child_pid_file)
                receipt = json.loads((work / "compiler-build.json").read_text())
                self.assertNotIn("processTreeTerminationError", receipt)
                self.assertLess(receipt["seconds"], 15)
            finally:
                if parent_pid_file.exists() and child_pid_file.exists():
                    kill_fixture_pids([int(parent_pid_file.read_text()), int(child_pid_file.read_text())])

    def test_taskkill_failure_returns_bounded_and_records_unproven_descendant_cleanup(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work, source, sdk, parent_pid_file, child_pid_file, command = self.fixture(root)
            started = time.monotonic()
            try:
                with self.assertRaisesRegex(RuntimeError, "descendant cleanup is unproven"):
                    self.run_build(work, source, sdk, parent_pid_file, child_pid_file, command,
                                   taskkill_failure=True)
                self.assertLess(time.monotonic() - started, 15)
                receipt = json.loads((work / "compiler-build.json").read_text())
                self.assertIn("processTreeTerminationError", receipt)
                self.assertTrue(receipt["processTreeTerminationError"])
                self.assertTrue(parent_pid_file.is_file())
                self.assertTrue(child_pid_file.is_file())
                self.assertFalse(pid_is_running(int(parent_pid_file.read_text())))
                self.assertTrue(pid_is_running(int(child_pid_file.read_text())))
            finally:
                if parent_pid_file.exists() and child_pid_file.exists():
                    kill_fixture_pids([int(parent_pid_file.read_text()), int(child_pid_file.read_text())])


if __name__ == "__main__":
    unittest.main(verbosity=2)

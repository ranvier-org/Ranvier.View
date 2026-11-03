"""Run one analyzer group with a deadline and retained Linux resource samples."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def linux_sample(group):
    processes = []
    for entry in Path('/proc').glob('[0-9]*/stat'):
        try:
            raw = entry.read_text()
            fields = raw[raw.rindex(')') + 2:].split()
            if int(fields[2]) != group:
                continue
            processes.append(dict(pid=int(entry.parent.name), startTicks=int(fields[19]),
                                  cpuTicks=int(fields[11]) + int(fields[12]),
                                  rssBytes=int(fields[21]) * os.sysconf('SC_PAGE_SIZE'),
                                  command=(entry.parent / 'cmdline').read_bytes().replace(b'\0', b' ').decode(errors='replace')))
        except (OSError, ValueError, IndexError):
            continue
    system = {}
    for name in ('meminfo', 'pressure/memory', 'pressure/cpu'):
        path = Path('/proc') / name
        if path.is_file():
            system[name] = path.read_text()
    for name in ('memory.current', 'memory.max', 'memory.events', 'cpu.stat'):
        path = Path('/sys/fs/cgroup') / name
        if path.is_file():
            system[name] = path.read_text()
    return dict(processes=processes, system=system)


def terminate(child):
    if os.name == 'posix':
        try:
            os.killpg(child.pid, signal.SIGTERM)
        except ProcessLookupError:
            return
        try:
            child.wait(timeout=3)
        except subprocess.TimeoutExpired:
            pass
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    elif child.poll() is None:
        subprocess.run(['taskkill', '/PID', str(child.pid), '/T', '/F'],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    child.wait()


def run(command, output, timeout, interval):
    output.mkdir(parents=True, exist_ok=False)
    start = time.monotonic()
    receipt = dict(command=command, source=os.environ.get('GITHUB_SHA'), timeoutSeconds=timeout,
                   processorCount=os.environ.get('DOTNET_PROCESSOR_COUNT'),
                   buildServerReuse=False, sampleCount=0, timedOut=False, exitCode=1)
    child = None
    try:
        environment = dict(os.environ, DOTNET_CLI_USE_MSBUILD_SERVER='0',
                           MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
        child = subprocess.Popen(command, env=environment, start_new_session=os.name == 'posix')
        receipt['pid'] = child.pid
        with (output / 'resources.jsonl').open('w', encoding='utf-8') as stream:
            while True:
                elapsed = time.monotonic() - start
                sample = linux_sample(child.pid) if sys.platform.startswith('linux') else {}
                stream.write(json.dumps(dict(seconds=elapsed, **sample)) + '\n')
                stream.flush()
                receipt['sampleCount'] += 1
                code = child.poll()
                if code is not None:
                    receipt['exitCode'] = code
                    break
                if elapsed >= timeout:
                    receipt.update(timedOut=True, exitCode=124)
                    break
                time.sleep(min(interval, max(0.001, timeout - elapsed)))
    finally:
        if child is not None:
            terminate(child)
        receipt['elapsedSeconds'] = time.monotonic() - start
        samples = output / 'resources.jsonl'
        receipt['samplesSha256'] = hashlib.sha256(samples.read_bytes()).hexdigest() if samples.exists() else None
        (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(receipt), file=sys.stderr, flush=True)
    return receipt['exitCode']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--timeout-seconds', type=float, default=720)
    parser.add_argument('--interval-seconds', type=float, default=5)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command or args.timeout_seconds <= 0 or args.interval_seconds <= 0:
        parser.error('A command and positive timeout/sample interval are required')
    return run(command, args.output, args.timeout_seconds, args.interval_seconds)


if __name__ == '__main__':
    raise SystemExit(main())

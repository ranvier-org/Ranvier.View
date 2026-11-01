"""Run strict Linux SDL GPU frames, cohort rollback and close/reopen acceptance."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--app", required=True, type=Path)
parser.add_argument("--runtime", required=True, type=Path)
parser.add_argument("--dotnet", default="dotnet")
args = parser.parse_args()
environment = dict(os.environ)
environment.update(
    LIBGL_ALWAYS_SOFTWARE="1",
    RANVIER_SMOKE="1",
    RANVIER_SMOKE_GPU="1",
    RANVIER_SMOKE_REOPEN="1",
    LD_LIBRARY_PATH=os.pathsep.join(
        [str(args.runtime.resolve()), str(args.app.resolve().parent), environment.get("LD_LIBRARY_PATH", "")]
    ),
)
receipts = []
for phase in ["before-registration", "after-registration"]:
    run_environment = dict(environment, RANVIER_SMOKE_EGL_FAILURE=phase)
    result = subprocess.run(
        [args.dotnet, str(args.app.resolve())], env=run_environment,
        capture_output=True, text=True, timeout=120,
    )
    print(result.stdout, end="", flush=True)
    if result.returncode:
        raise AssertionError(f"{phase}: exit {result.returncode}\n{result.stderr}")
    assert "EGL cohort failure smoke passed:" in result.stdout, result.stdout
    counts = [int(count) for count in re.findall(r"GPU frames (\d+)", result.stdout)]
    if phase == "before-registration":
        assert "owner retained false" in result.stdout and not counts, result.stdout
    else:
        assert "owner retained true" in result.stdout, result.stdout
        assert len(counts) == 3 and all(count > 0 for count in counts), counts
    receipts.append({"phase": phase, "gpu_frames": counts, "result": "PASS"})
print(json.dumps({"result": "PASS", "checks": receipts}))

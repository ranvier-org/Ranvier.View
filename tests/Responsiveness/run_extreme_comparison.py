"""Compare full and bounded shaping with the same font, cells and native runtime."""
import argparse
import json
from pathlib import Path
import statistics
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("fixture")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root = Path(args.output)
    root.mkdir(parents=True, exist_ok=True)
    results = {}
    for scenario in ("extreme-line", "extreme-edit"):
        cohorts = {"baseline": [], "optimized": []}
        for repetition in range(5):
            for mode in ("baseline", "optimized"):
                output = root / f"{mode}-{repetition}"
                command = ["dotnet", args.fixture, "--scenario", scenario, "--lines", "1",
                           "--iterations", "100", "--repetitions", "1", "--no-trace",
                           "--renderer", "software", "--output", str(output)]
                if mode == "optimized":
                    command.append("--horizontal-culling")
                subprocess.run(command, check=True, timeout=120, stdout=subprocess.DEVNULL)
                cohorts[mode].append(json.loads((output / f"{scenario}-1.json").read_text()))
        summary = {mode: {
            "p95Ms": statistics.median(row["turnP95Ms"] for row in rows),
            "bytesPerOperation": statistics.median(row["allocatedBytes"] / row["iterations"] for row in rows)
        } for mode, rows in cohorts.items()}
        results[scenario] = summary
        (root / "summary.json").write_text(json.dumps(results, indent=2))
        before, after = summary["baseline"], summary["optimized"]
        if after["p95Ms"] > before["p95Ms"] * 1.25 + 0.5:
            raise RuntimeError(f"{scenario}: latency regression: {summary}")
        if after["bytesPerOperation"] > before["bytesPerOperation"] + 8192:
            raise RuntimeError(f"{scenario}: allocation regression: {summary}")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()

"""medians per strategy and edit class from bench/results/*.json (stdlib only).
   python bench/summarize.py  ->  prints Markdown tables."""
import glob, json, os, re, statistics as st, sys

here = os.path.dirname(os.path.abspath(__file__))
files = [f for f in glob.glob(os.path.join(here, "results", "*.json")) if "run1" not in f]

CLASSES = [
    ("S1", "static text"), ("S2", "static attribute"), ("S3", "reader logic"), ("S4", "handler logic"),
    ("S5", "structure (new element)"), ("S6a", "compile error (gated)"), ("S6", "state type change"),
    ("S7", "comment only"), ("S8", "list row reader"),
]

def cls(step):
    m = re.match(r"(S6a|S\d)", step or "")
    return m.group(1) if m else None

def strategy_of(path):
    name = os.path.basename(path)
    m = re.match(r"(scenarios|scale-\d+rows|edge|shared)-(fine-stamped|fine-nochecker|fine|fsi|alc)-", name)
    return (m.group(1), m.group(2)) if m else (None, None)

data = {}
first = {}
for f in files:
    scen, strat = strategy_of(f)
    if not scen:
        continue
    rows = json.load(open(f))
    edits = [r for r in rows if r.get("totalMs") is not None]
    if edits:
        first.setdefault((scen, strat), []).append(edits[0]["totalMs"])
    for i, r in enumerate(rows):
        if r.get("totalMs") is None:
            continue
        if r is edits[0]:
            continue  # the first reload of a session pays JIT; reported apart
        c = cls(r["step"])
        if c == "S6" and r["step"].startswith("S6b"):
            c = "S6"
        data.setdefault((scen, strat, c), []).append((r["totalMs"], r["applyMs"], r.get("deliverMs") or 0, r.get("checkMs") or 0))

def med(xs):
    return f"{st.median(xs):.0f}" if xs else "-"

strategies = ["fine", "fine-stamped", "fine-nochecker", "fsi", "alc"]
print("## Main scenario: median save-to-presented ms (apply ms), first reload excluded\n")
print("| edit | " + " | ".join(strategies) + " |")
print("|---|" + "---|" * len(strategies))
for c, label in CLASSES:
    cells = []
    for s in strategies:
        xs = data.get(("scenarios", s, c), [])
        cells.append(f"{med([x[0] for x in xs])} ({med([x[1] for x in xs])})" if xs else "-")
    print(f"| {label} | " + " | ".join(cells) + " |")
print()
print("First reload of a session (cold JIT), median ms: " + ", ".join(
    f"{s} {med(first.get(('scenarios', s), []))}" for s in strategies))
print()
for scen in sorted({k[0] for k in data if k[0] not in ("scenarios",)}):
    print(f"## {scen}: median total ms (apply ms)\n")
    print("| edit | " + " | ".join(strategies) + " |")
    print("|---|" + "---|" * len(strategies))
    for c, label in CLASSES:
        if not any(data.get((scen, s, c)) for s in strategies):
            continue
        cells = []
        for s in strategies:
            xs = data.get((scen, s, c), [])
            cells.append(f"{med([x[0] for x in xs])} ({med([x[1] for x in xs])})" if xs else "-")
        print(f"| {label} | " + " | ".join(cells) + " |")
    print()

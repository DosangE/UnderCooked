"""Read-only audit of archived results; write independent evidence as JSON.

Usage: python tools/verify_results.py [output.json]
Requires tensorboard. Does not run Unity or alter source results.
"""
from pathlib import Path
import collections
import csv
import hashlib
import json
import math
import statistics as st
import struct
import sys

from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

ROOT = Path(__file__).resolve().parents[1]
RUNS = ROOT / "archive/runs"


def wilson(k, n):
    z = 1.959963984540054
    p = k / n
    den = 1 + z * z / n
    center = (p + z * z / (2 * n)) / den
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / den
    return [100 * (center - half), 100 * (center + half)]


def episodes(path):
    rows = [r.split(",") for r in path.read_text(encoding="utf-8").strip().split(";") if r.strip()]
    assert all(len(r) in (10, 11) for r in rows), path
    assert all(int(r[0]) <= int(r[1]) and 0 <= float(r[2]) <= 90 for r in rows), path
    success = [r for r in rows if int(r[0]) >= int(r[1])]
    wrong = [r for r in rows if int(r[6]) > 0]
    failed = [r for r in rows if int(r[0]) < int(r[1])]
    return dict(n=len(rows), goals=collections.Counter(int(r[1]) for r in rows),
                served=collections.Counter(int(r[0]) for r in rows),
                successes=len(success), success_pct=100 * len(success) / len(rows),
                wilson_95_pct=wilson(len(success), len(rows)),
                wrong_episodes=len(wrong), wrong_episode_pct=100 * len(wrong) / len(rows),
                wrong_success_pct=100 * sum(int(r[0]) >= int(r[1]) for r in wrong) / len(wrong) if wrong else None,
                success_mean_seconds=st.mean(float(r[2]) for r in success),
                success_median_seconds=st.median(float(r[2]) for r in success),
                failures=failed,
                sum_pots=sum(int(r[3]) for r in rows), sum_wrong_pots=sum(int(r[6]) for r in rows),
                sum_served_wrong=sum(int(r[7]) for r in rows),
                sum_expired_estimate=sum(int(r[8]) for r in rows))


def fills(path):
    rows = list(csv.DictReader(path.open(encoding="utf-8", newline="")))
    all_commits = [r for r in rows if r["event"] == "commit"]
    commits = [r for r in all_commits if r["first"] not in ("?", "") and r["second"] not in ("?", "")]
    present, right, wrong, intersections = (collections.Counter() for _ in range(4))
    first_src, second_src = collections.Counter(), collections.Counter()
    board_green = 0
    impossible_numerator = []
    for r in commits:
        board = {int(x.split(":")[0]) for x in r["ordersAtFirst"].split("|") if x}
        present.update(board)
        recipe = int(r["cooked"])
        if r["wrong"] == "0":
            right[recipe] += 1
            if recipe in board: intersections[recipe] += 1
            else: impossible_numerator.append(r)
        else: wrong[recipe] += 1
        first_src[r["firstSrc"]] += 1
        second_src[r["secondSrc"]] += 1
        if board.intersection({0, 1, 4}): board_green += 1
    return dict(all_commits=len(all_commits), known_commits=len(commits),
                wrong_count=sum(wrong.values()), wrong_pct=100 * sum(wrong.values()) / len(commits),
                present=present, made_right=right, made_wrong=wrong,
                reported_pick_pct={k: 100 * right[k] / present[k] for k in range(6)},
                strict_pick_pct={k: 100 * intersections[k] / present[k] for k in range(6)},
                numerator_not_in_first_board=len(impossible_numerator),
                green_recipe_present_pct=100 * board_green / len(commits),
                first_source=first_src, second_source=second_src)


# Minimal protobuf reader for ONNX graph metadata. No model inference or rewriting.
def varint(data, pos):
    val, shift = 0, 0
    while True:
        b = data[pos]; pos += 1
        val |= (b & 127) << shift
        if not b & 128: return val, pos
        shift += 7
        if shift >= 70: raise ValueError("invalid varint")


def fields(data):
    out = collections.defaultdict(list)
    pos = 0
    while pos < len(data):
        key, pos = varint(data, pos)
        field, wire = key >> 3, key & 7
        if wire == 0: value, pos = varint(data, pos)
        elif wire == 2:
            size, pos = varint(data, pos)
            value = data[pos:pos + size]; pos += size
        elif wire in (1, 5):
            size = 8 if wire == 1 else 4
            value = data[pos:pos + size]; pos += size
        else: raise ValueError(f"unsupported wire type {wire}")
        out[field].append(value)
    assert pos == len(data)
    return out


def model(path):
    blob = path.read_bytes()
    graph = fields(fields(blob)[7][0])
    def valueinfo(raw):
        v = fields(raw)
        tensor = fields(fields(v[2][0])[1][0])
        dims = []
        for dim in fields(tensor[2][0])[1]:
            d = fields(dim)
            dims.append(d[1][0] if 1 in d else d[2][0].decode() if 2 in d else None)
        return dict(name=v[1][0].decode(), shape=dims)
    return dict(sha256=hashlib.sha256(blob).hexdigest(), bytes=len(blob),
                inputs=[valueinfo(v) for v in graph[11]], outputs=[valueinfo(v) for v in graph[12]],
                ops=collections.Counter(fields(n)[4][0].decode() for n in graph[1]))


def main():
    evidence = dict(episodes={}, fills={}, scalars={}, models={}, determinism={})
    for path in RUNS.glob("*/episodes.txt"):
        evidence["episodes"][path.parent.name] = episodes(path)
    for path in RUNS.glob("*/fills.csv"):
        evidence["fills"][path.parent.name] = fills(path)
    raw_scalars = {}
    for folder in sorted(RUNS.iterdir()):
        files = list(folder.glob("**/events.out*"))
        if not files: continue
        ea = EventAccumulator(str(folder), size_guidance={"scalars": 0})
        ea.Reload()
        scalars = {t: ea.Scalars(t) for t in ea.Tags()["scalars"]}
        if not scalars: continue
        raw_scalars[folder.name] = scalars
        end = max(x.step for values in scalars.values() for x in values)
        summary = dict(last_step=end, event_files=len(files), tags=len(scalars))
        times = [x.wall_time for values in scalars.values() for x in values]
        summary["wall_minutes_first_to_last_scalar"] = (max(times) - min(times)) / 60
        summary["tail_500k"] = {}
        summary["tail_100k"] = {}
        summary["first_600k"] = {}
        summary["whole_run"] = {}
        for tag, values in scalars.items():
            tail = [x.value for x in values if end - 500000 < x.step <= end]
            summary["tail_500k"][tag] = dict(mean=st.mean(tail), n=len(tail)) if tail else None
            last100 = [x.value for x in values if end - 100000 < x.step <= end]
            first600 = [x.value for x in values if 0 < x.step <= 600000]
            summary["tail_100k"][tag] = dict(mean=st.mean(last100), n=len(last100)) if last100 else None
            summary["first_600k"][tag] = dict(mean=st.mean(first600), n=len(first600)) if first600 else None
            summary["whole_run"][tag] = dict(mean=st.mean(x.value for x in values), n=len(values))
        evidence["scalars"][folder.name] = summary
    for seed in (1, 3):
        a = raw_scalars[f"undercooked_stage_s{seed}"]
        b = raw_scalars[f"undercooked_stage_pen01_s{seed}"]
        summary = {}
        for tag in ("Environment/Cumulative Reward", "Kitchen/PotCommitted", "Kitchen/PotCommittedWrong", "Policy/Entropy"):
            aa = {x.step: struct.pack("f", x.value) for x in a[tag]}
            bb = {x.step: struct.pack("f", x.value) for x in b[tag]}
            common = aa.keys() & bb.keys()
            summary[tag] = dict(common=len(common), differences=sum(aa[k] != bb[k] for k in common))
        evidence["determinism"][str(seed)] = summary
    for p in [ROOT / "models/undercooked.onnx", RUNS / "undercooked_blue_urgent3/Chef.onnx", RUNS / "undercooked_final3/Chef.onnx"]:
        evidence["models"][str(p.relative_to(ROOT)).replace("\\", "/")] = model(p)
    inputs = [p for p in RUNS.rglob("*") if p.is_file() and (p.name.startswith("events.out") or p.name in ("episodes.txt", "fills.csv"))]
    evidence["source_sha256"] = {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    output = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "reports/2026-10-07-audit-evidence.json"
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Evidence: {output}; {len(evidence['scalars'])} event directories, {len(evidence['episodes'])} episode logs, {len(evidence['fills'])} fill logs")
    for name, values in evidence["episodes"].items():
        print(name, {k:values[k] for k in ("n", "served", "success_pct", "wrong_episode_pct", "wrong_success_pct", "success_mean_seconds", "success_median_seconds")})
    for name, values in evidence["fills"].items():
        print(name, {k:values[k] for k in ("known_commits", "wrong_pct", "reported_pick_pct", "strict_pick_pct", "numerator_not_in_first_board", "green_recipe_present_pct")})
    print("MODEL", evidence["models"])
    print("DETERMINISM", evidence["determinism"])


if __name__ == "__main__":
    main()

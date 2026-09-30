"""Summarise the inference tally dumped by inference/dump.cs.

usage: python results/.tools/analyze_tally.py <dump_output.json>
Each row is "served,potCommitted,dishTaken,dishToB,episodeSeconds".
"""
import json
import sys

import numpy as np

d = json.load(open(sys.argv[1], encoding="utf-8"))
a = np.array([[float(x) for x in r.split(",")] for r in d["data"]["result"].split(";") if r])
served, pot, taken, toB, length = a.T
ok = served >= 3
print(f"episodes {len(a)}  goal {ok.mean():.3f}  served {served.mean():.2f}  pot {pot.mean():.2f}  "
      f"taken {taken.mean():.2f}  toB {toB.mean():.2f}")
print("served dist", {int(k): int(v) for k, v in zip(*np.unique(served, return_counts=True))})
if ok.any():
    print(f"success sec {length[ok].mean():.1f}  pct25/50/75 {np.percentile(length[ok], [25, 50, 75]).round(1)}")
if (~ok).any():
    f = ~ok
    print(f"fail n={f.sum()} served {served[f].mean():.2f} pot {pot[f].mean():.2f} "
          f"toB {toB[f].mean():.2f} wasted {(toB - served)[f].mean():.2f}")

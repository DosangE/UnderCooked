"""Summarise episodes.txt written by inference/episode_log.cs.

usage: python analyze_episodes.py <episodes.txt> [<episodes.txt> ...]
Row: served,target,duration,potCommitted,dishesTaken,dishesToB,potWrong,servedWrong,ordersExpired,[wrongDishSeconds,]serveTimes
Files before 2026-09-30 have 10 fields (no wrongDishSeconds).
"""
import math
import sys

import numpy as np


def load(path):
    rows = []
    for r in open(path, encoding="utf-8").read().split(";"):
        if not r:
            continue
        f = r.split(",")
        has_wsec = len(f) >= 11
        rows.append(dict(served=int(f[0]), dur=float(f[2]), pot=int(f[3]), potW=int(f[6]), servW=int(f[7]),
                         exp=int(f[8]), wsec=float(f[9]) if has_wsec else float("nan"),
                         st=[float(x) for x in f[-1].split("/") if x]))
    return rows


def rate(rows):
    return np.mean([e["served"] >= 3 for e in rows]) if rows else float("nan")


def ci(p, n):
    return 1.96 * math.sqrt(p * (1 - p) / n) if n else float("nan")


for path in sys.argv[1:]:
    E = load(path)
    n = len(E)
    W = [e for e in E if e["potW"] >= 1]
    C = [e for e in E if e["potW"] == 0]
    p = rate(E)
    print(f"== {path}: episodes {n}  goal {p:.3f} +-{ci(p, n):.3f}")
    print(f"   no wrong fill: {len(C)} eps, goal {rate(C):.3f}")
    print(f"   wrong fill >=1: {len(W) / n:.3f} of eps, goal {rate(W):.3f} +-{ci(rate(W), len(W)):.3f}")
    print(f"   in those: discards {np.mean([e['servW'] for e in W]):.2f}  correct pots {np.mean([e['pot'] - e['potW'] for e in W]):.2f}"
          f"  wrong-dish sec {np.nanmean([e['wsec'] for e in W]):.1f}")
    for k in (1, 2, 3):
        L = [e for e in E if e["potW"] == k]
        if L:
            print(f"   wrong fill = {k}: {len(L)} eps, goal {rate(L):.3f}")
    ok = [e["dur"] for e in E if e["served"] >= 3]
    if ok:
        print(f"   success duration median {np.median(ok):.1f}s")

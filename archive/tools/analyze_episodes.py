"""Summarise episodes.txt written by inference/episode_log.cs. usage: python analyze_episodes.py <episodes.txt>"""
import sys, numpy as np, collections
rows=[r.split(",") for r in open(sys.argv[1]).read().split(";") if r]
E=[]
for r in rows:
    d=dict(served=int(r[0]),dur=float(r[2]),pot=int(r[3]),taken=int(r[4]),toB=int(r[5]),potW=int(r[6]),servW=int(r[7]),exp=int(r[8]),st=[float(x) for x in r[9].split("/") if x])
    E.append(d)
n=len(E); ok=[e for e in E if e["served"]>=3]; f=[e for e in E if e["served"]<3]
print(f"episodes {n}  success {len(ok)/n:.3f}  fail {len(f)}")
def m(L,k): return np.mean([e[k] for e in L]) if L else float('nan')
for name,L in [("success",ok),("fail",f)]:
    print(f" {name:7s} n={len(L)} served {m(L,'served'):.2f} pot {m(L,'pot'):.2f} potWrong {m(L,'potW'):.2f} servedWrong {m(L,'servW'):.2f} expired {m(L,'exp'):.2f} dur {m(L,'dur'):.1f}")
# success episodes: clean (no wrong) vs had wrong
clean=[e for e in ok if e["potW"]==0]
print(f" success clean(potWrong=0) {len(clean)/len(ok):.3f}, dur clean {m(clean,'dur'):.1f}, dur with-wrong {m([e for e in ok if e['potW']>0],'dur'):.1f}")
# fail categories
print(" fail served dist", collections.Counter(e["served"] for e in f))
print(" fail potWrong dist", collections.Counter(e["potW"] for e in f))
print(" all potWrong dist", collections.Counter(e["potW"] for e in E))
for k in (0,1,2,3):
    L=[e for e in E if e["potW"]==k]
    if L: print(f"  potWrong={k}: n={len(L)} success {np.mean([e['served']>=3 for e in L]):.3f}")
# pot committed count in fail: enough pots?
print(" fail pot dist", collections.Counter(e["pot"] for e in f))
# first serve time distribution
fs=[e["st"][0] for e in E if e["st"]]
print(" first serve time pct", np.percentile(fs,[10,50,90]).round(1), " success 2nd serve", np.percentile([e['st'][1] for e in ok if len(e['st'])>1],[10,50,90]).round(1))
# failures: first serve late (>20s)?
print(" fail with first serve >20s or none:", sum(1 for e in f if not e['st'] or e['st'][0]>20), "/", len(f))


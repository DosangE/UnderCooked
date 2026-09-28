import glob, sys
import numpy as np
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
run = sys.argv[1]; lo, hi = float(sys.argv[2]), float(sys.argv[3])
f = sorted(glob.glob(f"results/{run}/Chef/events.out*"))[-1]
ea = EventAccumulator(f, size_guidance={"scalars": 0}); ea.Reload()
tags = ["Environment/Cumulative Reward","Environment/Group Cumulative Reward","Kitchen/DishesServed","Kitchen/GoalReached",
        "Kitchen/OrdersExpired","Kitchen/PotCommitted","Kitchen/PlatesToChefA","Kitchen/DishesTaken","Kitchen/DishesToChefB","Kitchen/PotCommittedWrong","Kitchen/ServedWrongOrder","Kitchen/PotNotReady",
        "Environment/Episode Length","Policy/Entropy"]
for t in tags:
    if t not in ea.Tags()["scalars"]: continue
    v = [e.value for e in ea.Scalars(t) if lo < e.step <= hi]
    print(f"{t:40s} {np.mean(v):8.3f}  (n={len(v)})")

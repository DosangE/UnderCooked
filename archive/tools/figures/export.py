import glob, csv, sys
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
BASE = sys.argv[2] if len(sys.argv) > 2 else "results"
runs = ["undercooked_v1","undercooked_lesson0","undercooked_v2","undercooked_final","undercooked_final2","undercooked_final3"]
tags = ["Environment/Cumulative Reward","Environment/Group Cumulative Reward","Kitchen/GoalReached","Kitchen/PotCommittedWrong","Environment/Lesson Number"]
out = csv.writer(open(sys.argv[1],"w",newline="",encoding="utf-8"))
out.writerow(["run","tag","step","value"])
for r in runs:
    for f in sorted(glob.glob(f"{BASE}/{r}/**/events.out*", recursive=True)):
        ea = EventAccumulator(f, size_guidance={"scalars":0}); ea.Reload()
        have = ea.Tags()["scalars"]
        for t in tags:
            if t in have:
                for e in ea.Scalars(t): out.writerow([r,t,e.step,e.value])
        print(r, f.split("\\")[-1], [t for t in tags if t in have])

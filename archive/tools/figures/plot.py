import csv, collections, sys
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager

font_manager.fontManager.addfont("C:/Windows/Fonts/malgun.ttf")
plt.rcParams["font.family"] = "Malgun Gothic"
plt.rcParams["axes.unicode_minus"] = False

SURF, INK, INK2, MUTED, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#8a8984", "#e6e5e0"
S1, S2 = "#2a78d6", "#eb6834"

data = collections.defaultdict(list)
for r in csv.DictReader(open(sys.argv[1], encoding="utf-8")):
    data[(r["run"], r["tag"])].append((int(r["step"]), float(r["value"])))

CHAIN = [("undercooked_lesson0", "lesson0", "쉬운 난이도 고정"),
         ("undercooked_v2", "v2", "커리큘럼"),
         ("undercooked_final", "final", "최종 난이도"),
         ("undercooked_final2", "final2", "최종 난이도"),
         ("undercooked_final3", "final3", "최종, 벌점 -0.3")]
LEN = {"undercooked_lesson0": 3e6, "undercooked_v2": 8e6, "undercooked_final": 3e6,
       "undercooked_final2": 3e6, "undercooked_final3": 3e6}

def ema(v, a=0.8):
    out, s = [], None
    for x in v:
        s = x if s is None else a * s + (1 - a) * x
        out.append(s)
    return out

def style(ax):
    ax.set_facecolor(SURF)
    for sp in ["top", "right"]: ax.spines[sp].set_visible(False)
    for sp in ["left", "bottom"]: ax.spines[sp].set_color(GRID)
    ax.tick_params(colors=INK2, labelsize=9, length=0)
    ax.grid(axis="y", color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)

def chained(tag, title, ylabel, out, note, ylim=None):
    fig, ax = plt.subplots(figsize=(10, 4.2), dpi=150)
    fig.patch.set_facecolor(SURF); style(ax)
    off = 0
    for i, (run, name, desc) in enumerate(CHAIN):
        pts = sorted(data[(run, tag)])
        xs = [(off + s) / 1e6 for s, _ in pts]; ys = [v for _, v in pts]
        ax.plot(xs, ys, color=S1, alpha=0.22, linewidth=1)
        ax.plot(xs, ema(ys), color=S1, linewidth=2)
        if i: ax.axvline(off / 1e6, color=MUTED, linewidth=1, linestyle=(0, (3, 3)))
        mid = (off + LEN[run] / 2) / 1e6
        ax.text(mid, 1.08, name, transform=ax.get_xaxis_transform(), ha="center", color=INK, fontsize=10, fontweight="bold")
        ax.text(mid, 1.02, desc, transform=ax.get_xaxis_transform(), ha="center", color=INK2, fontsize=8)
        off += LEN[run]
    ax.set_xlim(0, off / 1e6)
    if ylim: ax.set_ylim(*ylim)
    ax.set_xlabel("누적 학습 스텝 (백만, 각 런은 이전 런에서 --initialize-from)", color=INK2, fontsize=9)
    ax.set_ylabel(ylabel, color=INK2, fontsize=9)
    fig.suptitle(title, x=0.01, y=0.99, ha="left", color=INK, fontsize=13, fontweight="bold")
    fig.text(0.01, 0.01, note, color=MUTED, fontsize=8, ha="left")
    fig.tight_layout(rect=(0, 0.04, 1, 0.93))
    fig.savefig(out, facecolor=SURF); plt.close(fig)

chained("Environment/Cumulative Reward", "Environment/Cumulative Reward", "에피소드당 개인 보상",
        "assets/tb_cumulative_reward.png",
        "개인 보상(AddReward)만 담는다. 매 스텝 -0.002가 깔려 있어 최대치가 약 +0.46이다. 팀 보상은 Group Cumulative Reward, 성과는 GoalReached로 함께 본다. 흐린 선: 원값, 진한 선: EMA 0.8.")
chained("Kitchen/GoalReached", "Kitchen/GoalReached (목표 달성률)", "목표 달성 에피소드 비율",
        "assets/tb_goal_reached.png",
        "lesson0은 쉬운 난이도, v2는 커리큘럼으로 난이도가 올라가는 중이라 값이 난이도마다 다르다. final 이후는 최종 난이도 고정. 흐린 선: 원값, 진한 선: EMA 0.8.",
        ylim=(0, 1.02))
chained("Environment/Group Cumulative Reward", "Environment/Group Cumulative Reward (팀 보상)", "에피소드당 팀 보상",
        "assets/tb_group_reward.png",
        "서빙 +3, 목표 달성 +2 같은 팀 보상. 주문 슬롯이 늘면 기준선이 -0.5씩 내려간다. 흐린 선: 원값, 진한 선: EMA 0.8.")

# PotCommittedWrong: final2 vs final3 on the same step axis
fig, ax = plt.subplots(figsize=(10, 4.2), dpi=150)
fig.patch.set_facecolor(SURF); style(ax)
for run, name, c in [("undercooked_final2", "final2 (벌점 -0.1)", S2), ("undercooked_final3", "final3 (벌점 -0.3)", S1)]:
    pts = sorted(data[(run, "Kitchen/PotCommittedWrong")])
    xs = [s / 1e6 for s, _ in pts]; ys = [v for _, v in pts]
    ax.plot(xs, ys, color=c, alpha=0.22, linewidth=1)
    sm = ema(ys)
    ax.plot(xs, sm, color=c, linewidth=2, label=name)
    ax.text(xs[-1] + 0.03, sm[-1], name, color=INK, fontsize=9, va="center")
ax.set_xlim(0, 3.55); ax.set_ylim(bottom=0)
ax.legend(frameon=False, loc="upper right", fontsize=9, labelcolor=INK)
ax.set_xlabel("런 안의 학습 스텝 (백만)", color=INK2, fontsize=9)
ax.set_ylabel("에피소드당 횟수", color=INK2, fontsize=9)
fig.suptitle("Kitchen/PotCommittedWrong (주문에 없는 레시피로 냄비를 채운 횟수)", x=0.01, y=0.99, ha="left", color=INK, fontsize=13, fontweight="bold")
fig.text(0.01, 0.01, "둘 다 최종 난이도, 이전 런에서 이어 학습. 흐린 선: 원값, 진한 선: EMA 0.8.", color=MUTED, fontsize=8, ha="left")
fig.tight_layout(rect=(0, 0.04, 1, 0.95))
fig.savefig("assets/tb_pot_committed_wrong.png", facecolor=SURF); plt.close(fig)
print("ok")

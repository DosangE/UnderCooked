"""Figures for the 6-dish (3-ingredient) model. Reads archive/runs directly.

usage (repo root): python archive/tools/figures/plot_blue.py
writes assets/tb_blue_goal_reached.png and assets/blue_pick_rate.png.
The 3-dish figures (plot.py) are left alone because README sections 3-7 cite them.
"""
import csv, collections, glob
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

font_manager.fontManager.addfont("C:/Windows/Fonts/malgun.ttf")
plt.rcParams["font.family"] = "Malgun Gothic"
plt.rcParams["axes.unicode_minus"] = False

SURF, INK, INK2, MUTED, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#8a8984", "#e6e5e0"
S1 = "#2a78d6"
BAND = "#f1f0ec"
RUNS = "archive/runs"

# Each run starts from the previous one with --initialize-from. Conditions differ, so the
# phases are shaded: the success rate only compares within one phase.
CHAIN = [("undercooked_blue_s1", "s1"), ("undercooked_blue_final", "final"),
         ("undercooked_blue_fix", "fix"), ("undercooked_blue_fix9", "fix9"),
         ("undercooked_blue_red", "red"), ("undercooked_blue_red_mix", "red_mix"),
         ("undercooked_blue_long", "long"), ("undercooked_blue_long8", "long8"),
         ("undercooked_blue_long8_g995", "g995"), ("undercooked_blue_urgent", "urgent"),
         ("undercooked_blue_urgent3", "urgent3")]
PHASES = [("단계 커리큘럼", ["undercooked_blue_s1"]),
          ("6종 고정 45초", ["undercooked_blue_final"]),
          ("단계 7~9", ["undercooked_blue_fix"]),
          ("6종 45초 / 목표 3", ["undercooked_blue_fix9", "undercooked_blue_red", "undercooked_blue_red_mix"]),
          ("90초 / 목표 5~8", ["undercooked_blue_long", "undercooked_blue_long8", "undercooked_blue_long8_g995",
                             "undercooked_blue_urgent", "undercooked_blue_urgent3"])]


def scalars(run, tag):
    pts = []
    for f in sorted(glob.glob(f"{RUNS}/{run}/**/events.out*", recursive=True)):
        ea = EventAccumulator(f, size_guidance={"scalars": 0}); ea.Reload()
        if tag in ea.Tags()["scalars"]:
            pts += [(e.step, e.value) for e in ea.Scalars(tag)]
    return sorted(pts)


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


def goal_reached():
    fig, ax = plt.subplots(figsize=(11, 4.6), dpi=150)
    fig.patch.set_facecolor(SURF); style(ax)
    off, start, end = 0, {}, {}
    for run, name in CHAIN:
        pts = scalars(run, "Kitchen/GoalReached")
        start[run] = off
        xs = [(off + s) / 1e6 for s, _ in pts]; ys = [v for _, v in pts]
        ax.plot(xs, ys, color=S1, alpha=0.22, linewidth=1)
        ax.plot(xs, ema(ys), color=S1, linewidth=2)
        off += pts[-1][0]
        end[run] = off
        if run != CHAIN[0][0]: ax.axvline(start[run] / 1e6, color=GRID, linewidth=0.8)
        # run names: short runs are crowded, so they sit at the bottom, rotated
        ax.text((start[run] + end[run]) / 2e6, 0.02, name, rotation=90, ha="center", va="bottom",
                color=INK2, fontsize=7.5, bbox=dict(boxstyle="round,pad=0.15", fc=SURF, ec="none", alpha=0.9), zorder=5)
    for i, (label, runs) in enumerate(PHASES):
        x0, x1 = start[runs[0]] / 1e6, end[runs[-1]] / 1e6
        if i % 2: ax.axvspan(x0, x1, color=BAND, zorder=0, linewidth=0)
        ax.text((x0 + x1) / 2, 1.03, label, transform=ax.get_xaxis_transform(), ha="center",
                color=INK, fontsize=9, fontweight="bold")
    ax.set_xlim(0, off / 1e6); ax.set_ylim(0, 1.02)
    ax.set_xlabel("누적 학습 스텝 (백만, 각 런은 이전 런에서 --initialize-from)", color=INK2, fontsize=9)
    ax.set_ylabel("목표 달성 에피소드 비율", color=INK2, fontsize=9)
    fig.suptitle("Kitchen/GoalReached, 재료 3종 · 요리 6종", x=0.01, y=0.99, ha="left", color=INK,
                 fontsize=13, fontweight="bold")
    fig.text(0.01, 0.01, "구간마다 난이도·라운드 길이·목표가 달라서 값은 같은 구간 안에서만 비교한다. "
             "red는 RedSoup만 주문하는 보강 런. 흐린 선: 원값, 진한 선: EMA 0.8.", color=MUTED, fontsize=8, ha="left")
    fig.tight_layout(rect=(0, 0.04, 1, 0.94))
    fig.savefig("assets/tb_blue_goal_reached.png", facecolor=SURF); plt.close(fig)


# Pick rate = pots that became that recipe while it was on the board at the first ingredient
# / commits at which that recipe was on the board (same as analyze_pot_fills.py [4]). All four are 45 s / goal 3 inference runs.
PICK = [("eval_undercooked_blue_fix9", "fix9", "#86b6ef"),
        ("eval_undercooked_blue_red_mix", "red_mix", "#5598e7"),
        ("eval_undercooked_blue_urgent_45", "urgent (보너스 1.5)", "#256abf"),
        ("eval_undercooked_blue_urgent3_45", "urgent3 (보너스 3.0, 최종)", "#104281")]
RECIPES = ["GreenSoup", "MixSoup", "RedSoup", "BlueSoup", "GreenBlueSoup", "RedBlueSoup"]


def pick_rates(path):
    ordered, made = collections.Counter(), collections.Counter()
    for r in csv.DictReader(open(path)):
        if r["event"] != "commit" or r["first"] in ("?", "") or r["second"] in ("?", ""): continue
        first = {int(x.split(":")[0]) for x in r["ordersAtFirst"].split("|") if x}
        for o in first: ordered[o] += 1
        if r["wrong"] == "0" and int(r["cooked"]) in first: made[int(r["cooked"])] += 1
    return [made[i] / ordered[i] if ordered[i] else 0 for i in range(len(RECIPES))]


def pick_rate():
    fig, ax = plt.subplots(figsize=(10, 4.4), dpi=150)
    fig.patch.set_facecolor(SURF); style(ax)
    n, w = len(PICK), 0.19
    for j, (run, name, color) in enumerate(PICK):
        rates = pick_rates(f"{RUNS}/{run}/fills.csv")
        xs = [i + (j - (n - 1) / 2) * (w + 0.01) for i in range(len(RECIPES))]
        ax.bar(xs, [r * 100 for r in rates], width=w, color=color, label=name, zorder=2)
        if j == n - 1:
            for x, r in zip(xs, rates):
                ax.text(x, r * 100 + 1, f"{r * 100:.0f}", ha="center", va="bottom", color=INK, fontsize=8)
    ax.set_xticks(range(len(RECIPES))); ax.set_xticklabels(RECIPES, color=INK2, fontsize=9)
    ax.set_ylabel("주문판에 있을 때 그 요리를 만든 비율 (%)", color=INK2, fontsize=9)
    ax.legend(frameon=False, fontsize=9, labelcolor=INK, loc="upper right")
    fig.suptitle("요리별 선택 비율, 45초 / 목표 3 추론", x=0.01, y=0.99, ha="left", color=INK,
                 fontsize=13, fontweight="bold")
    fig.text(0.01, 0.01, "냄비 첫 재료를 넣는 순간 주문판에 그 요리가 있었던 횟수 중 실제로 그 요리를 완성한 비율. "
             "주문 3개 중 하나만 만들면 되므로 합이 100%가 아니다. 숫자는 최종 모델.", color=MUTED, fontsize=8, ha="left")
    fig.tight_layout(rect=(0, 0.04, 1, 0.95))
    fig.savefig("assets/blue_pick_rate.png", facecolor=SURF); plt.close(fig)


goal_reached()
pick_rate()
print("ok")

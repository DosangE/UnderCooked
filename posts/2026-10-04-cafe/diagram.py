"""카페 글의 협동 구조 그림 -> images/02_협동_구조.png. 실행: python diagram.py (matplotlib 필요, 맑은 고딕)"""
import os
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch
font_manager.fontManager.addfont("C:/Windows/Fonts/malgun.ttf")
font_manager.fontManager.addfont("C:/Windows/Fonts/malgunbd.ttf")
plt.rcParams["font.family"] = "Malgun Gothic"
INK, INK2, MUTED = "#0b0b0b", "#52514e", "#8a8984"
A_BG, A_ED = "#eaf2fc", "#2a78d6"
B_BG, B_ED = "#fdeee6", "#eb6834"
C_BG, C_ED = "#f1f0ec", "#8a8984"

fig, ax = plt.subplots(figsize=(16, 7.2), dpi=150)
fig.patch.set_facecolor("white"); ax.set_xlim(0, 17); ax.set_ylim(0, 7.2); ax.axis("off")

def band(y0, y1, fc, label, sub):
    ax.add_patch(FancyBboxPatch((0.15, y0), 16.7, y1 - y0, boxstyle="round,pad=0,rounding_size=0.12", fc=fc, ec="none"))
    ax.text(0.4, y1 - 0.28, label, fontsize=13, fontweight="bold", color=INK, va="top")
    ax.text(0.4, y1 - 0.68, sub, fontsize=10, color=INK2, va="top", linespacing=1.6)

def box(x, y, title, body, ec, w=2.9, h=1.05):
    ax.add_patch(FancyBboxPatch((x - w / 2, y - h / 2), w, h, boxstyle="round,pad=0.02,rounding_size=0.12", fc="white", ec=ec, lw=1.6))
    ax.text(x, y + 0.17, title, ha="center", va="center", fontsize=12, fontweight="bold", color=INK)
    ax.text(x, y - 0.22, body, ha="center", va="center", fontsize=9.5, color=INK2)

def arrow(p, q, label=None, lx=0, ly=0, rad=0):
    ax.add_patch(FancyArrowPatch(p, q, arrowstyle="-|>", mutation_scale=16, lw=1.6, color=INK2,
                                 connectionstyle=f"arc3,rad={rad}", shrinkA=2, shrinkB=2))
    if label:
        ax.text((p[0] + q[0]) / 2 + lx, (p[1] + q[1]) / 2 + ly, label, fontsize=9.5, color=INK, ha="center", va="center",
                bbox=dict(boxstyle="round,pad=0.25", fc="white", ec="none"))

band(4.75, 7.05, A_BG, "Chef A 구역 (북쪽)", "냄비는 여기에만 있다")
band(2.95, 4.55, C_BG, "경계", "카운터 4칸 · 재료함 3개\n셰프는 넘어갈 수 없다")
band(0.15, 2.75, B_BG, "Chef B 구역 (남쪽)", "그릇함과 서빙구는 여기에만 있다")

# A lane
box(5.0, 5.75, "② 냄비에 재료 2개", "주문판을 보고 재료를 고른다", A_ED)
box(9.4, 5.75, "④ 요리 뜨기", "조리 5초. 완료 여부는 관측에 없다", A_ED)
box(15.3, 5.75, "주문판 (둘 다 관측)", "최대 3개 · 각 25초 · 무엇을 만들지", MUTED)
# counter
box(5.0, 3.6, "재료함 초록·빨강·파랑", "양쪽 모두 쓸 수 있다", C_ED, w=3.1, h=0.95)
box(9.4, 3.6, "③ 카운터", "빈 그릇이 넘어간다", C_ED, w=2.6, h=0.95)
box(13.0, 3.6, "⑤ 카운터", "완성 요리가 넘어간다", C_ED, w=2.6, h=0.95)
# B lane
box(5.0, 1.35, "① 그릇함", "빈 그릇 꺼내기", B_ED)
box(13.0, 1.35, "⑥ 서빙구", "주문과 맞으면 팀 +3", B_ED)

arrow((5.0, 4.08), (5.0, 5.22), "재료 (손질대는 구역마다 하나)", lx=-1.9, ly=0)
arrow((6.45, 5.75), (7.95, 5.75), "조리")
arrow((6.45, 1.35), (9.4, 3.12), rad=0.25)
arrow((9.4, 4.08), (9.4, 5.22))
arrow((10.85, 5.75), (13.0, 4.08), rad=-0.25)
arrow((13.0, 3.12), (13.0, 1.88))
ax.text(9.0, 0.48, "빈 그릇과 완성 요리는 반드시 카운터를 건넌다 → 혼자서는 한 접시도 못 낸다", fontsize=11, color=INK, ha="center",
        fontweight="bold")
fig.tight_layout(pad=0.3)
fig.savefig(os.path.join(os.path.dirname(os.path.abspath(__file__)), "images", "02_협동_구조.png"), facecolor="white")

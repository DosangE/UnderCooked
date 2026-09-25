"""한 접시를 내는 데 필요한 이동량을 맵에서 직접 재고, 레이아웃 대안을 비교한다.

decision 1회 = 0.1초 (DecisionPeriod 5 x fixedTimestep 0.02).
한 decision에 '이동 + Interact'를 동시에 낼 수 있다(행동 branch가 따로라서).
그래서 스테이션 사용 비용 = 인접칸까지의 거리로 센다. 여기서 재는 것은 '이동량'이고,
조리 대기(cook_time)와 전달 동기화 비용은 빠져 있다 -- 레이아웃 비교에는 그게 맞다.

용도: "구역별 손질대 + 한쪽에만 있는 그릇함/서빙구" 배치가 밸런스상 괜찮은가.
판정 기준은 두 셰프의 이동량 비(쏠림)다. 한쪽이 놀면 협동이 아니라 배달이다.

실행: python tools/measure_reach.py
"""
import itertools
from collections import deque

# ── 레이아웃 ──────────────────────────────────────────────────────────
# 배열 0번이 맵의 '위'(북쪽, row 최대). KitchenEnv.LayoutTopDown과 같아야 한다.
CURRENT = [
    "####a####",  # row 8  PrepA (A 구역)
    "#.......#",
    "#.......#",
    "#..A....P",  # row 5  Pot (A)
    "#CCG#RCC#",  # row 4  경계: 카운터4 + GreenBox + RedBox (공용)
    "D..B....S",  # row 3  PlateStack (B) / ServingHatch (B)
    "#.......#",
    "#.......#",
    "####b####",  # row 0  PrepB (B 구역)
]

# 대안 C: 그릇함을 A 구역으로 올린다. B가 3스테이션, A가 2스테이션이던 것을 2:2로.
PLATE_IN_A = [
    "####a####",
    "#.......#",
    "#.......#",
    "D..A....P",  # row 5  PlateStack 을 A 구역 서쪽으로
    "#CCG#RCC#",
    "#..B....S",  # row 3  B 쪽 서쪽 벽을 막는다
    "#.......#",
    "#.......#",
    "####b####",
]

COUNTER_ROW = 4
SPAWN = {0: (3, 5), 1: (3, 3)}
DIRS = [(0, 1), (0, -1), (-1, 0), (1, 0)]
FLOOR = ".AB"
NAME_OF = {"a": "PrepA", "b": "PrepB", "P": "Pot", "D": "PlateStack",
           "S": "ServingHatch", "G": "GreenBox", "R": "RedBox"}


class Map:
    def __init__(self, layout, free_prep):
        self.h = len(layout)
        self.w = len(layout[0])
        self.layout = layout
        # free_prep: 손질대가 색 제한 없이 아무 재료나 받는가.
        # True면 각 셰프가 '자기 구역 손질대'를 쓴다 -> 색과 구역의 결합이 끊긴다.
        self.free_prep = free_prep

        self.walkable = {0: set(), 1: set()}
        self.stations = {}
        self.counters = []
        for row in range(self.h):
            for col in range(self.w):
                c = layout[(self.h - 1) - row][col]
                if c in FLOOR:
                    self.walkable[0 if row > COUNTER_ROW else 1].add((col, row))
                elif c == "C":
                    self.counters.append((col, row))
                elif c in NAME_OF:
                    self.stations[NAME_OF[c]] = (col, row)

    def standing(self, cell, zone):
        col, row = cell
        return [(col + dc, row + dr) for dc, dr in DIRS
                if (col + dc, row + dr) in self.walkable[zone]]

    def can_use(self, name, zone):
        return bool(self.standing(self.stations[name], zone))

    def bfs(self, start, zone):
        dist = {start: 0}
        q = deque([start])
        while q:
            cur = q.popleft()
            for dc, dr in DIRS:
                nxt = (cur[0] + dc, cur[1] + dr)
                if nxt in self.walkable[zone] and nxt not in dist:
                    dist[nxt] = dist[cur] + 1
                    q.append(nxt)
        return dist

    def leg(self, frm, target, zone):
        """target = 스테이션 이름 또는 'COUNTER'. (비용, 도착해서 서 있는 칸)"""
        cells = self.counters if target == "COUNTER" else [self.stations[target]]
        dist = self.bfs(frm, zone)
        best = None
        for cell in cells:
            for stand in self.standing(cell, zone):
                if stand in dist and (best is None or dist[stand] < best[0]):
                    best = (dist[stand], stand)
        return best

    def chain_cost(self, zone, legs):
        pos = SPAWN[zone]
        total = 0
        for target in legs:
            cost, pos = self.leg(pos, target, zone)
            total += cost
        return total

    def prep_for(self, color_green, zone):
        """그 셰프가 이 재료를 손질할 때 쓰는 손질대. 못 쓰면 None."""
        if self.free_prep:
            # 색 제한 없음 -> 자기 구역 손질대를 쓴다
            for name in ("PrepA", "PrepB"):
                if self.can_use(name, zone):
                    return name
            return None
        # 참고용: 예전처럼 색으로 고정했을 때
        name = "PrepA" if color_green else "PrepB"
        return name if self.can_use(name, zone) else None


def plan(kitchen, greens, reds):
    """재료를 두 셰프에게 나눠 맡기는 모든 경우를 따져 max(A,B)가 최소인 배정을 찾는다.
    '완벽하게 협력하는 두 사람'의 하한이고, 그때의 쏠림이 레이아웃의 밸런스다."""
    items = [True] * greens + [False] * reds
    best = None

    for assign in itertools.product((0, 1), repeat=len(items)):
        legs = {0: [], 1: []}
        ok = True

        for color_green, chef in zip(items, assign):
            box = "GreenBox" if color_green else "RedBox"
            if not kitchen.can_use(box, chef):
                ok = False
                break
            prep = kitchen.prep_for(color_green, chef)
            if prep is None:
                ok = False
                break

            legs[chef] += [box, prep]
            if kitchen.can_use("Pot", chef):
                legs[chef].append("Pot")
            else:
                legs[chef].append("COUNTER")
                legs[1 - chef] += ["COUNTER", "Pot"]
        if not ok:
            continue

        pot_chef = 0 if kitchen.can_use("Pot", 0) else 1
        plate_chef = 0 if kitchen.can_use("PlateStack", 0) else 1
        serve_chef = 0 if kitchen.can_use("ServingHatch", 0) else 1

        # 빈 그릇 -> 냄비
        legs[plate_chef].append("PlateStack")
        if plate_chef == pot_chef:
            legs[pot_chef].append("Pot")          # 그대로 떠낸다
        else:
            legs[plate_chef].append("COUNTER")
            legs[pot_chef] += ["COUNTER", "Pot"]

        # 완성 요리 -> 서빙구
        if serve_chef == pot_chef:
            legs[pot_chef].append("ServingHatch")
        else:
            legs[pot_chef].append("COUNTER")
            legs[serve_chef] += ["COUNTER", "ServingHatch"]

        a = kitchen.chain_cost(0, legs[0])
        b = kitchen.chain_cost(1, legs[1])
        if best is None or max(a, b) < max(best[0], best[1]):
            best = (a, b)

    return best


RECIPES = [("GreenSoup(초록2)", 2, 0), ("MixSoup(초록1+빨강1)", 1, 1), ("RedSoup(빨강2)", 0, 2)]

VARIANTS = [
    ("A. 예전 (손질대가 색을 가림)", CURRENT, False),
    ("B. 현재 (구역 손질대, 색 무관)", CURRENT, True),
    ("C. 그릇함을 A 구역으로", PLATE_IN_A, False),
    ("D. B + C 둘 다", PLATE_IN_A, True),
]

for title, layout, free_prep in VARIANTS:
    kitchen = Map(layout, free_prep)
    print("=" * 66)
    print(title)
    print(f"{'요리':24s} {'A':>5s} {'B':>5s} {'쏠림':>7s} {'병목':>7s}")
    totals = [0, 0]
    worst = 0.0
    for name, greens, reds in RECIPES:
        a, b = plan(kitchen, greens, reds)
        totals[0] += a
        totals[1] += b
        skew = max(a, b) / max(1, min(a, b))
        worst = max(worst, skew)
        print(f"{name:24s} {a:5d} {b:5d} {skew:6.1f}:1 {max(a, b) * 0.1:6.1f}s")
    total_skew = max(totals) / max(1, min(totals))
    print(f"{'합계 (세 레시피 균등)':22s} {totals[0]:5d} {totals[1]:5d} {total_skew:6.1f}:1")
    print(f"  레시피 하나 기준 최악 쏠림: {worst:.1f} : 1")
    print()

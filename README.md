# UnderCooked

### Unity ML-Agents(MA-POCA)로 학습한 2인 협동 요리 에이전트

**두 셰프가 주문판을 읽고 수프를 만들어 서빙한다. 냄비와 서빙구가 서로 다른 구역에 있어서, 혼자서는 한 접시도 낼 수 없다.**

![](assets/demo.gif)

Overcooked에서 사람이 잘하는 팀은 두 가지를 한다. 주문판을 보고 **무엇을 만들지** 정하고, 동료와 **누가 무엇을 나를지** 나눈다.
이 저장소는 Overcooked를 극단적으로 단순화한 주방을 만들고, 두 셰프가 이 두 가지를 스스로 배우게 한다.
재료 2종·요리 3종 최종 모델은 가장 어려운 난이도에서 **3접시 목표 달성률 97.8%**(Unity 추론 97.1%, 478판)다.
이후 파랑 재료를 넣어 **재료 3종·요리 6종**으로 넓혔고, 같은 난이도에서 **Unity 추론 99.4%**다.
다만 초록이 안 들어간 요리를 덜 고르는 편향이 남아 있다 (§9).

```
주문판 (최대 3개, 각 25초)
      │  "▶ MixSoup 18초 / GreenSoup 9초 / RedSoup 22초"
      ▼
┌────────────────────────┐   재료함 → (손질대) → 냄비에 재료 2개 → 5초 조리 → 그릇에 담기
│  Chef A — 북쪽 구역      │   냄비는 여기에만 있다
└────────────────────────┘
      ▲ 빈 그릇 (+ B가 손질한 재료)   │ 완성 요리
      │                              ▼
┌────────────────────────┐   두 구역을 잇는 유일한 통로. 걸어서는 못 넘는다
│      카운터 4칸           │
└────────────────────────┘
      ▲                              │
      │                              ▼
┌────────────────────────┐   그릇함에서 빈 그릇을 넘기고, 받은 요리를 서빙구에 낸다
│  Chef B — 남쪽 구역      │   그릇함과 서빙구는 여기에만 있다
└────────────────────────┘
```

핵심은 **협동을 보상이 아니라 맵 구조로 강제한 것**이다. 그릇함은 B에, 냄비는 A에, 서빙구는 다시 B에 있어서 **빈 그릇과 완성 요리는 반드시 카운터를 건너야 한다.** 혼자 다 하는 정책은 물리적으로 존재하지 않는다. 재료는 A가 직접 가져올 수도, B가 손질해 넘길 수도 있다.
그리고 모든 요리는 **재료 정확히 2개**라서 어떤 주문이 와도 작업량이 같다. 학습이 풀어야 할 문제는 "주문을 읽고 무엇을 만들지 정하는 것"으로 좁혀진다.

---

## 시연 (재료 2종 모델)

| 최종 정책 (`undercooked_final3`, 최종 난이도) | 학습 곡선 — 목표 달성률 (런 5개 이어 붙임) |
|:--:|:--:|
| ![](assets/demo.gif) | ![](assets/tb_goal_reached.png) |

GIF는 한 에피소드(24.2초, 3접시)다. 위쪽 띠는 녹화할 때 기록한 서빙 수와 대기 주문이다.
곡선의 급락(누적 4.2M~4.6M)은 커리큘럼이 레시피 2종과 손질을 연달아 켠 구간이고, 이후 정책이 스스로 회복한다 (§3).

---

## 1. 게임 규칙

| 요리 | 냄비 내용물 | |
|---|---|---|
| GreenSoup | 초록 ×2 | 재료 2종 버전부터 |
| MixSoup | 초록 + 빨강 | 〃 |
| RedSoup | 빨강 ×2 | 〃 |
| BlueSoup | 파랑 ×2 | 재료 3종 확장 (§9) |
| GreenBlueSoup | 초록 + 파랑 | 〃 |
| RedBlueSoup | 빨강 + 파랑 | 〃 |

재료 3종에서 2개를 고르는 조합 6가지가 전부 요리다. 그래서 냄비가 차면 반드시 어떤 요리가 된다.
커리큘럼의 `recipe_pool_size`가 3 이하면 파랑 재료함은 숨겨지고 막혀서, 재료 2종 버전과 똑같이 동작한다.

```
재료 줍기 → (손질대) → 냄비에 2개 → 자동 조리(5초) → 그릇에 담기 → 서빙구에 제출
```

- 주문판에 최대 3개의 주문이 제한 시간과 함께 뜬다. 낸 요리와 **일치하는 대기 주문이 있어야** 점수다.
- 주문에 없는 요리를 내면 낭비(−0.5), 만료된 주문은 팀 벌점(−0.5).
- 에피소드는 45초(약 450 decision). 목표 접시 수를 채우면 성공으로 끝난다.

```
         col0  col1  col2  col3  col4  col5  col6  col7  col8
 row8     #     #     #     #    [pa]   #     #     #     #     ← A 구역 손질대
 row7     #     .     .     .     .     .     .     .     #    ┐
 row6     #     .     .     .     .     .     .     .     #    │ Chef A 구역
 row5     #     .     .     A     .     .     .     .   [POT]  ┘ (냄비)
 row4     #    [C]   [C]   [G]   [U]   [R]   [C]   [C]    #     ← 경계 (카운터 4칸)
 row3    [D]    .     .     B     .     .     .     .    [S]   ┐
 row2     #     .     .     .     .     .     .     .     #    │ Chef B 구역
 row1     #     .     .     .     .     .     .     .     #    ┘ (그릇함 D / 서빙구 S)
 row0     #     #     #     #    [pb]   #     #     #     #     ← B 구역 손질대
```

재료함(G 초록, U 파랑, R 빨강)은 경계에 있어서 양쪽이 쓰고, 손질대도 구역마다 하나씩 있다. **누가 어느 재료를 맡을지는 맵이 정해주지 않는다.**
파랑 재료함(U)은 재료 2종 버전에서 벽이던 경계 가운데 칸에 넣었다.

---

## 2. 왜 이렇게 설계했나

### 2-1. 손질대를 '색'이 아니라 '구역'으로 나눴다

처음에는 초록 손질대(A) / 빨강 손질대(B)였다. 그러면 재료 색이 담당자를 정해 버린다. 두 셰프가 완벽히 협력할 때의 이동량을 재 보면:

| 요리 | 색으로 나눴을 때 (A:B) | 구역으로 나눴을 때 (A:B) |
|---|---|---|
| GreenSoup | 20 : 8 — **2.5:1** | 8 : 14 — 1.8:1 |
| RedSoup | 6 : 26 — **4.3:1** | 10 : 16 — 1.6:1 |

RedSoup에서 A가 하는 일은 "냄비 앞에 서서 받아 넣기"뿐이었다. 구역으로 나누면 분담을 둘이 런타임에 정해야 한다 — MA-POCA가 풀라고 있는 문제다.
(이 측정은 재료 2종 맵 기준이다. 파랑도 같은 원칙으로 경계에 두었다.)

### 2-2. 주문은 보여주고, 조리 완료는 숨긴다

- **주문판은 관측에 넣는다.** 무엇을 만들지는 기억이 아니라 *읽어야 하는* 정보다. 없으면 정책은 기대값이 가장 높은 요리 하나만 만든다.
- **조리 완료 여부는 관측에서 뺐다.** Action Mask로도 새지 않게 막았다(뜨기 마스크가 완료 순간에 열리면 그게 곧 관측이다). 재료를 언제 다 넣었는지 기억해야 낭비 없이 요리를 뜰 수 있다 — Memory(LSTM)를 쓰는 근거다.

### 2-3. 중간 보상은 즉시 주고, 무산되면 회수한다

손질 +0.2, 투입 +0.3, 카운터 전달 +0.15는 **즉시** 준다. 서빙까지는 11단계짜리 2인 체인이라 즉시 주지 않으면 초반에 학습 신호가 없다.
대신 그 진행이 **서빙으로 이어지지 않으면 전부 회수**한다(냄비 비우기, 버리기, 틀린 제출, 에피소드 종료 정산). 회수하지 않으면 서빙 없이 점수만 쌓는 경로가 정직한 플레이를 이긴다 — 실제로 그랬다 (§4).

회수 통로는 진행 보상이 `KitchenGroup.ClawBack`, 전달 보상이 `ClawBackTransfer` 하나씩이다. 다만 회수가 최적 정책을 그대로 보존한다는 보장은 없다. γ = 0.99에서 지금 받은 +r을 k스텝 뒤에 회수하면 할인 리턴에는 r(1 − γᵏ)이 남는다. 회수는 서빙 없는 경로의 이득을 그만큼 작게 만들 뿐이고, 새는 경로가 하나라도 있으면 그쪽 이득은 그대로 남는다.

---

## 3. 학습 경로 (재료 2종) — 왜 런 5개를 이어 붙였나

| 런 | 설정 | 스텝 / 시간 | 결과 |
|---|---|---|---|
| `undercooked_v1` | 기본 커리큘럼, 무작위 초기화 | 1.44M / 19분 (중단) | **서빙 0회** |
| `undercooked_lesson0` | 가장 쉬운 난이도 고정 | 3M / 39분 | 1.96M에 첫 서빙, 99.7% |
| `undercooked_v2` | 기본 커리큘럼, lesson0에서 이어서 | 8M / 1시간 39분 | 최종 난이도 54% |
| `undercooked_final` | 최종 난이도 고정, v2에서 | 3M / 39분 | 74% |
| `undercooked_final2` | 같은 설정, final에서 | 3M / 40분 | 90.5% |
| `undercooked_final3` | 같은 설정, final2에서 | 3M / 40분 | **97.8%** → `models/undercooked.onnx` |

최종 모델의 체인은 **20M 스텝, 약 4시간 16분**(v1 제외)이다. 실패한 v1까지 넣으면 21.44M 스텝, 약 4시간 35분. 스텝은 에이전트 32명 합계이고, RTX 2080 SUPER에서 1M 스텝당 약 13분이다.

- **v1은 커리큘럼이 첫 서빙보다 빨랐다.** 첫 서빙에 약 2M 스텝이 걸리는데, progress 기준 커리큘럼은 1.2M에 레시피 2종, 1.6M에 손질을 켰다. 서빙 보상 +3을 한 번도 못 본 채 난이도만 올라갔다. 그래서 **lesson0을 먼저 고정 학습 → 커리큘럼 → 최종 난이도 고정**으로 바꿨다.
- **v2는 손질 전환에서 무너졌다.** 손질이 켜지자 생재료를 냄비에 넣던 정책이 냄비를 한 번도 못 채우고 서빙 0이 됐다. 약 1.5M 스텝 뒤 스스로 손질 경로를 찾아 회복했다. progress 관문은 이전 단계를 풀었는지 보지 않는다.
- **final 이후는 같은 설정으로 3M씩.** 매 런 학습률이 3e-4에서 다시 시작하고, 74% → 90.5% → 97.8%로 끝까지 올랐다.

---

## 4. 시행착오 — 곡선은 올라가는데 내용은 가짜였다

학습을 한 번도 돌리기 전에 환경과 보상에서 **19건**을 고쳤다(전체 목록과 근거는 [`docs/DESIGN.md`](docs/DESIGN.md) §4). 그중 결과를 바꾼 것들:

**보상 어뷰징은 막을 때마다 옆 경로가 열렸다.** 카운터에 놓았다 집기만 반복해도 점수가 났고, 막으니 A↔B 핑퐁이, 그걸 막으니 빈 그릇 왕복이 열렸다. 가장 심한 경로(손질 → 투입 → 비우기 반복)를 계산하면:

| 전략 (45초 에피소드, 셰프 한 명) | 보상 | 서빙 |
|---|---|---|
| 손질 → 투입 → 비우기 반복 | **+14 ~ +18** | **0회** |
| 정직하게 1접시 | +6.2 | 1회 |

학습이 실제로 최적화하는 값(개인 + 팀 보상)에서 파밍이 정직한 플레이를 이긴다. 그대로 학습했다면 **보상 곡선은 오르는데 서빙은 0인** 정책이 나왔을 것이다. 공통 원인은 전부 "되돌려야 하는 보상이 회수 체계 바깥에 있었다"였고, 찾을 때마다 회귀 검사 항목으로 남겼다(재료 2종 버전 14개, 지금 17개).

**커리큘럼 임계값이 존재하지 않는 보상을 보고 있었다.** 접시 수 관문은 3.0 / 6.0이었는데, ML-Agents가 비교하는 `Environment/Cumulative Reward`에는 **팀 보상이 들어 있지 않다.** 개인 보상만으로는 최대 약 +0.3이라 구조적으로 통과 불가능했다. 개인 보상 단위(−0.3 / +0.1)로 다시 잡았다.

**검사 도구 자체가 틀렸다.** 어뷰징이 막혔다고 판정한 검증 스크립트가 실제 동작이 아니라 의도한 값을 직접 계산하고 있었다. 관측 개수 검사는 항상 통과하는 껍데기였다. 지금 회귀 검사는 `ChefAgent.OnActionReceived`에 행동을 넣고 **보상 변화만 읽는다.**

**결과를 두 번 잘못 읽었다.**

1. 평균만 보고 "만드는 속도가 병목"이라고 적었다. 실패 에피소드만 뽑아 보니 냄비는 충분히 채우는데 **주문과 다른 레시피**를 만들고 있었다. 평균은 성공과 실패를 섞는다.
2. final3에서 주문에 없는 재료 투입 벌점을 −0.1 → −0.3으로 올렸고 90% → 98%를 벌점 효과로 적었다. **시드 3개씩 대조해 보니 −0.1도 96.7%였다.** 벌점 증가의 효과는 확인되지 않았고, 향상은 대부분 추가 학습으로 설명된다 (§5).

---

## 5. 보완 실험 (재료 2종)

### 5-1. 벌점 −0.1 vs −0.3 (final2에서 3M, 시드 3개씩)

| 조건 | 목표 달성 평균 (범위) | 잘못 채움 평균 (범위) |
|---|---|---|
| −0.1 | **96.7%** (95.7–97.6) | 0.27 (0.26–0.29) |
| −0.3 | **95.8%** (94.7–97.3) | 0.34 (0.29–0.40) |

범위가 겹친다. **이번 실험에서 벌점 증가의 개선 효과는 확인되지 않았다.** −0.1로도 같은 만큼 올랐으므로 final2 → final3의 향상은 대부분 3M 추가 학습으로 설명된다. 시드 간 편차는 약 2%p이고, 97.8%는 그 범위의 위쪽 끝이다.

### 5-2. 처음부터 한 번에 — 성공률 단계 커리큘럼 (시드 4개, 20M)

| 시드 | 결과 |
|---|---|
| s2 | 13.28M에 최종 난이도, **94.5%** |
| s4 | 17.60M에 최종 난이도, **92.0%** |
| s1, s3 | 0단계(가장 쉬운 단계)에서 서빙을 못 찾고 4.5M에 중단 |

무작위 초기화부터 한 런으로 최종 난이도까지 가는 커리큘럼은 **존재한다.** 손질을 50% → 100%로 나눈 단계가 v2의 손질 붕괴를 막았다.
하지만 4번 중 2번은 첫 서빙을 못 찾았고, 같은 조건 런 5개 중 3개만 서빙을 찾았다. 성패는 시드가 정한다 (같은 시드면 학습이 비트 단위로 재현된다).

### 5-3. 남은 실패는 거의 전부 "주문에 없는 레시피로 채운 판"이다

| | final3 (최종 모델) | s2 이어 학습 (한 번에 학습) |
|---|---|---|
| 목표 달성 (추론, 약 1100판) | 97.5% | 95.6% |
| 잘못 채우지 않은 판의 성공률 | 99.2% | 99.9% |
| 잘못 채운 판의 비율 | 16.7% | 21.8% |
| 그런 판의 성공률 (만회) | **88.6%** | 80.2% |

잘못 채우지 않으면 두 모델 모두 99% 이상 이긴다. 차이는 **레시피를 틀리는 빈도**와 **틀린 뒤 버리고 다시 만드는 만회**다.
틀린 요리를 들고 있으면 팀에 초당 벌점을 주는 실험도 했다. 버리기는 늘었지만(0.46 → 0.73회) 다시 만들다 또 틀려서 **성공률은 그대로**였다. 남은 병목은 레시피 선택 정확도다.

---

## 6. ML-Agents 설계

**에이전트** — 셰프 2명 1팀(`SimpleMultiAgentGroup`, Behavior Name `Chef`, Team ID 0). 주방 16개를 동시에 돌려 셰프 32명이 학습한다.
목표 달성은 `EndGroupEpisode()`, 45초 타임아웃은 `GroupEpisodeInterrupted()` — 타임아웃을 끝으로 처리하면 가치가 0으로 잘려 "시간이 지나면 가치 0"을 잘못 배운다.

**관측 (145차원, 재료 2종 버전은 103)** — 자기 위치만 정규화한 절대좌표이고, 동료·카운터·스테이션 위치는 나를 기준으로 한 상대좌표다. `normalize: true`

| 묶음 | 차원 (재료 3종) | (재료 2종) |
|---|---|---|
| 자기 위치(정규화 절대좌표) · 바라보는 방향 · 손에 든 것 | 2 + 4 + 14 | 2 + 4 + 9 |
| 동료 상대좌표 · 동료 손에 든 것 | 2 + 14 | 2 + 9 |
| 냄비의 재료별 개수 (조리 완료 여부는 **없음**) | 3 | 2 |
| 카운터 4칸 × (내용물 + 상대좌표 2) | 4 × 16 | 4 × 11 |
| 스테이션 상대좌표 (재료함 + 냄비·그릇함·서빙구·손질대 2) | 8 × 2 | 7 × 2 |
| 남은 시간 · 손질 필요 플래그 | 1 + 1 | 1 + 1 |
| **주문 슬롯 3 × (요리 one-hot + 남은 시간 1 + 유효 1)** | **3 × 8** | **3 × 5** |

재료가 늘어난 만큼 one-hot 크기만 커졌고 구조는 같다. 그래서 103차원 모델(`models/undercooked.onnx`)은 지금 코드에서 돌지 않는다.

슬롯 인덱스는 섞지 않는다. 같은 주문이 매 스텝 같은 자리에 있어야 "2번 슬롯이 급하다"를 배울 수 있다.

**행동** — Discrete 2 branch: 이동 5(정지/상/하/좌/우) + 상호작용 2(없음/Interact). 이동 방향이 곧 시선이고, 바닥이 아니면 회전만 한다.
마스킹: 무의미한 이동, 지금 손 상태로 할 게 없는 Interact. **주문에 맞는 재료인지는 마스킹하지 않는다** — 그건 보상이 가르쳐야 할 실수다.

**보상**

| 항 | 값 | 이유 |
|---|---|---|
| 서빙 (주문과 일치) | 팀 +3.0 | 유일한 실제 성과 |
| 목표 달성 | 팀 +2.0 | 3접시를 다 채우게 |
| 재료 투입 | 팀 +0.3 | 아직 대기 주문 하나라도 만들 수 있을 때만. 무산되면 회수 |
| 손질 | 팀 +0.2 | 무산되면 회수 |
| 주문 만료 | 팀 −0.5 | 급한 주문부터. yaml `order_expired_penalty`로 바꿀 수 있다 |
| 가장 급한 주문을 채운 서빙 | 팀 +보너스 (기본 꺼짐) | yaml `urgent_serve_bonus`. 재료 3종 확장에서 추가 (§9) |
| 카운터 전달 | 개인 +0.15 (양쪽) | 동료가 **집어간** 순간에만, 물건마다 1회. 서빙으로 안 이어지면 회수 |
| 주문에 없는 재료 투입 | 개인 −0.3 (final2까지 −0.1) | 냄비가 어떤 대기 주문도 만들 수 없게 되는 순간 |
| 주문에 없는 요리 서빙 | 개인 −0.5 | "아무거나 만들어 내보기"가 아무것도 안 하기보다 나빠야 한다 |
| 덜 끓은 냄비 뜨기 | 개인 −0.02 | 조리 완료를 기억하게 |
| 매 스텝 | 개인 −0.002 | 빨리 끝내기 |

이 밖에 집기 +0.05, 냄비 비우기 −0.05(데드락 탈출용), 요리가 아닌 물건 버리기 −0.2가 있다. 전체 표와 근거는 [`docs/DESIGN.md`](docs/DESIGN.md) §2.

POCA는 동료의 개인 보상도 내 리턴에 더한다(`add_groupmate_rewards`). 이 환경의 "개인 벌점"은 최적화 관점에서 사적이지 않다.

**Memory** — LSTM, `sequence_length` 64, `memory_size` 128.

**커리큘럼** (`configs/undercooked.yaml`)

| 파라미터 | 진행 | 기준 |
|---|---|---|
| `target_dishes` | 1 → 2 → 3 | 개인 보상 −0.3 / +0.1 |
| `recipe_pool_size` | 1 → 2 → 3 | progress 0.15 / 0.30 |
| `needs_prep` | 끔 → 켬 | progress 0.20 |
| `cook_time` | 2초 → 5초 | progress 0.35 |
| `order_slots` | 1 → 2 → 3 | progress 0.50 / 0.65 |

reward 기준은 하나뿐이다. 여럿이면 서로의 기준선을 움직여 같이 터지거나 같이 멈춘다. `recipe_pool_size`가 **주문을 읽게 만드는** 손잡이다 — 1종이면 읽을 게 없다.

재료 3종 확장에서 쓰는 yaml 파라미터. 모두 없으면 예전과 같게 동작한다.

| 파라미터 | 뜻 | 기본 |
|---|---|---|
| `recipe_pool_size` | 주문에 나오는 요리 수. 4 이상이면 파랑이 등장한다 | 6 |
| `recipe_pool_start` | 주문을 `[start, pool)` 범위에서 뽑는다. 이미 익힌 요리(지름길)를 빼는 데 쓴다 | 0 |
| `episode_duration` | 라운드 길이(초). 씬 기본값은 45초이고 `StartupValidator`는 그 기본값을 검사한다 | 씬 값 |
| `order_expired_penalty` | 주문 만료 팀 벌점 | −0.5 |
| `urgent_serve_bonus` | 서빙한 주문이 주문판에서 가장 급했을 때 팀 보너스 | 0 (꺼짐) |

**하이퍼파라미터** — `poca`, 은닉 256 × 2층, 학습률 3e-4 linear, batch 1024, buffer 20480, β 0.01, ε 0.2, λ 0.95, epoch 3, γ 0.99, `time_horizon` 128.

---

## 7. 결과 (재료 2종)

| 지표 (최종 난이도) | 학습 (final3, 2.5–3M) | Unity 추론 (478판) | 아무것도 안 할 때 |
|---|---|---|---|
| 목표 달성 | **97.8%** | **97.1%** | 0% |
| 서빙 / 에피소드 | 2.97 | 2.96 | 0 |
| 주문에 없는 레시피로 채움 | 0.22 (7%) | – | – |
| 팀 보상 | +13.29 | – | −1.5 |

최종 난이도: 손질 켜짐, 레시피 3종, 조리 5초, 주문 슬롯 3, 주문 25초, 목표 3접시. 성공한 판은 평균 27.7초에 끝나고, 0접시로 끝난 판은 없었다(1접시 3 / 2접시 11 / 3접시 464).

| 개인 보상 | 팀 보상 | 잘못 채움 |
|:--:|:--:|:--:|
| ![](assets/tb_cumulative_reward.png) | ![](assets/tb_group_reward.png) | ![](assets/tb_pot_committed_wrong.png) |

`Environment/Cumulative Reward`는 **개인 보상만** 담아서 최대 약 +0.46이다. 성과는 팀 보상과 목표 달성률로 읽는다.

---

## 8. 실행

```bash
conda activate mlagents

# 최종 모델을 만든 순서. 각 줄 실행 → "Listening on port 5004"가 뜨면 Unity에서 Play
mlagents-learn configs/undercooked_lesson0.yaml      --run-id=undercooked_lesson0 --torch-device cuda
mlagents-learn configs/undercooked.yaml              --run-id=undercooked_v2     --initialize-from=undercooked_lesson0 --torch-device cuda
mlagents-learn configs/undercooked_final_pen01.yaml  --run-id=undercooked_final  --initialize-from=undercooked_v2     --torch-device cuda
mlagents-learn configs/undercooked_final_pen01.yaml  --run-id=undercooked_final2 --initialize-from=undercooked_final  --torch-device cuda
mlagents-learn configs/undercooked_final_pen03.yaml  --run-id=undercooked_final3 --initialize-from=undercooked_final2 --torch-device cuda

# 처음부터 한 번에 (단계 커리큘럼, 20M, 약 4시간 15분. 시드 2, 4만 성공했다)
mlagents-learn configs/undercooked_stage.yaml --run-id=undercooked_stage_s2 --seed=2 --torch-device cuda
```

재료 3종·요리 6종 모델을 만든 순서 (§9). 각 yaml 머리말에 같은 명령과 그 런을 만든 이유가 있다.

```bash
mlagents-learn configs/undercooked_blue_stage.yaml --run-id=undercooked_blue_s1    --seed=1 --torch-device cuda   # 25M, 무작위 초기화
mlagents-learn configs/undercooked_blue_final.yaml --run-id=undercooked_blue_final --initialize-from=undercooked_blue_s1    --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_fix.yaml   --run-id=undercooked_blue_fix   --initialize-from=undercooked_blue_final --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_fix9.yaml  --run-id=undercooked_blue_fix9  --initialize-from=undercooked_blue_fix   --seed=1 --torch-device cuda   # 92.4%
mlagents-learn configs/undercooked_blue_red.yaml     --run-id=undercooked_blue_red     --initialize-from=undercooked_blue_fix9 --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_red_mix.yaml --run-id=undercooked_blue_red_mix --initialize-from=undercooked_blue_red  --seed=1 --torch-device cuda   # 99.4%
# 90초 / 목표 8 조건으로 RedSoup 보강 (long -> long8 -> long8_g995 -> urgent -> urgent3)
mlagents-learn configs/undercooked_blue_long.yaml       --run-id=undercooked_blue_long       --initialize-from=undercooked_blue_red_mix    --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_long8.yaml      --run-id=undercooked_blue_long8      --initialize-from=undercooked_blue_long       --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_long8_g995.yaml --run-id=undercooked_blue_long8_g995 --initialize-from=undercooked_blue_long8      --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_blue_urgent.yaml     --run-id=undercooked_blue_urgent     --initialize-from=undercooked_blue_long8_g995 --seed=1 --torch-device cuda   # 99.6%
mlagents-learn configs/undercooked_blue_urgent3.yaml    --run-id=undercooked_blue_urgent3    --initialize-from=undercooked_blue_urgent     --seed=1 --torch-device cuda   # 99.4%, 최종
```

`long`, `long8`, `long8_g995`는 효과가 없어 중간에 멈춘 런이다(2.3M / 4.8M / 3.0M). 같은 결과를 내려면 같은 지점에서 멈춘다.

씬은 `UnityProject/Assets/Scenes/UnderCooked.unity`. **`mlagents-learn`을 먼저 띄우고 Play한다** — 반대로 하면 사람 플레이로 판정되어 주방 하나로만 학습된다. Play 직후 콘솔에 `학습 모드 (트레이너 연결됨, 주방 16개)`가 찍혀야 한다.
실행 전 체크리스트(회귀 검사 등)는 [`.claude/docs/TRAINING.md`](.claude/docs/TRAINING.md).

**모델로 보기** — 셰프의 Behavior Parameters > Model에 6종 최종 모델(`archive/runs/undercooked_blue_urgent3/Chef.onnx`)을 넣고, `KitchenEnv`의 `defaultTargetDishes`를 3, `defaultOrderDuration`을 25로 바꾼 뒤 트레이너 없이 Play. 트레이너가 없으면 yaml 대신 이 기본값(2접시 / 20초, 요리 6종)이 쓰인다.
재료 2종 모델 `models/undercooked.onnx`(관측 103)는 이 코드에서 돌지 않는다. 그 모델은 `main` 브랜치의 재료 2종 버전에서 본다.

**직접 플레이** — Behavior Type을 `Heuristic Only`로 바꾸고 Play. 주방 하나만 남고 주문판, 스테이션 깜빡임(🟩 집기 / 🟦 놓기 / 🟥 버리기), 행동 로그가 켜진다.

| 셰프 | 이동 | Interact |
|---|---|---|
| A (북쪽) | `WASD` | `LeftShift` |
| B (남쪽) | 방향키 | `RightShift` / `Enter` |

환경: Unity 6000.3.18f1, `com.unity.ml-agents` 4.0.3, Python 3.10.12, `mlagents` 1.2.0.dev0(소스 설치), torch 2.2.2+cu121.

---

## 9. 확장: 재료 3종 · 요리 6종

재료 2종 모델에 파랑 재료를 넣었다. 요리는 재료 2개 조합 6가지가 되고, 작업량(재료 2개)은 그대로다.
전체 과정과 수치는 [`reports/2026-10-04-blue-ingredient.md`](reports/2026-10-04-blue-ingredient.md).

| 모델 (평가: 45초 / 목표 3, Unity 추론 600k) | 목표 달성 | 잘못 채움 | RedSoup 선택 |
|---|---|---|---|
| `undercooked_blue_fix9` | 92.4% | 9% | 1.5% |
| `undercooked_blue_red_mix` | 99.4% | 3% | 6.5% |
| `undercooked_blue_urgent` (보너스 1.5) | 99.6% | 3% | 10.8% |
| **`undercooked_blue_urgent3`** (보너스 3.0, 최종) | **99.4%** | 4% | **16.6%** |

"RedSoup 선택"은 냄비에 첫 재료를 넣는 순간 주문판에 RedSoup이 있었던 경우 중 실제로 RedSoup을 만든 비율이다.

**1. 새 재료를 안 배웠다 — 성공률 관문이 지름길에 속았다.**
- 주문 슬롯이 3개라서 이미 아는 GreenSoup·MixSoup만 만들어도 45~72%가 나왔다. 파랑은 한 번도 안 썼다.
- 고친 방법: 단계마다 이미 익힌 요리를 주문에서 뺐다(`recipe_pool_start`). 7단계는 BlueSoup만, 8단계는 지름길 2종을 뺀 4종이다.
- 그 뒤 6종을 섞어 이어 학습하자 26% → 93%가 됐다.

**2. 초록 없는 요리를 버렸다 — 버리는 주문에 대가가 없었다.**
- 6종을 다 할 줄 알면서도 RedSoup·RedBlueSoup 주문의 절반을 만료시켰다.
  - 매 판 일부 주문은 버릴 수밖에 없고, 어느 요리든 보상이 같다.
  - 그래서 정책은 "초록으로 시작하기"(주문판에 초록 요리가 있을 확률 87.5%)를 굳혔다.
- 효과가 없었던 시도:
  - RedSoup만 따로 학습시킨 뒤 다시 섞었다 → 다시 잊었다.
  - 라운드를 90초로 늘렸다.
  - 만료 벌점을 −3으로 키우고 γ를 0.995로 올렸다.

  만료 벌점은 결정 뒤 10~30초 늦게 와서 거의 지워진다.
- **서빙 순간에 "가장 급한 주문을 채웠다"를 바로 보상하자(`urgent_serve_bonus`) 처음으로 움직였다** (6.5% → 10.8%).
  보너스를 1.5 → 3.0으로 키우고 학습률을 다시 시작하자 16.6%가 됐다. 학습 조건(90초 / 목표 8)으로 재면 21.1%다.

남은 편향: 최종 모델은 초록이 들어간 요리를 주문판에 있을 때 47~90% 고르고, 초록 없는 요리는 13~36%만 고른다.
지금 요리는 one-hot으로 주어져서 "RedSoup = 빨강 2개"를 정책이 따로 외워야 한다.
다음 후보는 주문과 냄비를 같은 "재료 구성" 형식으로 주는 관측 변경이다.

---

## 10. 현재 상태

- **되는 것**
  - 재료 2종: 최종 난이도 97.8%(추론 97.1%). 처음부터 한 번에 학습하는 단계 커리큘럼으로도 최종 난이도에 도달했다(시드 4개 중 2개, 94.5% / 92.0%).
  - 재료 3종·요리 6종: 추론 99.4%, 잘못 채움 4% (§9). 최종 모델은 `archive/runs/undercooked_blue_urgent3/Chef.onnx`.
- **안 되는 것** — 요리 선택의 편향.
  - 재료 2종 모델은 판의 약 17~22%에서 한 번 이상 주문에 없는 레시피로 냄비를 채웠다. 벌점 크기나 틀린 요리 보유 벌점으로는 이 비율이 줄지 않았다.
  - 6종 모델은 잘못 채움은 줄었지만, 초록 없는 요리를 덜 고른다.
- **아직 안 푼 것**
  - 단계 커리큘럼의 0단계 서빙 발견(시드 의존)
  - 6종 모델을 무작위 초기화부터 한 번에 학습하는 재현
  - Memory 효과 검증(on/off 비교에서 차이가 나오지 않았다)

| 문서 | 내용 |
|---|---|
| [`reports/2026-10-04-blue-ingredient.md`](reports/2026-10-04-blue-ingredient.md) | 재료 3종·요리 6종 확장 전체 (지름길, RedSoup 보강, 요리별 진단, 배운 것) |
| [`docs/DESIGN.md`](docs/DESIGN.md) | 설계 기록 전체 — 고친 19건(§4), 진단 지표 읽는 법(§6), 파일 구조(§7). 코드 주석의 `README §N`은 이 문서의 절이다 |
| [`reports/2026-09-29-final-report.md`](reports/2026-09-29-final-report.md) | 최종 보고서 |
| [`reports/2026-09-29-experiment-results.md`](reports/2026-09-29-experiment-results.md) | 보완 실험 (벌점 대조, 처음부터 학습, 실패 분석) |
| [`reports/2026-09-30-full-log.md`](reports/2026-09-30-full-log.md) | 전체 과정·시행착오·학습 시간 (런 30개, 약 25시간) |
| [`archive/`](archive/) | 런별 TensorBoard 이벤트, 설정, 모델 |

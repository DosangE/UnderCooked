# UnderCooked 최종 보고서 (2026-09-29, 09-30 보완 실험 반영)

MA-POCA로 2인 협동 요리 정책을 학습한 프로젝트의 최종 정리.
환경 설계의 세부 내용은 `docs/DESIGN.md`(요약은 `README.md`), 날짜별 분석은 아래 보고서에 있다. 이 문서는 그 결과를 한곳에 모으고
보관한 산출물의 위치를 적는다.

| 문서 | 내용 |
|---|---|
| `README.md` | 요약: 구조, 설계 이유, 학습 경로, 시행착오, 보완 실험, 실행 |
| `docs/DESIGN.md` | 설계 기록 전체: 게임 규칙, 관측·행동·보상 설계, 설계 과정에서 고친 것(§4), 결과(§6), 회고(§9). 아래의 `§4-16` 같은 절 번호는 이 문서다 |
| `reports/2026-09-25-preflight.md` | 학습 전 환경 점검 (버전 실측) |
| `reports/2026-09-25-undercooked_v1-serve-zero.md` | v1 서빙 0회 원인 분석, lesson0 진단 |
| `reports/2026-09-25-training-results.md` | 런별 경과, 실패 분석(병목 정정), final2·final3 |
| `reports/2026-09-28-submission-checklist.md` | 제출 전 남은 작업 |
| `reports/2026-09-29-experiment-results.md` | 보완 실험: 벌점 대조(B), 처음부터 학습(A), 실패 에피소드 분석, 0단계 후속 (계획은 `experiment-plan.md`) |
| `reports/2026-09-30-wrong-dish-hold.md` | 틀린 요리 보유 벌점 실험 |
| `reports/2026-09-30-full-log.md` | 전체 과정·시행착오·학습 시간 (런 30개, 약 25시간) |
| `archive/README.md` | 학습 결과 파일 보관 목록 |

![최종 정책 데모](../assets/demo.gif)

---

## 1. 결과 요약

- **최종 모델 `models/undercooked.onnx` (`undercooked_final3`)는 최종 난이도에서 3접시 목표 달성률 97.8%다.**
  학습 없이 Unity에서 추론만 돌려도 97.1%(에피소드 478개)가 나와 학습 지표와 일치한다.
- 최종 난이도: 손질 켜짐, 레시피 3종, 조리 5초, 주문 슬롯 3, 주문 제한 25초, 목표 3접시, 에피소드 45초.
- 아무것도 하지 않는 정책은 목표 달성 0%, 팀 보상 −1.5다.
- 무작위 초기화부터 기본 커리큘럼 한 번으로는 학습되지 않았다(v1). **lesson0 고정 → 커리큘럼 → 최종 난이도
  고정**의 3단계로 도달했다. 총 20M 스텝(v1 제외), 1M 스텝당 약 13분.
  마지막 런(final3)에서 주문에 없는 재료 투입 벌점을 −0.1 → −0.3으로 올렸지만, 보완 실험 B에서 마지막 향상
  (90% → 98%)은 벌점이 아니라 **3M 추가 학습의 효과**로 나왔다 (§4-4 정정).
- 97.8%는 시드 1개의 값이다. final2에서 같은 3M을 시드만 바꿔 이어 학습하면 94.7~97.3%(벌점 −0.3, 3시드)였고,
  97.8%는 그 범위의 위쪽 끝이다. 모델 자체의 성능은 같은 모델을 600k 스텝(1100판) 다시 쟀을 때도 97.5%로 확인했다
  (`archive/runs/eval_final3`).

| 지표 (최종 난이도) | 학습 (final3 2.5–3M) | Unity 추론 (478 에피소드) |
|---|---|---|
| 목표 달성 | **97.8%** | **97.1%** |
| 서빙 / 에피소드 | 2.97 | 2.96 |
| 냄비 확정 / 에피소드 | 3.26 | 3.25 |
| 주문에 없는 레시피로 채움 | 0.22 (7%) | – |
| 주문과 다른 요리 제출 | 0.26 | – |
| 주문 만료 | 1.10 | – |
| 팀 보상 (`Group Cumulative Reward`) | +13.29 | – |
| 개인 보상 (`Cumulative Reward`) | +0.46 | – |
| 에피소드 길이 (decision) | 281 | – |

Unity 추론 서빙 분포는 1접시 3 / 2접시 11 / 3접시 464로, 0접시 에피소드는 없었다. 성공 에피소드는 평균 27.7초
(25/50/75 분위 24.9 / 25.4 / 27.4초)에 끝난다.

---

## 2. 환경 요약

| 항목 | 내용 |
|---|---|
| 에이전트 | 셰프 2명 1팀 (`SimpleMultiAgentGroup`), 주방 16개 동시 학습 (셰프 32명) |
| 협동 강제 | 냄비는 A 구역, 그릇함·서빙구는 B 구역. 경계는 걸어서 넘을 수 없고 카운터 4칸으로만 주고받는다 |
| 관측 | 103차원, 전부 상대좌표. 주문판 3슬롯(레시피 + 남은 시간) 포함. 조리 완료 여부는 뺐다 |
| 행동 | Discrete 2 branch (이동 5, 상호작용 2) + Action Masking |
| 보상 | 팀: 서빙 +3, 목표 +2, 투입 +0.3·손질 +0.2(무산되면 회수), 주문 만료 −0.5. 개인: 전달 +0.15(서빙으로 안 이어지면 회수), 집기 +0.05, 주문에 없는 재료 투입 −0.3, 주문에 없는 요리 서빙 −0.5, 스텝 −0.002 |
| 고급 개념 | MA-POCA, Action Masking, Memory(LSTM, sequence 64 / memory 128), Curriculum |

상세 표는 `docs/DESIGN.md` §1~§3.

---

## 3. 학습 경로

![Kitchen/GoalReached](../assets/tb_goal_reached.png)

| run-id | 설정 | 스텝 / 시간 | 결과 |
|---|---|---|---|
| `undercooked_v1` | 기본 커리큘럼, 무작위 초기화 | 1.44M (중단) | 서빙 0회 |
| `undercooked_lesson0` | lesson0 고정 | 3M / 39분 | 1.96M 첫 서빙, 3M 99.7% |
| `undercooked_v2` | 기본 커리큘럼, `--initialize-from=undercooked_lesson0` | 8M / 1시간 39분 | 최종 난이도 54% |
| `undercooked_final` | 최종 난이도 고정, `--initialize-from=undercooked_v2` | 3M / 39분 | 74% |
| `undercooked_final2` | 같은 설정 + 주문 불일치 지표 | 3M / 40분 | 90.5% |
| `undercooked_final3` | 같은 설정 + 주문에 없는 재료 투입 벌점 −0.1 → −0.3 | 3M / 40분 | **97.8%** |

모든 런은 실행 전 보상 회귀 검사 전 항목 통과와 `학습 모드 (트레이너 연결됨, 주방 16개)` 표시를 확인하고 시작했다.

### 곡선

![Environment/Cumulative Reward](../assets/tb_cumulative_reward.png)

![Environment/Group Cumulative Reward](../assets/tb_group_reward.png)

- `Environment/Cumulative Reward`는 **개인 보상만** 담는다. 스텝 비용이 깔려 있어 최대 약 +0.46이다.
  성과는 팀 보상과 `GoalReached`로 읽는다 (`docs/DESIGN.md` §4-16).
- v2의 누적 4.2M~4.6M(v2 기준 1.2M~1.6M) 급락은 레시피 2종, 손질 전환이 이어진 구간이다. 손질이 켜지자
  냄비를 한 번도 채우지 못했다(`Kitchen/PotCommitted` 0). 약 1.5M 스텝 뒤 스스로 손질 경로를 찾아 회복했다.
- final 이후 곡선은 최종 난이도 고정이라 서로 직접 비교할 수 있다.

---

## 4. 무엇이 성패를 갈랐나

### 4-1. v1 실패: 커리큘럼이 첫 서빙보다 빨랐다

v1은 1.44M 스텝 동안 서빙 0회였고 팀 보상이 모든 요약에서 정확히 −0.500이었다. progress 커리큘럼이 1.2M에 레시피
2종, 1.6M에 손질을 켰는데, 같은 보상 체계로 lesson0만 고정해 돌려 보니 첫 서빙에 1.96M이 걸렸다. 서빙을 한 번도 본 적 없는
상태에서 난이도가 올라간 것이다. lesson0을 먼저 따로 학습하고 `--initialize-from`으로 이어 가는 방식으로 바꿨다.

### 4-2. 학습률 재시작 (원인은 가르지 못했다)

v2 마지막 1M 스텝은 48% → 54%로 거의 제자리였다(linear 학습률 감소). 같은 모델로 최종 난이도를 고정하고 학습률을
3e-4부터 다시 시작한 final은 3M 동안 +20%p 올랐다.
이 상승이 학습률 재시작 때문인지, 최종 난이도 고정 때문인지, 단순히 더 학습해서인지는 따로 가르지 않았다.
같은 재시작을 한 s2 이어 학습(`undercooked_stage_s2_ft`)은 94.5% → 93.9%로 오르지 않았다 (experiment-results §2-2b).

### 4-3. 병목 판단 정정: 속도가 아니라 주문 대조

final 시점의 에피소드 평균(냄비 확정 3.49, 서빙 2.63)만 보고 처음에는 "만드는 속도가 병목"이라고 판단했다. Unity 추론
에피소드 322개를 성공/실패로 나눠 보니 달랐다.

| | 에피소드 | 서빙 | 냄비 확정 | B까지 가고 버려진 요리 |
|---|---|---|---|---|
| 성공 | 237 | 3.00 | 3.32 | 0.36 |
| 실패 | 85 | 1.73 | **4.07** | **1.80** |

실패 85개 중 81개가 냄비를 3번 이상 채웠다. 만들 시간은 충분했고 주문과 맞지 않는 요리를 만들고 있었다.
평균 지표는 성공과 실패를 섞어서 이걸 가렸다.

### 4-4. 원인 분리 → 벌점 조정 (효과는 없었다)

> 이 절의 해석은 아래 정정(보완 실험 B)으로 바뀌었다. 표는 측정값 그대로 두고, 벌점 덕분이라고 읽은 부분을 고쳤다.

주문 불일치 지표 2종(`Kitchen/PotCommittedWrong`, `Kitchen/ServedWrongOrder`)을 넣고 final2를 돌렸다. 틀린 제출(0.48)이
잘못 채운 횟수(0.63)보다 적었다. 불일치는 대부분 **처음부터 주문에 없는 레시피로 채우는 것**이었고, 채운 뒤 주문이
만료되는 경우는 적었다. final2는 2M 이후 0.25M 구간 성공률이 88~91%에서 오르내려(90.1 / 88.0 / 89.6 / 91.3%)
정체한 것으로 보았다. 이 판단은 아래 정정에서 틀린 것으로 확인됐다.

그래서 이 한 가지만 겨냥해 주문에 없는 재료 투입 벌점을 −0.1 → −0.3으로 올렸다(코드 기본값과 프리팹 셰프 2명 값).
회귀 검사 11개 항목을 다시 통과시킨 뒤 final2에서 3M 이어 학습했다. 대조군은 없었다.

![Kitchen/PotCommittedWrong](../assets/tb_pot_committed_wrong.png)

| 구간 | 목표 달성 | 냄비 확정 | 잘못 채움 |
|---|---|---|---|
| final2 2.5–3M | 90.5% | 3.47 | 0.63 (18%) |
| final3 0.8–1.0M | 87.7% | 3.29 | 0.44 (13%) |
| final3 1.8–2.0M | 94.9% | 3.25 | 0.28 (9%) |
| final3 2.5–3.0M | **97.8%** | 3.26 | **0.22 (7%)** |

1.5M까지는 오히려 87.7%로 final2(90.5%)보다 낮았고, 냄비 확정도 3.47 → 3.29로 줄었다. 처음에는 새 벌점에 적응하는
기간이라고 해석했다. 그런데 실험 B에서는 벌점 −0.1 조건도 0.5–1.0M 구간이 88.2%로 똑같이 낮았다 (§3-2).
벌점이 아니라 이어 학습 초반의 하락일 가능성이 크다. 냄비 확정 감소가 −0.3 조건에서만 나타나는지는 B에서 비교하지 않았다.
2M 이후에는 냄비를 덜 채우고도 서빙이 늘었다.

> **정정 (2026-09-29, 보완 실험 B).** 같은 final2에서 벌점만 −0.1 / −0.3으로 두고 시드 3개씩 3M 이어 학습했다.
> 두 조건 모두 96~97%가 됐다 (−0.1 평균 96.7%, 범위 95.7–97.6 / −0.3 평균 95.8%, 범위 94.7–97.3).
> 잘못 채움도 두 조건 모두 0.63 → 0.2~0.3회로 줄었다 (−0.1 평균 0.27 / −0.3 평균 0.34).
> 범위가 겹치므로 벌점 효과라고 말할 수 없고, final2 → final3의 향상은 대부분 **같은 설정으로 3M 더 학습한 효과**다.
> "final2가 90%에서 멈췄다"는 판단도 틀렸다. final2의 0.5M 구간 성공률은 83.7% → 89.0% → 90.5%로 끝까지 오르고 있었다.
> 자세한 것은 `reports/2026-09-29-experiment-results.md` §3.

### 4-5. 학습 전에 막은 것

`docs/DESIGN.md` §4에 설계 과정에서 고친 19건이 있다. 결과에 직접 영향을 준 것:

- 카운터에 놓았다 집기 반복, 건네고 나서 버리기 같은 **보상 어뷰징 경로**. 전달 보상은 동료가 집은 순간에만 주고,
  진행·전달 보상은 무산되면 회수하도록 바꿨다. 회귀 검사 항목으로 남겼다 (§4-1, §4-12, §4-19).
- 커리큘럼 임계값(3.0 / 6.0)이 팀 보상을 담지 않는 개인 보상과 비교되어 구조적으로 통과할 수 없었다 (§4-16).
  임계값을 개인 보상 단위로 다시 잡았다.
- Play를 먼저 누르면 주방 15개가 꺼진 채 학습되는 문제 (§4-17). Play 직후 `학습 모드 (트레이너 연결됨, 주방 16개)`를
  찍게 해서 매 런 시작 때 확인했다.

---

## 5. 한계와 하지 않은 것

| 항목 | 상태 |
|---|---|
| 처음부터 재현 | 최종 모델의 체인(lesson0 → final3)은 다시 돌려 보지 않았다. 대신 현재 코드(벌점 −0.3)로 무작위 초기화부터 단계 커리큘럼 20M을 돌린 보완 실험 A s2, s4가 94.5%, 92.0%에 도달했다 (`reports/2026-09-29-experiment-results.md` §2) |
| Memory 효과 | 설정에는 쓰였지만 이 브랜치 안에서 효과를 검증하지 않았다. on/off 비교는 별도 실험(PR #17, 병합 안 함)에 있다 |
| 커리큘럼 | progress 커리큘럼은 손질 전환에서 무너진다. 보완 실험 A의 성공률 단계 커리큘럼은 시드 4개 중 2개(s2, s4)가 한 번에 최종 난이도까지 갔다 (20M, 94.5%, 92.0%, 둘 다 강제 승급 1회). s1, s3는 0단계에서 서빙을 배우지 못했다. 단계표의 5단계(주문 슬롯 1, 레시피 3)가 최종 난이도보다 어려웠다. 0단계 실패는 벌점 탓이 아니었다(0단계에서는 벌점이 발생하지 않아 벌점 −0.1로 같은 시드를 돌리면 비트 단위로 같았다). `beta`를 3배로 키워도 풀리지 않았다. 시드가 정한다 (`reports/2026-09-29-experiment-results.md` §2-4, §2-5) |
| 시드 | 최종 체인의 런은 모두 시드 1개다. 보완 실험 B(final2에서 3M, 시드 3개씩)에서 같은 조건의 시드 간 편차는 목표 달성 약 2%p였다 |
| 주문 관측 ablation | 하지 않음 |
| 조리 완료를 관측에 넣은 진단 조건 | 하지 않음 |
| 틀린 요리 보유 벌점 (09-30, PR #28) | 시험함. 틀린 요리를 버리는 횟수는 늘었지만(0.46 → 0.73) 성공률은 오르지 않았다 (추론 95.5% → 94.3%, 오차 범위 안, 런 1개). 코드는 yaml로 켜는 방식(기본값 꺼짐)으로 남겼고 최종 모델과 기존 설정은 그대로다 (`reports/2026-09-30-wrong-dish-hold.md`) |
| 남은 약점 | 주문에 없는 레시피로 채우기 7%. 벌점 −0.1과 −0.3의 차이가 없었으므로(§4-4 정정) 벌점을 더 올려야 한다는 근거는 없다. 한 번에 학습한 모델에서도 판의 약 22%가 레시피를 틀리게 고르고, 보유 벌점은 이 비율을 건드리지 못했다. 레시피 선택 정확도를 직접 겨냥하는 것이 다음 후보다 |

---

## 6. 재현

### 학습

`README.md` §8(= `docs/DESIGN.md` §5)의 순서를 그대로 따른다. 각 런은 이전 런의 `results/<run-id>/Chef/checkpoint.pt`에서 시작한다.

```bash
mlagents-learn configs/undercooked_lesson0.yaml --run-id=undercooked_lesson0 --torch-device cuda
mlagents-learn configs/undercooked.yaml       --run-id=undercooked_v2     --initialize-from=undercooked_lesson0 --torch-device cuda
mlagents-learn configs/undercooked_final_pen01.yaml --run-id=undercooked_final  --initialize-from=undercooked_v2     --torch-device cuda
mlagents-learn configs/undercooked_final_pen01.yaml --run-id=undercooked_final2 --initialize-from=undercooked_final  --torch-device cuda
mlagents-learn configs/undercooked_final_pen03.yaml --run-id=undercooked_final3 --initialize-from=undercooked_final2 --torch-device cuda
```

잘못된 재료 투입 벌점은 final2까지 −0.1, final3부터 −0.3으로 학습했다. 코드 기본값은 −0.3이므로
앞의 네 런은 yaml에 `wrong_ingredient_penalty: -0.1`이 들어간 설정으로 돌린다
(`undercooked_lesson0.yaml`, `undercooked.yaml`, `undercooked_final_pen01.yaml`).
`undercooked_final_pen01/03.yaml`은 `undercooked_final.yaml`에 이 한 줄만 더한 것이다.
Play 후 콘솔에 `[KitchenEnv] wrong_ingredient_penalty = ...`가 찍히는지 확인한다.

실행 전 체크리스트는 `.claude/docs/TRAINING.md` §2. 체크포인트는 보관하지 않았으므로, 중간 런부터 이어 가려면
해당 런까지 다시 학습해야 한다.

처음부터 한 번에 학습하는 방식(보완 실험 A, 단계 커리큘럼)은 다음 명령이다. 20M, 약 4시간 15분이다.
시드에 따라 0단계에서 서빙을 못 찾을 수 있다 (시드 1~4 중 2, 4만 성공). 콘솔에 `[StageCurriculum] 켜짐...`이 찍히는지 확인한다.

```bash
mlagents-learn configs/undercooked_stage.yaml --run-id=undercooked_stage_s2 --seed=2 --torch-device cuda
```

### 추론 확인

1. 셰프 32명의 Behavior Parameters > Model에 `models/undercooked.onnx`를 넣는다 (Behavior Type `Default`).
2. `KitchenEnv`의 `defaultTargetDishes`를 3, `defaultOrderDuration`을 25로 바꾼다.
   **트레이너 없는 Play의 기본값(2 / 20초)은 최종 난이도가 아니다.**
3. 트레이너 없이 Play. 주방 16개가 모두 모델로 움직인다.
4. 확인 후 씬을 저장하지 않고 다시 불러온다.

자동화한 절차는 `archive/tools/inference/`와 TRAINING.md에 있다.

### 그래프·GIF 다시 만들기

```bash
python archive/tools/figures/export.py scalars.csv archive/runs   # mlagents 환경 (tensorboard 필요)
python archive/tools/figures/plot.py scalars.csv                  # matplotlib 환경. assets/tb_*.png
```

GIF는 Play 중 `figures/cam.cs` → `rec.cs`로 프레임을 녹화하고 `gif2.py`로 만든다. 녹화한 에피소드는
주방 `TrainingArea_09`의 한 에피소드(24.2초, 3접시)다. 3번째 서빙과 에피소드 리셋이 같은 틱에 일어나서,
마지막 프레임은 직전 화면에 "서빙 3/3"을 표시했다.

---

## 7. 산출물

| 경로 | 내용 |
|---|---|
| `models/undercooked.onnx` | 최종 모델 (`undercooked_final3`) |
| `archive/runs/<run-id>/` | 런 26개(최종 체인 6개 + 보완 실험·추론 평가 20개)의 TensorBoard 이벤트, 설정, 런 종료 모델, 실행 기록. 트레이너 콘솔 로그는 `.gitignore`의 `*.log` 때문에 저장소에 없다 (단계 승급 기록은 `stage_log.txt`) |
| `archive/tools/` | 지표 요약, 에피소드 분석, Unity 추론 확인, 그래프·GIF 생성 스크립트 |
| `assets/tb_*.png` | 학습 곡선 4장 |
| `assets/demo.gif` | 최종 정책 데모 |
| `configs/` | 학습 설정 9종 (ASCII 전용). 체인 재현에는 §6의 4종(`undercooked`, `_lesson0`, `_final_pen01`, `_final_pen03`)을 쓴다. 나머지 5종은 `_final`(벌점 줄이 없는 원본)과 보완 실험용(`_stage`, `_stage_beta03`, `_stage_pen01`, `_final_hold`)이다 |
| `UnityProject/` | 환경 (씬 `Assets/Scenes/UnderCooked.unity`) |

## 8. 환경

데스크탑 RTX 2080 SUPER. Unity 6000.3.18f1, URP 17.3.0, `com.unity.ml-agents` 4.0.3 (통신 API 1.5.0).
Python 3.10.12, `mlagents` / `mlagents-envs` 1.2.0.dev0 (소스 설치), torch 2.2.2+cu121, numpy 1.23.5, protobuf 3.20.3.

보완 실험(09-29~)은 Unity 6000.3.25f1에서 돌렸다 (에디터를 올리면서 바뀐 것이고 `ProjectVersion.txt`는 커밋하지 않았다).
환경이 예전과 같은지는 final2 모델을 학습 없이 다시 돌려 확인했다 (추론 92.0%, 학습 당시 90.5%).

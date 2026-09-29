# 보완 실험 계획 (2026-09-29)

최종 보고서(`reports/2026-09-29-final-report.md` §5)의 한계 세 가지를 보완한다.

| 한계 | 보완하는 실험 |
|---|---|
| 한 번에 끝까지 학습되는 커리큘럼이 없다 (최종 모델은 런 5개를 이은 것) | A |
| 현재 코드(벌점 −0.3)로 처음부터 학습해 본 적이 없다 | A |
| 모든 런이 한 번씩만 학습됐다 (시드 1개). 벌점 효과(90% → 98%)에 대조군이 없다 | A, B 모두 시드 3개 |

코드와 설정은 브랜치 `dev_exp-stage-curriculum`에 있다. 학습은 데스크탑에서 한다.

---

## 실험 A: 성공률 단계 커리큘럼으로 처음부터 한 번에

**설정:** `configs/undercooked_stage.yaml`, 무작위 초기화, 20M 스텝 (기존 체인 합계와 같은 예산).
벌점은 코드 기본값 −0.3을 그대로 쓴다.

**무엇이 달라졌나.** 기존 커리큘럼은 스텝 수(`progress`)로 난이도를 올려서, 배우기 전에 올라갔다
(v1 서빙 0회, v2 손질 전환 붕괴). ML-Agents의 `measure: reward`는 개인 보상만 보는데, 그 값이
같은 성공률에서도 런·단계마다 달라서 기준으로 쓸 수 없었다.

| 최종 난이도 성공률 | 개인 보상 (`Environment/Cumulative Reward`) |
|---|---|
| 70~80% | final −0.07 / final2 +0.02 |
| 80~90% | final +0.02 / final2 +0.17 / final3 +0.23 |

그래서 **최근 500판 성공률(`Kitchen/GoalReached`)이 80% 이상이면 다음 단계**로 올린다.
판정은 환경(`StageCurriculum.cs`)이 하고, 켜기·임계값·창 크기는 yaml로 받는다.

| 단계 | 목표 접시 | 레시피 | 손질 | 조리 | 주문 슬롯 |
|---|---|---|---|---|---|
| 0 | 1 | 1 | 꺼짐 | 2초 | 1 |
| 1 | 3 | 1 | 꺼짐 | 2초 | 1 |
| 2 | 3 | 2 | 꺼짐 | 2초 | 1 |
| 3 | 3 | 2 | **에피소드 50%** | 2초 | 1 |
| 4 | 3 | 2 | 켜짐 | 2초 | 1 |
| 5 | 3 | 3 | 켜짐 | 5초 | 1 |
| 6 | 3 | 3 | 켜짐 | 5초 | 2 |
| 7 | 3 | 3 | 켜짐 | 5초 | 3 | ← 최종 난이도 (`undercooked_final.yaml`과 같음)

- 3단계는 v2가 무너진 손질 전환을 둘로 나눈 것이다.
- 한 단계에서 6000판을 넘기면 강제로 올린다 (밤새 멈춰 있지 않게). 강제 승급은 콘솔에 ★로 찍히고, 보고할 때 밝힌다.

**성공 기준**

- 3개 시드 중 몇 개가 7단계에 도달했나, 도달 스텝
- 20M 시점 최종 난이도 성공률 (마지막 0.5M 평균). 기존 체인 결과 97.8%와 비교
- 강제 승급이 있었나

## 실험 B: 벌점 −0.1 vs −0.3 대조

**질문:** final2(90%) → final3(98%)는 벌점을 올린 것과 3M을 더 학습한 것이 섞여 있다. 둘을 분리한다.

**설정:** 같은 체크포인트에서 3M, 벌점만 다르다. 각 시드 3개, 총 6런.

| 조건 | 설정 | 벌점 |
|---|---|---|
| 대조군 | `configs/undercooked_final_pen01.yaml` | −0.1 (예전 값) |
| 처리군 | `configs/undercooked_final_pen03.yaml` | −0.3 (현재 값) |

벌점은 yaml의 `wrong_ingredient_penalty`로 넣는다. 코드는 그대로다.

**출발점**

- 데스크탑 `results/undercooked_final2/Chef/checkpoint.pt`가 있으면 그것 (`--initialize-from=undercooked_final2`). 원래 질문에 정확히 답한다.
- 없으면 실험 A가 끝난 런에서 출발한다. 이 경우 질문이 "−0.3으로 배운 정책에서 벌점을 낮추면 나빠지나"로 바뀐다. 보고할 때 그렇게 한정한다.

**성공 기준:** 두 조건의 마지막 0.5M `Kitchen/GoalReached`와 `Kitchen/PotCommittedWrong` 평균·범위. 범위가 겹치지 않으면 벌점 효과라고 말할 수 있다.

---

## 집에서 할 일 (순서대로)

### 1. 코드 받기와 확인

```bash
git fetch origin
git switch dev_exp-stage-curriculum
git pull --ff-only
```

Unity에서 `UnityProject`를 열고 컴파일 에러가 없는지 본다. 새 파일은 `Assets/Scripts/StageCurriculum.cs`다.

Play → `UnderCooked/보상 회귀 검사` (`Ctrl+Shift+T`). **13개 항목 전부 OK**여야 한다.
새 항목은 두 개다.

- `[12] 단계 커리큘럼`: 창이 차야 승급, 이전 단계 판 무시, 80% 경계, 판 수 상한, 실제 종료 경로, 최종 단계 표, 손질 섞임
- `[13] 잘못된 재료 투입 벌점`: yaml이 없으면 −0.30, yaml −0.1이면 −0.10

확인 뒤 Play를 끈다.

### 2. 체크포인트 확인

`UnderCooked\results\undercooked_final2\Chef\` 안에 `checkpoint.pt`가 있는지 본다. 실험 B의 출발점이 여기서 정해진다.

### 3. 학습

각 줄마다 트레이너를 먼저 띄우고 `Listening on port 5004`가 나오면 Play (`.claude/docs/TRAINING.md` §2).
콘솔에 `[StageCurriculum] 켜짐...` 또는 `[KitchenEnv] wrong_ingredient_penalty = ...`가 나오는지 확인한다.

```bash
# 실험 A (각 약 4.3시간)
mlagents-learn configs/undercooked_stage.yaml --run-id=undercooked_stage_s1 --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_stage.yaml --run-id=undercooked_stage_s2 --seed=2 --torch-device cuda
mlagents-learn configs/undercooked_stage.yaml --run-id=undercooked_stage_s3 --seed=3 --torch-device cuda

# 실험 B (각 약 40분). START = undercooked_final2 또는 undercooked_stage_s1
mlagents-learn configs/undercooked_final_pen01.yaml --run-id=pen01_s1 --initialize-from=START --seed=1 --torch-device cuda
mlagents-learn configs/undercooked_final_pen03.yaml --run-id=pen03_s1 --initialize-from=START --seed=1 --torch-device cuda
# s2, s3도 같은 방식 (--seed=2, --seed=3)
```

권장 순서: 첫날 밤 A s1 → 결과 확인 → B 6런 → A s2, s3.
A s1이 초반 단계에서 오래 멈춰 있으면 나머지를 돌리기 전에 원인을 먼저 본다.

**TensorBoard에서 볼 것 (실험 A):** `Kitchen/Stage`(계단), `Kitchen/StageSuccessRate`, `Kitchen/GoalReached`, `Kitchen/PotCommitted`.
승급 직후 성공률이 떨어졌다가 다시 오르는 것이 정상이다. 한 단계에서 `PotCommitted`가 0에 붙어 있으면 v2와 같은 붕괴다.

### 4. 결과 넘기기

`results/<run-id>/Chef/events.out.tfevents.*`와 `configuration.yaml`, 콘솔 로그를 `archive/runs/<run-id>/`에 보관하면
노트북에서 분석하고 README·최종 보고서를 갱신한다.

---

## 확인한 것 / 아직 확인 안 한 것

- 노트북에서 Unity 6000.3.19f1의 Roslyn 컴파일러로 `Assets/Scripts/*.cs`를 프로젝트 어셈블리에 대고 컴파일 → 에러 0건.
  (일부러 틀린 코드를 넣으면 에러가 나는 것도 확인했다.)
- ML-Agents 설정 파서(`RunOptions.from_dict`, POCA 플러그인 등록)로 새 yaml 3개 파싱 통과. ASCII 전용 확인.
- 새 yaml과 기존 yaml의 차이는 의도한 줄뿐이다 (`max_steps`, `environment_parameters`).
- **아직 안 한 것:** Unity 에디터에서의 컴파일과 회귀 검사 실행 (UnityMCP 미연결), 학습.
- 트레이너 파라미터는 주방의 첫 `ResetEnv`보다 늦게 도착할 수 있다. 그래서 `stage_curriculum`은 켜질 때까지
  매 에피소드 다시 읽는다. 처음 몇 판은 기존 경로로 돌고 판정에서 빠진다.

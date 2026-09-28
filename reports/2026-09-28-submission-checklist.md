# 제출 전 남은 작업 (2026-09-28)

학습은 끝났다. 최종 모델 `models/undercooked.onnx`(`undercooked_final3`)는 최종 난이도에서
목표 달성률 97.8%이고, Unity 추론에서도 97.1%(에피소드 478개)가 나왔다.
과제 필수 조건(`최종_프로젝트.md` §1) 가운데 **학습을 더 돌려야 하는 항목은 없다.**
남은 것은 첨부물, 문서 수정, 제출이다.

마감: 9월 말.

---

## 0. 요구사항 대비 현황

| # | 과제 조건 | 상태 | 남은 것 |
|---|---|---|---|
| 1 | 환경을 직접 제작 | 완료 | – |
| 2 | 고급 개념 사용 | 완료 (MA-POCA, Action Masking, Memory, Curriculum) | – |
| 3 | 수렴을 TensorBoard 그래프로 증명 | 수치 표만 있음 | **그래프 이미지** (§1) |
| 4 | `.onnx`로 추론 | 완료 (97.1%) | – |
| 5 | README (관측/행동/보상 표, 결과, 시행착오) | 대부분 완료 | **버전·실행 명령·그래프·GIF** (§3) |
| 6 | Git 규칙 (fork 브랜치, PR, 리뷰) | 제출 사본이 9/10 상태 | **동기화, PR, 리뷰 1건** (§4) |
| – | 결과물: 데모 GIF | 없음 (`assets/`에 `.gitkeep`만 있음) | **GIF** (§2) |

---

## 1. TensorBoard 그래프 캡처 (데스크탑)

이벤트 파일은 데스크탑의 `UnderCooked/results/`에만 있다. 노트북에는 없다.

```bash
conda activate mlagents
cd UnderCooked
tensorboard --logdir results
```

- [x] **`Environment/Cumulative Reward`** — 과제가 수렴 증명으로 지정한 스칼라. 필수
- [x] `Kitchen/GoalReached` — 실제 성공률. 3번 그래프를 읽을 수 있게 해 준다
- [x] `Environment/Group Cumulative Reward` — 팀 보상 (실질적인 성과 곡선)
- [x] (선택) `Kitchen/PotCommittedWrong` — final2 → final3에서 벌점 조정의 효과

캡처 방법:

- 런 필터에서 `undercooked_v2`, `undercooked_final`, `undercooked_final2`, `undercooked_final3`를 켠다.
  `undercooked_v1`(실패)과 `undercooked_lesson0`은 시행착오 설명용으로 따로 캡처해도 된다.
- 각 런의 스텝은 0부터 다시 시작해서 곡선이 겹쳐 보인다. 런별로 따로 캡처하거나
  한 장에 겹친 뒤 범례로 구분한다.
- 저장 위치: `assets/tb_cumulative_reward.png`, `assets/tb_goal_reached.png`,
  `assets/tb_group_reward.png` (파일명은 README에서 참조하므로 맞춘다)
- 2026-09-29: TensorBoard 화면 캡처 대신 이벤트 파일 값을 그대로 읽어 matplotlib으로 그렸다. 런을 학습 순서대로 이어 붙여 누적 스텝으로 표시. `assets/tb_pot_committed_wrong.png`도 추가

**그래프에 붙일 설명 (그래프를 잘못 읽지 않도록).** `Environment/Cumulative Reward`는
**개인 보상(`AddReward`)만** 담는다. 매 스텝 −0.002가 깔려 있어서 최대로 올라가도 +0.46이다.
서빙 +3, 목표 +2 같은 팀 보상은 `Group Cumulative Reward` 쪽에 찍힌다. 그래서 성과는
`GoalReached`로 함께 본다는 한 줄을 그래프 옆에 둔다. 근거는 README §4-16, §6.

참고 수치 (각 런 마지막 0.5M 평균):

| 런 | Cumulative Reward | Group Cumulative Reward | GoalReached |
|---|---|---|---|
| 랜덤 기준선 | 약 −0.9 | −1.5 (최종 난이도) | 0 |
| v2 | −0.35 | +8.83 | 0.54 |
| final | −0.08 | +10.74 | 0.74 |
| final2 | +0.29 | +12.60 | 0.905 |
| final3 | +0.46 | +13.29 | 0.978 |

캡처가 이 수치와 크게 다르면 런을 잘못 골랐는지 먼저 확인한다.

---

## 2. 데모 GIF

- [x] `assets/demo.gif` (README §7 파일 구조에 이미 이 경로로 적혀 있다)

**주의: 트레이너 없이 Play하면 최종 난이도가 아니다.** `KitchenEnv`의 기본값은 학습의
최종 값과 다르다.

| 파라미터 | 프리팹 기본값 | 최종 학습 값 |
|---|---|---|
| `defaultTargetDishes` | 2 | **3** |
| `defaultOrderDuration` | 20 | **25** |
| 나머지 (손질, 조리 5초, 슬롯 3, 레시피 3종, 재료 상한 2) | 같음 | 같음 |

순서:

1. `TrainingArea` 프리팹 셰프의 Behavior Parameters > Model에 `models/undercooked.onnx`를
   넣고 Behavior Type은 `Default`로 둔다. 추론 성적(97.1%)을 잴 때와 같은 방법이다.
2. `KitchenEnv`의 위 두 값을 3 / 25로 바꾼다.
   - 임시로 바꿨다면 녹화 후 씬을 저장하지 않고 다시 불러온다.
   - 아예 3 / 25로 고정하고 커밋해도 된다. 그러면 트레이너 없이 Play할 때 항상 최종 난이도가 된다.
     이렇게 하면 사람 플레이도 3접시 목표로 바뀐다는 점에 주의한다.
3. 트레이너 없이 Play한다. 주방 16개가 모두 모델로 움직인다. 카메라를 주방 하나로 좁혀서
   10~30초 분량(3접시 서빙까지)을 녹화한다.
4. GIF로 변환한다. PR 본문에도 첨부하므로 용량을 몇 MB 이하로 줄인다.
   ```bash
   ffmpeg -i demo.mp4 -vf "fps=12,scale=640:-1:flags=lanczos" -loop 0 assets/demo.gif
   ```

---

## 3. README / 문서 수정

학습 환경을 바꿀 필요는 없다. 문서가 예전 계획에 머물러 있으므로 **문서를 실제 값으로 고친다.**

- [x] **§8 환경 버전**을 실제 학습 환경으로 고친다 (`reports/2026-09-25-preflight.md` 실측).

  | 항목 | 현재 문서 | 실제 학습 |
  |---|---|---|
  | Unity | 6000.3.19f1 | **6000.3.18f1** |
  | Python `mlagents` | 1.1.0 | **1.2.0.dev0** (`C:/Users/User/ml-agents` 소스 설치) |
  | PyTorch | 2.2.2+cpu (CPU 학습) | **2.2.2+cu121** (RTX 2080 SUPER) |
  | Python / numpy / protobuf | 3.10.12 / – / – | 3.10.12 / 1.23.5 / 3.20.3 |
  | Unity ML-Agents 패키지 | 4.0.3 | 4.0.3 (같음) |

  `.claude/docs/TRAINING.md` §1과 `configs/undercooked.yaml` 주석의 "installed mlagents 1.1.0"도
  같은 기준으로 맞춘다. yaml은 ASCII 전용이다.

- [x] **§5 실행** — 지금은 실패한 `undercooked_v1` 명령만 있다. 최종 모델을 재현하는 순서로 바꾼다.
  ```bash
  mlagents-learn configs/undercooked_lesson0.yaml --run-id=undercooked_lesson0 --torch-device cuda
  mlagents-learn configs/undercooked.yaml       --run-id=undercooked_v2     --initialize-from=undercooked_lesson0 --torch-device cuda
  mlagents-learn configs/undercooked_final.yaml --run-id=undercooked_final  --initialize-from=undercooked_v2       --torch-device cuda
  mlagents-learn configs/undercooked_final.yaml --run-id=undercooked_final2 --initialize-from=undercooked_final    --torch-device cuda
  mlagents-learn configs/undercooked_final.yaml --run-id=undercooked_final3 --initialize-from=undercooked_final2   --torch-device cuda
  ```
  주의: `undercooked_final3` 전까지는 `rewardPotWrongIngredient`가 −0.1이었다. 현재 코드는 −0.3이다.
  현재 코드로 처음부터 이 순서를 다시 돌려 본 적은 없다. 이 점을 명령 아래에 한 줄 적는다.

- [x] **§6 결과** — §1의 그래프 이미지를 넣는다. 체크리스트의 `학습 곡선 (TensorBoard) — 위 표`를
  이미지 링크로 바꾼다. `최종 정책 데모 GIF` 항목을 체크한다.
- [x] **상단 "개요 / 데모"** — 과제 README 양식은 맨 위에 GIF 1장과 한 문단을 요구한다. 제목 아래에 GIF를 넣는다.
- [x] **회고** 절 — 과제 README 양식에 있는데 현재 README에는 없다. 짧게 추가한다.
  재료: v1 실패 → lesson0 분리, progress 커리큘럼의 손질 붕괴, 병목 판단 정정(속도 → 주문 대조), 벌점 조정.
- [x] 미완료로 남길 항목은 "하지 않음"으로 분명히 적는다. 과제 필수는 아니다.
  - memory on/off ablation
  - 조리 완료를 관측에 넣은 진단 조건
  - 주문 관측 ablation

---

## 4. 제출 (Study 저장소)

제출 사본: `C:\Users\User\Desktop\PCUBE\Study\2026-PCUBE-RL-Study\week7-8\UnderCooked-Hyeongjun\`,
브랜치 `final-hyeongjun-undercooked`. 2026-09-28 확인 결과 마지막 커밋이
**9/10 `363014e`**(README 작성)이고, `models/`에 `.onnx`가 없다.

- [ ] §1~§3을 끝낸 뒤 이 저장소를 `dev` → `main`으로 병합한다. 제출 기준은 `main`이다.
- [ ] 제출 사본을 동기화한다. 과제 구조는 `README.md`, `UnityProject/`, `configs/`, `models/`, `assets/`이다.
  - `UnityProject/`는 `Assets/`, `Packages/`, `ProjectSettings/`만 복사한다. `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 제외한다.
  - `reports/`와 `tools/`는 README에서 참조하므로 함께 넣는다.
  - `.onnx`와 `.gif`가 Study 저장소의 LFS 추적 대상인지 `.gitattributes`로 확인한다.
- [ ] 동기화는 작업 단위로 나눠 커밋한다. 과제 규칙: "2주치 한 번에 커밋" 금지.
  예: 환경 코드 → 학습 config → 결과·모델 → README.
- [ ] fork(`origin`)에 push한 뒤 upstream(`pandora-cube/2026-PCUBE-RL-Study`) `main`으로 PR을 연다.
  - 제목: `[Final] UnderCooked - <이름>`
  - 본문: 데모 GIF, 학습 그래프(`Environment/Cumulative Reward` 필수), 최종 성적 한 줄
- [ ] 다른 사람 PR 최소 1개에 리뷰 코멘트를 남긴다.

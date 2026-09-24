# 학습 실행

저장소의 학습 설정과 다른 PC에서 필요한 **환경 설치·검증·실행 절차**를 다룬다.
Python 환경과 GPU 드라이버는 git pull로 설치되지 않는다.
왜 그렇게 설계했는지는 `README.md`에 있다.

---

## 0. 학습할 코드와 Unity 프로젝트 확인

학습에 쓸 코드는 `dev`에 있다.

```bash
git fetch origin
git switch dev
git pull --ff-only origin dev
git log -1 --oneline
```

추가 수정이 있다면 그 수정도 커밋·push되어 있어야 학습 PC에서 받을 수 있다.

Unity Hub에서 저장소 루트가 아닌 **`UnderCooked/UnityProject`**를 연다.
Unity **6000.3.19f1**로 패키지 복원과 컴파일을 마친 뒤
**`Assets/Scenes/UnderCooked.unity`**를 연다. Console에 컴파일 에러가 없어야 한다.
`Packages/manifest.json`과 `packages-lock.json`은 함께 유지한다.

## 1. 파이썬 환경 만들기

학습은 노트북이 아니라 **데스크탑(RTX 2080 Super)** 에서 돌린다. 저장소에는 파이썬
환경이 들어 있지 않으므로 그 PC에서 새로 만든다.

```bash
conda create -n mlagents python=3.10.12 -y
conda activate mlagents
```

`mlagents==1.1.0`의 Python 허용 범위는 **3.10.1 이상, 3.10.12 이하**다.
`python=3.10`만 지정하면 범위를 넘는 패치 버전이 설치될 수 있다.
[공식 설치 안내](https://unity-technologies.github.io/ml-agents/Installation/) 참조.

ML-Agents는 `torch>=2.1.1`만 요구하며 CPU/CUDA 빌드나 상한 버전을 고정하지 않는다.
검증한 2.2.2의 CUDA 빌드를 먼저 설치한 뒤 ML-Agents를 설치한다.
이미 CPU 빌드가 있어도 교체되도록 **`+cu121`까지 명시**한다.
`torch==2.2.2`만 지정하면 기존 `2.2.2+cpu`가 요구사항을 만족해 교체되지 않을 수 있다.
[PyTorch 공식 이전 버전 안내](https://docs.pytorch.org/get-started/previous-versions/) 참조.

```bash
python -m pip install torch==2.2.2+cu121 --index-url https://download.pytorch.org/whl/cu121
python -m pip install mlagents==1.1.0
python -m pip check
```

확인:

```bash
python -c "import sys, torch, mlagents_envs; print(sys.version); print(torch.__version__, torch.cuda.is_available()); assert torch.cuda.is_available(); print(torch.cuda.get_device_name(0)); print((torch.ones(1, device='cuda') + 1).item())"
# Python 3.10.12, 2.2.2+cu121 True, GPU 이름, 2.0을 확인한다.
```

CUDA 확인에 실패하면 `nvidia-smi`로 NVIDIA 드라이버와 GPU 인식을 먼저 확인한다.

> 이 저장소를 만든 노트북 환경은 `torch 2.2.2+cpu` / Python 3.10.12 / numpy 1.23.5다.
> Unity 쪽은 Unity 6000.3.19f1 + `com.unity.ml-agents` 4.0.3.

---

## 2. 실행 전 체크리스트

**순서가 중요하다. 트레이너를 먼저 띄우고 새 Play 세션에서 연결한다.**

1. **트레이너 없이 회귀 검사를 먼저 실행한다.**
   Unity에서 Play → 메뉴 `UnderCooked/보상 회귀 검사` (`Ctrl+Shift+T`).
   [1]~[9]와 [5b], 출력 10개 항목이 전부 OK인지 확인한 뒤 **Play를 종료한다.**
   검사는 실제 행동·보상·주문·타이머를 변경한다. 본 학습에 연결한 채 실행하면
   인위적인 전이가 학습 데이터와 통계에 섞인다.

2. **Play가 꺼진 상태에서 트레이너를 띄운다.**
   ```bash
   conda activate mlagents
   cd UnderCooked
   mlagents-learn configs/undercooked.yaml --run-id=undercooked_v1 --torch-device cuda
   ```
   `Listening on port 5004` 가 뜰 때까지 기다린다.

3. **그 다음** Unity 에디터에서 Play를 누른다.

   > Play를 먼저 누르면 트레이너가 없어서 `BehaviorParameters.IsInHeuristicMode()`가
   > true가 되고, `KitchenEnv`가 그걸 사람 플레이로 읽어 **주방 15개를 꺼버린다.**
   > 같은 Play 세션에서는 연결을 재시도하지 않는다. Play를 종료하고 트레이너를 먼저 띄운 뒤 다시 Play해야 한다.

4. **콘솔 두 줄을 확인한다.**
   ```
   [StartupValidator] 씬-코드 일치 확인 (32명). 관측 103 / 행동 [5, 2] / 에피소드 45s
   [StartupValidator] 학습 모드 (트레이너 연결됨, 주방 16개)
   ```
   - 첫 줄이 에러로 바뀌면 Play가 자동으로 멈춘다. 그대로 학습하면 안 된다.
   - 둘째 줄이 `사람 플레이 모드`거나 주방이 16개가 아니면 **Play를 종료하고 2번부터 다시 한다.**

5. 이어서 학습하려면 `--resume`, 같은 run-id로 처음부터 다시 하려면 `--force`.

TensorBoard는 별도 터미널에서:

```bash
tensorboard --logdir results
```

---

## 3. TensorBoard 읽는 법

**어느 스칼라가 무엇인지 반드시 구분해야 한다.** 세 값의 단위가 서로 다르다.

| 스칼라 | 무엇이 들어가는가 |
|---|---|
| `Environment/Cumulative Reward` | **개인 보상(`AddReward`)만.** 정상 학습이어도 대략 −0.9 ~ +0.3 사이다. **커리큘럼 임계값이 비교하는 값이 바로 이것** |
| `Environment/Group Cumulative Reward` | **팀 보상(`AddGroupReward`)만.** 서빙 +3.0, 목표 +2.0, 진행 보상과 회수가 여기 찍힌다. 실질적인 성과 곡선 |
| `Policy/Extrinsic Reward` | 본인 개인 + 동료 개인 + 팀. **실제로 최적화되는 값** |

`Environment/Cumulative Reward`가 낮게 깔려 있다고 학습이 안 되는 게 아니다.
개인 보상에는 매 스텝 −0.002가 붙어 있어서 450 decision짜리 에피소드는 그것만으로
−0.9다. 성과는 `Group Cumulative Reward`로 본다.

**랜덤 정책(lesson0) 기준선** — 2026-09-24 스모크 런 실측 (PR #8 병합 후 `dev` `604eba7`,
80k 스텝, 20k마다 4회 요약):

| 스칼라 | 값 |
|---|---|
| `Environment/Cumulative Reward` | **−0.84 ~ −0.91** (−0.880 / −0.908 / −0.888 / −0.839) |
| `Environment/Group Cumulative Reward` | **−0.50** (4회 모두) |
| `Policy/Extrinsic Reward` | **−2.18 ~ −2.32** (−2.260 / −2.316 / −2.277 / −2.178) |
| `Environment/Episode Length` | **449 ~ 450** |

학습이 시작되면 이 값들보다 올라가야 한다.

> 예전 문서의 `−0.78 / −2.06`은 **README §4-19 수정 전** 측정값이다. 그때는 랜덤 행동으로
> 우연히 받은 전달 보상이 에피소드 끝까지 남았다. 지금은 서빙으로 이어지지 않은 전달 보상이
> 종료 정산 때 회수되므로 개인 기준선이 조금 더 낮다. lesson0 임계값 −0.3과의 간격은
> 오히려 넓어졌다.

`Policy/Extrinsic Reward`는 앞의 둘의 합이 아니다. POCA는 `add_groupmate_rewards = True`라
**동료의 개인 보상까지 더한다** (위 요약 네 번 모두 `Extrinsic = 2 × Cumulative + Group`,
예: −2.260 = 2 × (−0.880) + (−0.50)). 즉 개인 보상도 최적화 관점에서는 팀이 같이 진다.

환경 쪽 진단 지표(`KitchenGroup.RecordStats`)는 `README.md` §6 표를 본다.
서빙이 0인데 보상이 오르면 `Kitchen/ServesPerTransfer`, `Kitchen/CreditClawedBack`,
`Kitchen/TransferClawedBack`을 먼저 본다.
서빙이 0이면 체인 지표 `Kitchen/PotCommitted` → `PlatesToChefA` → `DishesTaken` →
`DishesToChefB` 순으로 보고, 처음 0으로 떨어지는 고리가 막힌 곳이다.

### `order_slots`가 늘면 팀 보상 기준선이 계단식으로 내려간다 (오독 주의)

주문 제한 시간이 25초, 에피소드가 45초다. 그래서 **슬롯 하나당 아무것도 안 해도
에피소드당 정확히 1건이 만료된다** (t=25에 만료되고, 리필분은 t=50이라 안 온다).
기준선 `Group Cumulative Reward −0.50`이 바로 이것이다.

즉 `order_slots`가 1→2→3으로 가는 4.0M / 5.2M 지점에서 **팀 보상 기준선이 실력과
무관하게 −0.5, −1.0씩 내려앉는다.**

| order_slots | 아무것도 안 했을 때의 팀 보상 |
|---|---|
| 1 | −0.5 |
| 2 | −1.0 |
| 3 | −1.5 |

커리큘럼 전환 직후 곡선이 떨어지는 것은 **정상이다.** 성능이 나빠진 게 아니라
기준선이 내려간 것이다. 실제 성능은 `Kitchen/DishesServed`와 `Kitchen/GoalReached`로
본다. 판단하려면 전환 전후를 비교하지 말고, **전환 후 곡선이 새 기준선에서 다시
올라가는지**를 본다.

(`target_dishes` 임계값은 **개인** 보상 기준이고 만료 패널티는 팀 쪽이므로,
이 계단은 커리큘럼 전환에는 영향을 주지 않는다.)

---

## 4. 커리큘럼 임계값 보정

`target_dishes`만 `measure: reward` 기준이고, 그 임계값은 **개인 보상 스케일**로
잡혀 있다(`configs/undercooked.yaml`의 주석에 근거가 있다). 계산으로 잡은 추정치이므로
실제 곡선을 보고 한 번 보정한다.

- **500k 스텝까지 lesson0이 안 넘어가면** — `Environment/Cumulative Reward_hist` 히스토그램과
  `Kitchen/GoalReached`, `Kitchen/DishesServed`를 함께 확인한다. 성공률이 충분한데도
  전환되지 않을 때만 임계값을 보정한다. 히스토그램은 성공/실패를 합친 분포이므로
  높은 보상 쪽 봉우리가 반드시 성공 에피소드라고 단정하지 않는다.
- **너무 빨리 넘어가면** (서빙이 안 되는데 lesson1로 갔으면) — `Kitchen/DishesServed`가
  1에 가까운지 확인하고, 아니면 임계값을 올린다.

고친 뒤 `--resume`으로 이어서 돌리면 새 임계값이 적용된다.

나머지 커리큘럼은 전부 `measure: progress`(= `step / max_steps`)라 `max_steps`를
바꾸면 **전환 시점이 통째로 달라진다.** 8,000,000 기준으로:

| 파라미터 | 전환 |
|---|---|
| `recipe_pool_size` 1→2→3 | 1.2M / 2.4M |
| `needs_prep` 0→1 | 1.6M |
| `cook_time` 2s→5s | 2.8M |
| `order_slots` 1→2→3 | 4.0M / 5.2M |

### 최종 난이도가 실현 가능한가 (확인됨)

`target_dishes`는 reward 게이트라 한 번 오르면 내려오지 않는데, 난이도 손잡이는
progress로 계속 올라간다. 그래서 후반에 **최종 난이도로 45초 안에 3접시**를
요구받는다. 실현 불가능하면 후반 학습이 통째로 헛돈다.

측정 결과 여유가 있다.

- `tools/measure_reach.py` — 완벽히 협력할 때 접시 하나의 **이동 병목 1.4~1.6초**
  (현재 배치, 세 레시피 전부)
- 냄비가 하나라 조리는 직렬이다 → 3접시 = 조리 15초 + 이동 약 5초 ≈ **20초**
- 에피소드 45초. **2배 이상 여유**다.

그래도 `Kitchen/GoalReached`가 후반에 0에 붙어 있으면 `episodeDuration`이나
`target_dishes` 임계값을 의심한다.

---

## 5. run-id 규칙

| run-id | 용도 |
|---|---|
| `undercooked_v1` | 본 학습. 이어서 돌릴 때도 이 id를 쓴다 |
| `undercooked_<내용>` | ablation / 실험 (`undercooked_nomemory`, `undercooked_noorder` 등) |
| `smoke` | 배선 확인용 1~2분 런. 확인 후 `results/smoke`를 지운다 |

`results/`는 `.gitignore`에 있다. 학습 결과를 저장소에 커밋하지 않는다.
최종 모델만 `models/undercooked.onnx`로 옮긴다.

---

## 6. 2026-09-15 재검증 결과

검증한 코드: PR #7 head `3f97e66` (이 절차 보완은 별도 로컬 변경).

- 현재 Python 3.10.12 / ML-Agents 1.1.0 / torch 2.2.2+cpu 환경에서 `pip check` 통과.
- 실제 CLI의 `parse_command_line`으로 POCA 등록과 YAML 파싱 통과, 파라미터 7종 확인.
- Unity 6000.3.19f1에서 회귀 검사 [1]~[6], [5b] 전부 통과.
- Python `UnityEnvironment` 연결 및 마스킹을 지킨 무작위 행동 검사 통과:
  32명, 그룹 16개, 관측 103, 이산 행동 [5, 2], 종료 64건, Kitchen 지표 6종.
- 런타임 16개 주방에서 목표 1 / 손질 꺼짐 / 조리 2초 / 에피소드 45초 /
  외부 재료 상한 2를 확인했다. 주문 파라미터 3종은 전송 값과 ResetEnv 경로를 확인했다.
- 무작위 검사에서 행동 요청 수는 에피소드당 450~451회였다. ML-Agents의
  `agent_processor.py`는 최초 요청을 episode_steps에 세지 않으므로 대응하는
  Episode Length 집계는 449~450이다. 매 에피소드가 정확히 450이라는 보장은 아니다.
- `measure: reward`는 개인 보상 버퍼를 참조하며, POCA는 동료 개인 보상도
  최적화 신호에 포함한다는 것을 설치된 패키지 소스로 재확인했다.

이 검사는 정책 최적화나 모델 저장을 하지 않았다. 기존 20k 학습 스모크 런을
이번에 재실행한 것은 아니다. 본 학습 `undercooked_v1`도 실행하지 않았다.
Unity는 Play를 종료했고 씬 변경은 저장하지 않았다.
대상 데스크탑의 신규 환경 설치·CUDA 연산·학습 처리량은 그 PC에서 확인해야 한다.
커리큘럼 임계값 -0.3 / +0.1의 실제 전환 품질은 본 학습 곡선으로 검증한다.

---

## 7. 2026-09-18 최종 검증 결과

학습 실행 전 마지막 점검. **학습은 돌리지 않았다.** Unity MCP로 에디터를 직접 확인했다.

- 컴파일 에러·경고 0건. 씬 `Assets/Scenes/UnderCooked.unity`.
- `StartupValidator`: 씬-코드 일치 (32명 / 관측 103 / 행동 [5, 2] / 에피소드 45s).
- 보상 회귀 검사 [1]~[7] 전부 통과. 새로 추가한 [7]은 −0.49.
- 런타임 파라미터 실측: `needsPrep=True / cookTime=5s / orderSlots=3 / recipePool=3 /
  orderDuration=20s / episode=45s`, RedBox 표시됨, 주문 슬롯 3개 채워짐.
- 그릇 상한 동작 확인: 나와 있는 그릇 0개·1개면 그릇함 Interact 가능, **2개면 막히고**,
  다시 0개가 되면 풀린다.
- 빈 그릇 쓸모 판정 확인: 냄비가 비었으면 A에게 쓸모 **없음**, 냄비를 채우면 있음,
  같은 상태에서 B에게는 없음(냄비는 A 구역).

이번에 고친 것은 §4-18 하나다(빈 그릇이 모든 어뷰징 방어의 바깥에 있던 문제).
나머지는 문서·주석 정정과 곡선 오독 방지 안내다.

Play는 매번 종료했고 씬 변경은 저장하지 않았다.
본 학습 `undercooked_v1`은 여전히 실행 전이고, 대상 데스크탑의 환경 설치·CUDA 연산·
학습 처리량은 그 PC에서 확인해야 한다.

---

## 8. 2026-09-24 전달 보상 회수 추가

§7 이후 학습 직전 점검에서 **서빙 0회로 lesson0 관문을 넘는 경로 2개**를 에디터에서 재현했다
(README §4-19). 전달 보상이 회수 체계 바깥에 있었던 것이 원인이다.

- 수정 전 재현(5사이클): 전달→투입→비우기 개인 합계 사이클당 +0.30,
  냄비가 찬 동안 새 그릇 왕복 +0.15. 팀 보상은 두 경우 모두 0.
- 수정 후: 컴파일 에러 0건, 회귀 검사 [1]~[9]와 [5b] (출력 10개 항목) 전부 통과.
  [8] 0.00 / [9] −0.45, 정상 파이프라인 [4]와 정상 서빙 [5b]는 그대로.
- 새 진단 지표 `Kitchen/TransferClawedBack`을 추가했다.

Unity MCP로 에디터에서 직접 실행했다. Play는 매번 종료했고 씬은 변경하지 않았다.
본 학습은 여전히 실행 전이다.

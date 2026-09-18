# 학습 실행

저장소의 학습 설정과 다른 PC에서 필요한 **환경 설치·검증·실행 절차**를 다룬다.
Python 환경과 GPU 드라이버는 git pull로 설치되지 않는다.
왜 그렇게 설계했는지는 `README.md`에 있다.

---

## 0. 학습할 코드와 Unity 프로젝트 확인

PR #7이 병합되기 전에는 `dev`만 pull해도 수정이 들어오지 않는다.
현재 PR 검증 대상은 `dev_training-setup`의 `3f97e66`이다.

```bash
git fetch origin
git switch dev_training-setup
git pull --ff-only origin dev_training-setup
git log -1 --oneline
```

PR 병합 후에는 `dev`로 전환해 `git pull --ff-only origin dev`를 실행한다.
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
   [1]~[6]과 [5b]가 전부 OK인지 확인한 뒤 **Play를 종료한다.**
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

**랜덤 정책(lesson0) 기준선** — 20k 스텝 스모크 런 실측:
`Cumulative Reward −0.78` / `Group Cumulative Reward −0.50` / `Extrinsic Reward −2.06`
/ `Episode Length 450`. 학습이 시작되면 이 값들보다 올라가야 한다.

`Policy/Extrinsic Reward`는 앞의 둘의 합이 아니다. POCA는 `add_groupmate_rewards = True`라
**동료의 개인 보상까지 더한다** (−2.06 = 2 × (−0.78) + (−0.50)). 즉 개인 보상도
최적화 관점에서는 팀이 같이 진다.

환경 쪽 진단 지표(`KitchenGroup.RecordStats`)는 `README.md` §6 표를 본다.
서빙이 0인데 보상이 오르면 `Kitchen/ServesPerTransfer`와 `Kitchen/CreditClawedBack`을
먼저 본다.

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

---

## 5. run-id 규칙

| run-id | 용도 |
|---|---|
| `undercooked_v1` | 본 학습. 이어서 돌릴 때도 이 id를 쓴다 |
| `undercooked_<내용>` | ablation / 실험 (`undercooked_nomemory`, `undercooked_noorder` 등) |
| `smoke` | 배선 확인용 1~2분 런. 확인 후 `results/smoke`를 지운다 |

`results/`는 `.gitignore`에 있다. 학습 결과를 저장소에 커밋하지 않는다.
최종 모델만 `models/undercooked.onnx`로 옮긴다.


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

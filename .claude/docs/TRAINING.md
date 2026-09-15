# 학습 실행

환경 구축은 끝났고, 이 문서는 **학습을 실제로 돌리는 절차**만 다룬다.
왜 그렇게 설계했는지는 `README.md`에 있다.

---

## 1. 파이썬 환경 만들기

학습은 노트북이 아니라 **데스크탑(RTX 2080 Super)** 에서 돌린다. 저장소에는 파이썬
환경이 들어 있지 않으므로 그 PC에서 새로 만든다.

```bash
conda create -n mlagents python=3.10 -y
conda activate mlagents
pip install mlagents==1.1.0
```

`mlagents==1.1.0`은 의존성으로 **CPU 전용 torch**를 끌고 온다. 그대로 두면 2080 Super를
쓰지 못한다. 설치 후 CUDA 빌드로 바꿔 끼운다.

```bash
pip install torch==2.2.2 --index-url https://download.pytorch.org/whl/cu121
```

확인:

```bash
python -c "import torch, mlagents_envs; print(torch.__version__, torch.cuda.is_available())"
# 2.2.2+cu121 True  가 나와야 한다. 'True'가 아니면 GPU를 안 쓰고 있는 것이다.
```

> 이 저장소를 만든 노트북 환경은 `torch 2.2.2+cpu` / Python 3.10.12 / numpy 1.23.5다.
> Unity 쪽은 Unity 6000.3.19f1 + `com.unity.ml-agents` 4.0.3.

---

## 2. 실행 전 체크리스트

**순서가 중요하다. 반대로 하면 조용히 1/16 속도로 학습된다.**

1. **트레이너를 먼저 띄운다.**
   ```bash
   conda activate mlagents
   cd UnderCooked
   mlagents-learn configs/undercooked.yaml --run-id=undercooked_v1 --torch-device cuda
   ```
   `Listening on port 5004` 가 뜰 때까지 기다린다.

2. **그 다음** Unity 에디터에서 Play를 누른다.

   > Play를 먼저 누르면 트레이너가 없어서 `BehaviorParameters.IsInHeuristicMode()`가
   > true가 되고, `KitchenEnv`가 그걸 사람 플레이로 읽어 **주방 15개를 꺼버린다.**
   > 이 판정은 한 번 굳으므로 뒤늦게 트레이너가 붙어도 주방 하나로만 학습된다.

3. **콘솔 두 줄을 확인한다.**
   ```
   [StartupValidator] 씬-코드 일치 확인 (32명). 관측 103 / 행동 [5, 2] / 에피소드 45s
   [StartupValidator] 학습 모드 (트레이너 연결됨, 주방 16개)
   ```
   - 첫 줄이 에러로 바뀌면 Play가 자동으로 멈춘다. 그대로 학습하면 안 된다.
   - 둘째 줄이 `사람 플레이 모드`거나 주방이 16개가 아니면 **1번으로 돌아간다.**

4. **보상 회귀 검사** — 메뉴 `UnderCooked/보상 회귀 검사` (`Ctrl+Shift+T`).
   시나리오 [1]~[6]이 **전부 OK**여야 한다. 하나라도 `★ 실패`면 보상 설계가
   깨진 것이므로 학습을 시작하지 않는다.

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
| `Policy/Extrinsic Reward` | 개인 + 팀. **실제로 최적화되는 값** |

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

- **500k 스텝까지 lesson0이 안 넘어가면** — `Environment/Cumulative Reward` 히스토그램에서
  성공 에피소드 쪽 봉우리를 읽고, 임계값을 그 아래로 내린다.
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

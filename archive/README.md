# 학습 결과 보관 (archive)

`results/`는 gitignore라서 학습 결과가 학습 PC에만 있었다. 최종 모델까지 이어지는 런 6개의
결과를 여기에 보관한다. 체크포인트(`*.pt`)와 중간 스냅샷 `.onnx`는 용량 때문에 뺐다
(런마다 약 50MB).

## runs/

| 폴더 | 설정 파일 | 스텝 | 결과 |
|---|---|---|---|
| `undercooked_v1` | `configs/undercooked.yaml` | 1.44M (중단) | 서빙 0회. 실패 런 |
| `undercooked_lesson0` | `configs/undercooked_lesson0.yaml` | 3M | lesson0 목표 달성 99.7% |
| `undercooked_v2` | `configs/undercooked.yaml`, `--initialize-from=undercooked_lesson0` | 8M | 최종 난이도 54% |
| `undercooked_final` | `configs/undercooked_final.yaml`, `--initialize-from=undercooked_v2` | 3M | 최종 난이도 74% |
| `undercooked_final2` | 같은 설정, `--initialize-from=undercooked_final` | 3M | 최종 난이도 90.5% |
| `undercooked_final3` | 같은 설정 + 벌점 −0.3, `--initialize-from=undercooked_final2` | 3M | **최종 난이도 97.8%** |

보완 실험 (`reports/2026-09-29-experiment-results.md`):

| 폴더 | 설정 파일 | 스텝 | 결과 |
|---|---|---|---|
| `eval_final2` | `eval_final.yaml` (폴더 안, `undercooked_final.yaml`의 max_steps만 400k), `--inference --initialize-from=undercooked_final2` | 400k | 학습 없이 추론 92.0% (환경 확인용) |
| `undercooked_stage_s1` | `configs/undercooked_stage.yaml`, `--seed=1` | 4.53M (중단) | 0단계에서 서빙 0회. 실패 런 |
| `undercooked_stage_s2` | `configs/undercooked_stage.yaml`, `--seed=2` | 20M | 무작위 초기화에서 한 번에 최종 난이도. 94.5% (강제 승급 1회) |
| `undercooked_stage_s3` | `configs/undercooked_stage.yaml`, `--seed=3` | 4.52M (중단) | 0단계에서 서빙 0회. 실패 런 |
| `undercooked_stage_s4` | `configs/undercooked_stage.yaml`, `--seed=4` | 20M | 한 번에 최종 난이도. 92.0% (강제 승급 1회) |
| `undercooked_stage_s2_hold` | `configs/undercooked_final_hold.yaml`, `--initialize-from=undercooked_stage_s2 --seed=2` | 3M | 틀린 요리 보유 벌점. 95.1% (기준선 s2_ft 93.9%, 오차 범위 안) |
| `eval_stage_s2_hold`, `eval_stage_s2_ft_v2` | `eval_final.yaml` (폴더 안), `--inference` | 600k | 추론 94.3% / 95.5%. `episodes.txt`에 틀린 요리 시간 칸 추가 |
| `undercooked_stage_beta03_s1` | `configs/undercooked_stage_beta03.yaml` (beta 0.03), `--seed=1` | 4.5M | 0단계에서 서빙 0회. 탐색을 늘려도 실패 |
| `undercooked_stage_pen01_s1`, `_s3` | `configs/undercooked_stage_pen01.yaml` (벌점 −0.1), `--seed=1`, `3` | 4.5M / 2.08M | 벌점 −0.3 런과 비트 단위로 같다. 0단계에서는 벌점이 발생하지 않는다 |
| `undercooked_stage_s2_ft` | `configs/undercooked_final_pen03.yaml`, `--initialize-from=undercooked_stage_s2`, `--seed=2` | 3M | 93.9%. 이어 학습해도 오르지 않았다 |
| `eval_stage_s2_ft`, `eval_final3` | `eval_final.yaml` (폴더 안), `--inference` | 600k | 추론 95.6% / 97.5%. 에피소드별 기록 `episodes.txt` (실패 분석용) |
| `pen01_s1` ~ `s3` | `configs/undercooked_final_pen01.yaml`, `--initialize-from=undercooked_final2`, `--seed=1~3` | 3M | 95.7–97.6% |
| `pen03_s1` ~ `s3` | `configs/undercooked_final_pen03.yaml`, 같은 출발점, `--seed=1~3` | 3M | 94.7–97.3% |

파랑 재료·요리 6종 (`reports/2026-10-04-blue-ingredient.md`, 관측 145. 위 런들의 모델과 호환되지 않는다):

| 폴더 | 설정 파일 | 스텝 | 결과 |
|---|---|---|---|
| `undercooked_blue_s1` | `configs/undercooked_blue_stage.yaml` (커밋 95cff34의 단계표), `--seed=1` | 25M | 6종 단계 45%. GreenSoup/MixSoup만 만들고 파랑은 0회 (`stage_log.txt`) |
| `undercooked_blue_final` | `configs/undercooked_blue_final.yaml`, `--initialize-from=undercooked_blue_s1 --seed=1` | 9M | 45%. 이어 학습해도 오르지 않았다 |
| `eval_blue_final` | `eval_blue.yaml` (폴더 안, 6종 고정 600k), `--inference` | 600k | 진단. 냄비 채움 기록 `fills.csv`, 요약 `summary.txt` |
| `undercooked_blue_fix` | `configs/undercooked_blue_fix.yaml` (7단계부터), `--initialize-from=undercooked_blue_final --seed=1` | 12M | 파랑을 배웠지만 빨강을 버렸다. 6종 26% (`stage_log.txt`) |
| `eval_undercooked_blue_fix` | `eval_blue.yaml`, `--inference` | 600k | 진단. 26.1%, 잘못 채움 53% |
| `undercooked_blue_fix9` | `configs/undercooked_blue_fix9.yaml` (9단계 고정), `--initialize-from=undercooked_blue_fix --seed=1` | 14M | **6종 93.3%** |
| `eval_undercooked_blue_fix9` | `eval_blue.yaml`, `--inference` | 600k | **추론 92.4%, 잘못 채움 9%** |
| `undercooked_blue_red` | `configs/undercooked_blue_red.yaml` (RedSoup만 주문), `--initialize-from=undercooked_blue_fix9 --seed=1` | 3M | RedSoup 100% |
| `undercooked_blue_red_mix` | `configs/undercooked_blue_red_mix.yaml` (6종 45초 / 목표 3), `--initialize-from=undercooked_blue_red --seed=1` | 9M | 99.1% |
| `eval_undercooked_blue_red_mix` | `eval_config.yaml` (폴더 안), `--inference` | 600k | 추론 99.4%, 잘못 채움 3%, RedSoup 선택 6.5% |
| `undercooked_blue_long` | `configs/undercooked_blue_long.yaml` (90초 / 목표 5 / 만료 −1), `--initialize-from=undercooked_blue_red_mix --seed=1` | 2.3M (중단) | 98.5% |
| `undercooked_blue_long8` | `configs/undercooked_blue_long8.yaml` (90초 / 주문 30초 / 목표 8), `--initialize-from=undercooked_blue_long --seed=1` | 4.8M (중단) | 97.4% |
| `undercooked_blue_long8_g995` | `configs/undercooked_blue_long8_g995.yaml` (만료 −3, γ 0.995), `--initialize-from=undercooked_blue_long8 --seed=1` | 3.0M (중단) | 95.5% |
| `undercooked_blue_urgent` | `configs/undercooked_blue_urgent.yaml` (급한 주문 보너스 1.5), `--initialize-from=undercooked_blue_long8_g995 --seed=1` | 9M | 98.9% (90초 / 목표 8) |
| `eval_undercooked_blue_urgent_45`, `_90` | `eval_config.yaml` (폴더 안), `--inference` | 600k | 45초: 99.7%, RedSoup 10.8% / 90초: 99.1%, 13.8% |
| `undercooked_blue_urgent3` | `configs/undercooked_blue_urgent3.yaml` (보너스 3.0), `--initialize-from=undercooked_blue_urgent --seed=1` | 9M | **최종 모델** (= `models/undercooked.onnx`) |
| `eval_undercooked_blue_urgent3_45`, `_90` | `eval_config.yaml` (폴더 안), `--inference` | 600k | 45초: **99.5%, RedSoup 16.6%** / 90초: 99.1%, 21.1% |
| `eval_undercooked_blue_urgent3_episodes` | `eval_config.yaml` (폴더 안, 45초 / 목표 3), `--inference --seed=1` | 600k | 판 단위 기록 `episodes.txt` (`episode_log_public.cs`). **1105판 99.5%** |

냄비 채움 기록 도구: `tools/inference/pot_fill_log.cs` (Play 중 실행), 요약 `tools/analyze_pot_fills.py`.
추론 런의 목표 달성은 600k 스텝까지의 `Kitchen/GoalReached` 요약 평균이다. `eval_*`은 모두 `mlagents-learn --inference`(Python 정책)로 잰 것이고, `.onnx`를 Unity 안에서 돌린 평가가 아니다.
`summary.txt`(냄비 채움 요약)는 2026-10-08에 고친 `analyze_pot_fills.py`로 다시 만들었다. [4]의 made-right가 첫 재료 순간 주문판에 있던 요리만 센다(전에는 나중 주문과 맞은 냄비도 셌다).

런 폴더마다 들어 있는 것:

| 파일 | 내용 |
|---|---|
| `events.out.tfevents.*` | TensorBoard 스칼라 전체 (원본 `results/<run>/Chef/`) |
| `configuration.yaml` | 트레이너가 실제로 읽은 최종 설정 (기본값 포함) |
| `Chef.onnx` | 런 종료 시점 모델. `undercooked_blue_urgent3/Chef.onnx`는 지금의 `models/undercooked.onnx`와 같은 파일 (MD5 `660bc39e…`). 재료 2종 시절의 제출 모델은 `undercooked_final3/Chef.onnx` (MD5 `00bf1911…`) |
| `timers.json`, `training_status.json` | ML-Agents 실행 기록 (명령줄, 소요 시간, 체크포인트 목록) |
| `console.log` | 트레이너 콘솔 출력 (`results/<run>_console.log`) |

v1은 중단한 런이라 `Chef.onnx`, `configuration.yaml`, 실행 기록이 없다.

memory ablation 런(`undercooked_mem_on` / `mem_off` 등)은 이 브랜치의 코드와 설정으로 만든 것이 아니라서
넣지 않았다 (PR #17 쪽 실험).

## 곡선 보기

```bash
tensorboard --logdir archive/runs
```

## tools/

학습·검증에 쓴 보조 스크립트 (원본 `results/.tools/`). 경로는 이 PC(`D:/PCUBE/UnderCooked`,
`C:/Users/User/miniconda3/envs/mlagents`) 기준으로 적혀 있다.

| 파일 | 용도 |
|---|---|
| `tb.py` | 런의 스텝 구간 평균. `python tb.py <run-id> <시작> <끝>` (`results/` 기준) |
| `mcp.sh`, `mcp_exec.py` | 로컬 UnityMCP 서버(`127.0.0.1:8080/mcp`)에 직접 요청. C# 코드 실행 |
| `inference/*.cs` | Unity 추론 확인 절차 (`.claude/docs/TRAINING.md`, 모델 할당 → 집계 → 원상복구) |
| `analyze_tally.py` | 추론 집계 결과를 성공/실패로 나눠 요약 |
| `inference/episode_log.cs` | 트레이너(`--inference`)가 돌리는 동안 주방별 에피소드 기록. 씬을 바꾸지 않는다 |
| `inference/episode_log_public.cs` | 위와 같은 기록을 공개 API만으로 남긴다. unity-mcp의 `Unity_RunCommand`는 리플렉션을 막아서 재료 3종 코드에서는 이쪽을 쓴다 |
| `analyze_episodes.py` | `episode_log.cs` 기록을 성공/실패, 잘못 채움 횟수별로 요약. 여러 파일을 한 번에 받는다 |
| `figures/export.py` | 이벤트 파일 → CSV. `python export.py out.csv archive/runs` |
| `figures/plot.py` | CSV → `assets/tb_*.png` (matplotlib, 맑은 고딕) |
| `figures/cam.cs`, `rec.cs` | Play 중 임시 카메라로 주방 하나의 한 에피소드를 프레임 PNG로 녹화. `<FRAMES_DIR>`를 바꿔 쓴다 |
| `figures/gif2.py` | 프레임 + 기록(서빙 수, 주문) → `assets/demo.gif`, `assets/demo_blue.gif`. 넷째 인자로 `rec.cs`가 보고한 최종 서빙 수를 주면 마지막 정지 프레임에 반영한다 |
| `figures/plot_blue.py` | 재료 3종 런 11개 → `assets/tb_blue_goal_reached.png`, 추론 냄비 기록 → `assets/blue_pick_rate.png`. 저장소 루트에서 실행 |

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
| `undercooked_stage_beta03_s1` | `configs/undercooked_stage_beta03.yaml` (beta 0.03), `--seed=1` | 4.5M | 0단계에서 서빙 0회. 탐색을 늘려도 실패 |
| `undercooked_stage_pen01_s1`, `_s3` | `configs/undercooked_stage_pen01.yaml` (벌점 −0.1), `--seed=1`, `3` | 4.5M / 2.08M | 벌점 −0.3 런과 비트 단위로 같다. 0단계에서는 벌점이 발생하지 않는다 |
| `undercooked_stage_s2_ft` | `configs/undercooked_final_pen03.yaml`, `--initialize-from=undercooked_stage_s2`, `--seed=2` | 3M | 93.9%. 이어 학습해도 오르지 않았다 |
| `eval_stage_s2_ft`, `eval_final3` | `eval_final.yaml` (폴더 안), `--inference` | 600k | 추론 95.6% / 97.5%. 에피소드별 기록 `episodes.txt` (실패 분석용) |
| `pen01_s1` ~ `s3` | `configs/undercooked_final_pen01.yaml`, `--initialize-from=undercooked_final2`, `--seed=1~3` | 3M | 95.7–97.6% |
| `pen03_s1` ~ `s3` | `configs/undercooked_final_pen03.yaml`, 같은 출발점, `--seed=1~3` | 3M | 94.7–97.3% |

런 폴더마다 들어 있는 것:

| 파일 | 내용 |
|---|---|
| `events.out.tfevents.*` | TensorBoard 스칼라 전체 (원본 `results/<run>/Chef/`) |
| `configuration.yaml` | 트레이너가 실제로 읽은 최종 설정 (기본값 포함) |
| `Chef.onnx` | 런 종료 시점 모델. `undercooked_final3/Chef.onnx`는 `models/undercooked.onnx`와 같은 파일 (MD5 `00bf1911…`) |
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
| `analyze_episodes.py` | `episode_log.cs` 기록을 성공/실패, 잘못 채움 횟수별로 요약 |
| `figures/export.py` | 이벤트 파일 → CSV. `python export.py out.csv archive/runs` |
| `figures/plot.py` | CSV → `assets/tb_*.png` (matplotlib, 맑은 고딕) |
| `figures/cam.cs`, `rec.cs` | Play 중 임시 카메라로 주방 하나의 한 에피소드를 프레임 PNG로 녹화. `<FRAMES_DIR>`를 바꿔 쓴다 |
| `figures/gif2.py` | 프레임 + 기록(서빙 수, 주문) → `assets/demo.gif` |

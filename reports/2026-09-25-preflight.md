# 학습 전 최종 점검 — 2026-09-25

판정: 현재 PC의 학습 실행 환경은 통과. 기본 커리큘럼으로 무작위 초기화부터 다시 시작하는 것은 권장하지 않는다. 기존 lesson0 성공 모델을 초기 가중치로 사용하는 다음 실험이 적절하다. 최종 난이도 학습 성공은 아직 검증되지 않았다.

## 실제 확인한 항목

- 저장소 HEAD: 3f177b9. 열려 있는 씬: Assets/Scenes/UnderCooked.unity.
- 실제 Unity: 6000.3.18f1. Unity ML-Agents 패키지: 4.0.3, 통신 API: 1.5.0.
- Python 3.10.12, mlagents / mlagents-envs 1.2.0.dev0, torch 2.2.2+cu121, numpy 1.23.5, protobuf 3.20.3.
- RTX 2080 SUPER 인식 및 CUDA 텐서 연산 통과. pip check 통과.
- Python 패키지는 C:/Users/User/ml-agents 하위 소스에서 설치되었다는 direct_url 기록이 있다. 해당 소스 체크아웃 HEAD는 a2777719560e4676be99e2ea128c5eb1fbeb3dbb이다. 설치 시점의 소스가 현재 체크아웃과 동일한지는 별도 검증하지 않았다.
- 두 YAML 모두 설치된 실제 mlagents CLI 파서로 통과. Chef 및 환경 파라미터 7종 확인.
- 모든 셰프: Behavior=Chef / Default, 관측 103, 이산 행동 [5,2], 연속 행동 0, MaxStep=0, DecisionPeriod=5, TakeActionsBetweenDecisions=false, 모델 미할당.
- 실제 트레이너 연결: 주방 16개, 셰프 32명. 모든 주방에서 관측 실측 103, 카운터 4개, episode 45초, target 1, prep false, cook 2초, HumanPlay=false.
- 트레이너 없는 새 Play 세션에서 기존 보상 회귀 검사 10개 전부 통과.
- 최초 점검 세션에서는 실행 중 스크립트 재컴파일 이후 초기화 필드 소실과 예외가 관찰되었다. 새 Play 세션에서 재검사한 결과 통과했고, 이후 연결 점검에서도 재현되지 않았다. 실행 중 코드 변경/재컴파일이 발생하면 Play를 종료하고 새로 시작해야 한다.
- 마지막 Unity 콘솔 조회: 오류·경고 0건. 최종 상태: Play 종료.

## 별도 짧은 학습 검증

기존 결과와 분리한 run-id: preflight_20260925_0225_smoke.
설정: results/preflight_20260925_0225/config.yaml.
고정 lesson0 설정에서 max_steps=25000, summary_freq=5000, checkpoint_interval=25000만 변경. CUDA, time_scale=20으로 실행.

- 정상 종료(exit code 0), 약 45초, 최종 체크포인트 26720 스텝. 배치 처리로 지정 종료 스텝을 조금 초과했다.
- 실제 정책 업데이트 확인: 25000 요약의 Policy Loss=0.146381, Value Loss=0.055963, Baseline Loss=0.306682. 모두 유한값.
- Chef.onnx 저장 및 onnx.checker 검증 통과. 기존 undercooked_lesson0/Chef.onnx도 구조 검증 통과.
- 이 짧은 실행은 연결·업데이트·저장을 확인한다. 정책 성능이나 최종 난이도 성공을 증명하지 않는다.
- Python의 pkg_resources, 텐서 전치, LSTM ONNX 관련 경고가 있었으나 실행과 모델 구조 검증은 성공했다. 내보낸 모델의 Unity 추론 실행은 이번 점검 범위에 포함하지 않았다.

## 본 학습 전에 반영할 판단

1. 기본 커리큘럼의 난이도 상승이 빠를 가능성이 있다.
   undercooked_v1은 144만 스텝까지 기록된 모든 요약에서 서빙/성공률 0이었다.
   고정 lesson0 실행은 첫 양수 서빙 요약이 196만 스텝이고, 300만 스텝에서 종료되었다.
   마지막 5개 요약(292만~300만)의 성공률은 100%, 100%, 98.65%, 100%, 100%였다.
   기본 YAML은 120만 스텝에 레시피를 늘리고 160만에 손질을 추가한다.
   서로 다른 실행이므로 이것만으로 원인을 확정할 수는 없지만, 기본 설정으로 처음부터 같은 실험을 반복하기보다 성공한 lesson0 가중치를 활용하는 근거가 있다.

2. 문서의 버전/상태가 현재와 다르다.
   README/TRAINING의 Unity 6000.3.19f1, Python mlagents 1.1.0, CPU 또는 학습 전이라는 설명은 현재 PC와 결과를 정확히 반영하지 않는다.
   실제 설치를 임의로 바꾸지 말고 재현 시 위 실측 버전을 기준으로 삼아야 한다.

3. 기존 실행 이름을 그대로 새 실행에 쓰면 충돌한다.
   undercooked_v1, undercooked_lesson0 결과가 이미 존재한다. 다음 실험에는 새 run-id를 쓴다.
   현재 기본 셸의 python은 3.12이다. 반드시 mlagents 환경을 활성화하거나 환경의 실행 파일을 직접 사용한다.

다음 본 학습의 시작 예시(이번 점검에서는 실행하지 않음):

```powershell
& C:/Users/User/miniconda3/envs/mlagents/Scripts/mlagents-learn.exe configs/undercooked.yaml --run-id=undercooked_curriculum_from_lesson0_v1 --initialize-from=undercooked_lesson0 --torch-device=cuda
```

Listening on port 5004를 확인한 뒤 새 Play 세션을 시작한다. 주방 16개 연결 배너를 확인한다. 이 명령은 학습된 가중치로 새 실행을 시작하며, 기본 커리큘럼의 이후 전환 적절성은 실제 성공률로 계속 확인해야 한다.

## 변경 범위

프로젝트 코드·씬·원본 학습 설정·기존 결과를 수정하지 않았다. 점검 설정, 별도 짧은 학습 결과, 이 보고서만 results 아래에 추가했다. 시작 전부터 있던 ProjectSettings 변경 4개는 그대로 남겨 두었다.

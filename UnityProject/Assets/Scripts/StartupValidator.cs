using System.Text;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

// Play를 누르는 순간 씬이 코드와 맞는지 검사하고, 어긋나면 **학습 시작을 막는다.**
// 아무 TrainingArea 하나에 붙여도 되고(씬 전체를 검사한다), 프리팹에 붙어 있어도 된다.
//
// 이 저장소에서 같은 사고가 세 번 났다.
//   - VectorObservationSize 52 (코드 77)        -> Play 즉시 사망
//   - Action BranchSizes [1] (코드 [5,2])       -> Invalid Action Masking
//   - episodeDuration 30초 (문서/yaml 45초)      -> 조용히 다른 조건으로 학습
//
// 앞의 둘은 큰 소리로 죽기라도 하는데, 셋째처럼 **조용한 불일치**가 제일 위험하다.
// 몇 시간을 학습시키고 나서야 "그게 아니었네"를 알게 된다.
//
// ★ 프리팹만 보면 안 된다. 씬 인스턴스가 프리팹 값을 덮어쓸 수 있으므로,
//   씬에 실제로 존재하는 컴포넌트들을 전부 훑는다.
//
// ★ 이 검사로는 '보상 지급 조건 누락' 같은 로직 결함은 못 잡는다. 그건 KitchenSelfTest가
//   실제 행동을 넣어 보상 변화를 읽는 방식으로 따로 검사한다.
public class StartupValidator : MonoBehaviour
{
    [Tooltip("불일치를 찾으면 Play를 중단한다. 끄면 에러만 남기고 계속 진행한다")]
    [SerializeField] bool stopPlayOnFailure = true;

    [Tooltip("이 값이 씬의 에피소드 길이와 다르면 경고한다. README/yaml과 맞춰 둘 것")]
    [SerializeField] float documentedEpisodeDuration = 45f;

    [Tooltip("Behavior Parameters의 Behavior Name. configs/undercooked.yaml의 키와 같아야 한다")]
    [SerializeField] string expectedBehaviorName = "Chef";

    static bool s_Ran;
    static bool s_ModeLogged;

    void Start()
    {
        // 모드 배너는 모든 인스턴스가 시도하고 먼저 살아남은 하나만 찍는다.
        // s_Ran 가드 안에 두면, 하필 그 인스턴스가 사람 플레이 때 꺼지는
        // 15개 중 하나일 때 배너가 통째로 사라진다.
        StartCoroutine(LogModeNextFrame());

        // 씬 전체를 한 번만 검사한다. TrainingArea가 16개여도 한 번이면 된다.
        if (s_Ran) return;
        s_Ran = true;

        var problems = new StringBuilder();
        int checks = 0;

        foreach (var agent in FindObjectsByType<ChefAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            checks++;
            var behavior = agent.GetComponent<BehaviorParameters>();
            if (behavior == null)
            {
                problems.AppendLine($"  {Path(agent.transform)} : BehaviorParameters가 없다");
                continue;
            }

            var brain = behavior.BrainParameters;

            if (brain.VectorObservationSize != ChefAgent.ObservationSize)
                problems.AppendLine($"  {Path(agent.transform)} : 관측 크기 {brain.VectorObservationSize}"
                                    + $" != 코드 {ChefAgent.ObservationSize}");

            var branches = brain.ActionSpec.BranchSizes;
            if (branches == null || branches.Length != 2 || branches[0] != 5 || branches[1] != 2)
                problems.AppendLine($"  {Path(agent.transform)} : Action BranchSizes ["
                                    + (branches == null ? "" : string.Join(", ", branches)) + "] != [5, 2]");

            if (brain.NumStackedVectorObservations != 1)
                problems.AppendLine($"  {Path(agent.transform)} : NumStackedVectorObservations "
                                    + brain.NumStackedVectorObservations + " != 1"
                                    + " (관측 차원이 배로 늘어 트레이너와 어긋난다)");

            if (behavior.BehaviorName != expectedBehaviorName)
                problems.AppendLine($"  {Path(agent.transform)} : Behavior Name '{behavior.BehaviorName}'"
                                    + $" != '{expectedBehaviorName}' (yaml의 behaviors 키와 같아야 한다)");

            // 실제로 관측을 몇 개 내보내는지까지 확인한다. 선언값만 맞고 코드가 다를 수 있다.
            int actual = CountObservations(agent);
            if (actual < 0)
                problems.AppendLine($"  {Path(agent.transform)} : 관측 개수를 측정하지 못했다"
                                    + " (VectorSensor 내부 구조가 바뀌었을 수 있다). 검사가 무력하다");
            else if (actual != ChefAgent.ObservationSize)
                problems.AppendLine($"  {Path(agent.transform)} : CollectObservations가 {actual}개를 냈다"
                                    + $" != 선언 {ChefAgent.ObservationSize}");
        }

        if (checks == 0) problems.AppendLine("  씬에 ChefAgent가 하나도 없다");

        foreach (var env in FindObjectsByType<KitchenEnv>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var agents = env.GetComponentsInChildren<ChefAgent>(true);
            if (agents.Length != KitchenEnv.AgentCount)
                problems.AppendLine($"  {Path(env.transform)} : ChefAgent가 {agents.Length}명"
                                    + $" != {KitchenEnv.AgentCount}명");

            if (env.GetComponent<KitchenGroup>() == null)
                problems.AppendLine($"  {Path(env.transform)} : KitchenGroup이 없다 (팀 보상이 전부 사라진다)");

            if (!Mathf.Approximately(env.EpisodeDuration, documentedEpisodeDuration))
                problems.AppendLine($"  {Path(env.transform)} : episodeDuration {env.EpisodeDuration}s"
                                    + $" != 문서 기준 {documentedEpisodeDuration}s"
                                    + " (조용히 다른 조건으로 학습된다)");
        }

        if (problems.Length == 0)
        {
            Debug.Log($"[StartupValidator] 씬-코드 일치 확인 ({checks}명). "
                      + $"관측 {ChefAgent.ObservationSize} / 행동 [5, 2] / "
                      + $"에피소드 {documentedEpisodeDuration}s");
            return;
        }

        Debug.LogError("[StartupValidator] 씬과 코드가 어긋난다. 이대로 학습하면 안 된다.\n" + problems);

#if UNITY_EDITOR
        if (stopPlayOnFailure) UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // 지금 어느 모드로 돌고 있는가를 한 줄로 남긴다.
    //
    // Play를 mlagents-learn보다 **먼저** 누르면 트레이너가 없으므로
    // BehaviorParameters.IsInHeuristicMode()가 true가 되고, KitchenEnv가 그걸
    // 사람 플레이로 읽어 **주방 15개를 꺼버린다.** 그 판정은 한 번 굳으므로,
    // 늦게 트레이너가 붙어도 1/16 처리량으로 그대로 학습된다.
    // 화면에 아무 표시가 없으면 몇 시간이 지나서야 알게 된다 - 이 저장소가
    // 반복해서 겪은 '조용한 불일치' 계열이다.
    // ★ 한 프레임 기다렸다가 센다. KitchenEnv.Start()가 주방을 끄는 것과
    //   이 검사의 Start() 순서는 Unity가 보장하지 않는다. 즉시 세면 사람
    //   플레이인데도 16개라고 적혀서, 정확히 이 배너가 막으려는 상황을
    //   못 보게 된다. 틀린 안내는 안내가 없는 것보다 나쁘다 (README 4-8).
    System.Collections.IEnumerator LogModeNextFrame()
    {
        yield return null;

        if (s_ModeLogged) yield break;
        s_ModeLogged = true;

        bool trainer = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
        int liveAreas = FindObjectsByType<KitchenEnv>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        Debug.Log(trainer
            ? $"[StartupValidator] 학습 모드 (트레이너 연결됨, 주방 {liveAreas}개)"
            : $"[StartupValidator] 사람 플레이 모드 (트레이너 없음, 주방 {liveAreas}개)."
              + " 학습하려면 mlagents-learn을 먼저 띄우고 Play할 것");
    }

    // CollectObservations가 **실제로 몇 개를 넣었는지** 센다. 측정 불가면 -1.
    //
    // ★ GetObservationSpec().Shape[0]을 읽으면 안 된다. 그건 VectorSensor 생성자에 준
    //   크기를 그대로 돌려줄 뿐이라, 관측을 1개만 넣어도 103을 반환한다. 처음에 그렇게
    //   짜서 이 검사가 항상 통과하는 껍데기였다. 실제 개수는 내부 목록에만 있다.
    //   (패딩/잘라내기는 Write 시점에 일어나므로 목록에는 넣은 그대로 들어 있다)
    public static int CountObservations(ChefAgent agent)
    {
        var sensor = new Unity.MLAgents.Sensors.VectorSensor(ChefAgent.ObservationSize);
        agent.CollectObservations(sensor);

        var field = typeof(Unity.MLAgents.Sensors.VectorSensor).GetField("m_Observations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = field?.GetValue(sensor) as System.Collections.Generic.List<float>;
        return list?.Count ?? -1;
    }

    void OnDestroy()
    {
        s_Ran = false;
        s_ModeLogged = false;
    }

    static string Path(Transform t)
    {
        return t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}

using System.Text;
using Unity.MLAgents;
using UnityEngine;

// 성공률로 난이도를 올리는 커리큘럼. 주방 16개가 단계 하나를 공유한다.
//
// 기존 yaml 커리큘럼(configs/undercooked.yaml)으로는 한 번에 끝까지 학습되지 않았다.
//   - v1: progress 게이트가 첫 서빙(약 2M)보다 먼저 레시피·손질을 켜서 서빙 0회로 중단
//   - v2: 손질이 켜지는 1.6M에서 서빙 0으로 붕괴, 약 1.5M 스텝을 잃음
// 둘 다 '스텝 수가 지나면 올린다'가 원인이다. 이전 단계를 익혔는지 보지 않는다.
//
// ML-Agents의 measure: reward 게이트도 쓸 수 없었다. 그 값은 개인 보상(AddReward)뿐이라
// 같은 성공률에서도 런·단계마다 크게 다르다 (최종 난이도 성공률 70~80%에서
// final -0.07 / final2 +0.02, 80~90%에서 final3 +0.23. archive/runs 이벤트 파일 기준).
// 임계값을 잘못 잡으면 README 4-16처럼 조용히 영영 못 넘는 관문이 된다.
//
// 그래서 게이트를 성공률(Kitchen/GoalReached) 그 자체로 건다. 단계 표는 여기 코드에,
// 켜기/임계값/창 크기는 yaml의 environment_parameters로 받는다 (configs/undercooked_stage.yaml).
//
// stage_curriculum 파라미터가 없으면(사람 플레이, 회귀 검사, 기존 yaml) 전부 꺼져 있고
// KitchenEnv는 예전처럼 개별 파라미터를 읽는다.
public static class StageCurriculum
{
    public struct Stage
    {
        public int TargetDishes;
        public int RecipePool;
        // 에피소드마다 손질을 켤 확률. 0과 1 사이면 손질 있는 판과 없는 판이 섞인다.
        public float PrepChance;
        public float CookTime;
        public int OrderSlots;

        public override string ToString()
        {
            return $"목표 {TargetDishes} / 레시피 {RecipePool} / 손질 {PrepChance:0.##} / 조리 {CookTime:0.#}s / 슬롯 {OrderSlots}";
        }
    }

    // 순서는 v2의 전환 순서(레시피 2종 -> 손질 -> 레시피 3종 -> 조리 5초 -> 슬롯)를 따른다.
    // 3단계는 v2가 무너진 손질 전환을 둘로 쪼갠 것이다. 손질 없는 판을 섞어 두면
    // 이미 배운 '생재료를 바로 냄비에' 경로가 한순간에 전부 막히지 않는다.
    //
    // 파랑을 넣으면서 바꾼 것 (reports/2026-09-29-experiment-results.md 실험 A):
    //   - 예전 5단계(레시피 3, 슬롯 1)를 뺐다. 성공한 두 런 모두 거기서 강제 승급됐고,
    //     슬롯 2개가 되자 오히려 좋아졌다. 주문이 하나뿐이면 첫 재료를 틀렸을 때 받아줄
    //     다른 주문이 없어서, 레시피가 많을수록 최종 난이도보다 어려운 칸이 된다.
    //     그래서 레시피를 늘리는 단계는 전부 슬롯 2개 이상에서 한다.
    //   - 파랑은 최종 3종을 익힌 뒤에 들인다. 먼저 BlueSoup(파랑x2)만 더해 새 재료함 하나를
    //     익히고(7), 그다음 파랑 조합 두 가지를 더한다(8).
    public static readonly Stage[] Stages =
    {
        new Stage { TargetDishes = 1, RecipePool = 1, PrepChance = 0f,   CookTime = 2f, OrderSlots = 1 }, // 0 = 기존 lesson0
        new Stage { TargetDishes = 3, RecipePool = 1, PrepChance = 0f,   CookTime = 2f, OrderSlots = 1 }, // 1
        new Stage { TargetDishes = 3, RecipePool = 2, PrepChance = 0f,   CookTime = 2f, OrderSlots = 1 }, // 2 주문 읽기 시작
        new Stage { TargetDishes = 3, RecipePool = 2, PrepChance = 0.5f, CookTime = 2f, OrderSlots = 1 }, // 3 손질 절반
        new Stage { TargetDishes = 3, RecipePool = 2, PrepChance = 1f,   CookTime = 2f, OrderSlots = 1 }, // 4
        new Stage { TargetDishes = 3, RecipePool = 3, PrepChance = 1f,   CookTime = 5f, OrderSlots = 2 }, // 5 (예전 6)
        new Stage { TargetDishes = 3, RecipePool = 3, PrepChance = 1f,   CookTime = 5f, OrderSlots = 3 }, // 6 = 예전 최종 난이도
        new Stage { TargetDishes = 3, RecipePool = 4, PrepChance = 1f,   CookTime = 5f, OrderSlots = 3 }, // 7 파랑 등장 (BlueSoup)
        new Stage { TargetDishes = 3, RecipePool = 6, PrepChance = 1f,   CookTime = 5f, OrderSlots = 3 }, // 8 = 최종 난이도 (6종)
    };

    public static int LastStage => Stages.Length - 1;

    public static bool Enabled { get; private set; }
    public static int Current { get; private set; }

    static float s_Threshold;
    static int s_MaxEpisodes;

    // 현재 단계에서 끝난 에피소드의 성공 여부를 최근 창 크기만큼만 기억한다.
    static bool[] s_Window;
    static int s_WindowNext;
    static int s_WindowCount;
    static int s_WindowSuccesses;
    // 현재 단계에서 끝난 에피소드 수 (창과 달리 누적). 상한 판정용.
    static int s_StageEpisodes;

    // Play를 새로 누를 때마다 처음 상태에서 시작한다. 도메인 리로드를 끄는 설정에서도
    // 이전 Play의 단계가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Enabled = false;
        Current = 0;
        s_Window = null;
        s_WindowNext = s_WindowCount = s_WindowSuccesses = s_StageEpisodes = 0;
    }

    // KitchenEnv.ResetEnv가 매 에피소드 부른다. 켜질 때 한 번만 초기화한다.
    //
    // 켜졌는지는 켜질 때까지 매번 다시 본다. 트레이너의 파라미터는 사이드 채널로 오는데,
    // 주방들의 첫 ResetEnv(KitchenGroup.Start)가 그보다 먼저 돌 수 있다. 처음 한 번만 읽으면
    // 그때 못 받은 값으로 '꺼짐'이 굳어버린다. 켜지기 전의 몇 판은 기존 경로(개별 파라미터)로
    // 돌고 Stage가 -1이라 판정에 들어가지 않는다.
    public static void Configure(EnvironmentParameters envParams)
    {
        if (Enabled) return;
        if (envParams.GetWithDefault("stage_curriculum", 0f) < 0.5f) return;
        Enabled = true;

        s_Threshold = Mathf.Clamp01(envParams.GetWithDefault("stage_success_threshold", 0.8f));
        int window = Mathf.Max(1, Mathf.RoundToInt(envParams.GetWithDefault("stage_window", 500f)));
        s_MaxEpisodes = Mathf.Max(0, Mathf.RoundToInt(envParams.GetWithDefault("stage_max_episodes", 0f)));
        // --resume으로 이어 학습할 때 Unity는 처음부터 다시 켜지므로 단계를 직접 지정해야 한다.
        Current = Mathf.Clamp(Mathf.RoundToInt(envParams.GetWithDefault("stage_start", 0f)), 0, LastStage);

        s_Window = new bool[window];
        ClearWindow();

        var sb = new StringBuilder();
        sb.Append($"[StageCurriculum] 켜짐. 시작 단계 {Current} ({Stages[Current]}), ");
        sb.Append($"최근 {window}판 성공률 {s_Threshold:0.##} 이상이면 승급");
        if (s_MaxEpisodes > 0) sb.Append($", 단계당 {s_MaxEpisodes}판 넘으면 강제 승급");
        Debug.Log(sb.ToString());
    }

    // 에피소드 하나가 끝났다. playedStage는 그 에피소드가 **시작할 때의** 단계다.
    // 승급 직전에 시작해서 승급 뒤에 끝난 에피소드는 이전 단계 기록이므로 버린다.
    public static void Report(int playedStage, bool success)
    {
        if (!Enabled || playedStage != Current || s_Window == null) return;

        if (s_WindowCount == s_Window.Length)
        {
            if (s_Window[s_WindowNext]) s_WindowSuccesses--;
        }
        else
        {
            s_WindowCount++;
        }
        s_Window[s_WindowNext] = success;
        if (success) s_WindowSuccesses++;
        s_WindowNext = (s_WindowNext + 1) % s_Window.Length;
        s_StageEpisodes++;

        if (Current >= LastStage) return;

        bool full = s_WindowCount == s_Window.Length;
        bool passed = full && WindowSuccessRate >= s_Threshold;
        bool forced = !passed && s_MaxEpisodes > 0 && s_StageEpisodes >= s_MaxEpisodes;
        if (!passed && !forced) return;

        int from = Current;
        float rate = WindowSuccessRate;
        int episodes = s_StageEpisodes;
        Current++;
        ClearWindow();

        string reason = passed ? "성공률 도달" : "★ 판 수 상한으로 강제 승급";
        Debug.Log($"[StageCurriculum] 단계 {from} -> {Current} ({reason}: 최근 {s_Window.Length}판 성공률 {rate:0.000}, " +
                  $"이 단계 {episodes}판). 새 조건: {Stages[Current]}");
    }

    // 현재 단계의 최근 창 성공률. 창이 덜 찼으면 찬 만큼으로 계산한다.
    public static float WindowSuccessRate => s_WindowCount > 0 ? (float)s_WindowSuccesses / s_WindowCount : 0f;

    static void ClearWindow()
    {
        if (s_Window != null) System.Array.Clear(s_Window, 0, s_Window.Length);
        s_WindowNext = s_WindowCount = s_WindowSuccesses = s_StageEpisodes = 0;
    }

    // ── 회귀 검사 전용 ───────────────────────────────────────
    // 트레이너 없이는 environment_parameters를 받을 수 없으므로 검사가 직접 켜고 끈다.
    public static void EnableForTest(float threshold, int window, int maxEpisodes, int startStage)
    {
        Enabled = true;
        s_Threshold = threshold;
        s_MaxEpisodes = maxEpisodes;
        Current = Mathf.Clamp(startStage, 0, LastStage);
        s_Window = new bool[Mathf.Max(1, window)];
        ClearWindow();
    }

    // 검사가 끝나면 Play 시작 때와 같은 상태로 되돌린다. 다음 ResetEnv가 설정을 다시 읽는다.
    public static void DisableForTest()
    {
        ResetStatics();
    }
}

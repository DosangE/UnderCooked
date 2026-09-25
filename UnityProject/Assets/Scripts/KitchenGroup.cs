using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

// TrainingArea 하나의 셰프 둘을 한 팀으로 묶는다 (MA-POCA).
// 팀 보상 지급, 에피소드 종료/중단, 씬 리셋을 담당한다.
// KitchenEnv와 같은 오브젝트(TrainingArea 루트)에 붙인다.
[RequireComponent(typeof(KitchenEnv))]
public class KitchenGroup : MonoBehaviour
{
    [Tooltip("비워두면 같은 오브젝트에서 찾는다")]
    [SerializeField] KitchenEnv env;

    [Tooltip("비워두면 자식에서 ChefAgent를 전부 찾아 AgentIndex 순으로 정렬한다")]
    [SerializeField] List<ChefAgent> agents = new List<ChefAgent>();

    [Header("팀 보상 (AddGroupReward)")]
    [SerializeField] float rewardServe = 3.0f;
    [Tooltip("손질대를 거쳐 다음 단계로 넘어갔을 때. 체인이 길어진 만큼 중간 신호를 하나 더 준다")]
    [SerializeField] float rewardPrepped = 0.2f;
    [SerializeField] float rewardIngredientInPot = 0.3f;
    [SerializeField] float rewardGoalBonus = 2.0f;
    [Tooltip("주문 제한 시간을 넘겨서 손님이 떠났을 때. " +
             "너무 크면 '어차피 못 하니 아무것도 안 한다'가 최적이 되므로 서빙 보상보다 훨씬 작게 둔다")]
    [SerializeField] float rewardOrderExpired = -0.5f;

    SimpleMultiAgentGroup m_Group;
    bool m_Ready;

    // 이 그룹에 지금까지 지급된 팀 보상의 합. **회귀 검사/진단 전용이다.**
    // SimpleMultiAgentGroup은 누적값을 노출하지 않아서, 보상 설계가 의도대로
    // 동작하는지 자동으로 확인하려면 이쪽에서 따로 세어야 한다.
    public float TotalGroupReward { get; private set; }

    // 진단용 집계. 에피소드마다 StatsRecorder로 내보내고 0으로 리셋한다.
    int m_Transfers;
    int m_OrdersExpired;
    // 이번 에피소드에 회수된 진행 보상의 합. 냄비 비우기/잘못된 제출/종료 정산을 전부 더한다.
    // 마지막 종료 정산액만 담으면 '중간에 얼마나 버렸는지'가 통째로 빠진다.
    float m_CreditClawedBack;
    // 이번 에피소드에 회수된 전달 보상 (셰프 한 명당 금액의 합).
    float m_TransferClawedBack;

    // 서빙까지 가는 체인의 고리별 통과 횟수. 보상과 무관한 순수 관측이다.
    // 서빙이 0일 때 '어느 고리에서 끊기는가'를 보려고 둔다. 전달·회수 지표만으로는
    // 냄비가 찼는지, 요리를 떴는지 구분할 수 없었다 (reports/2026-09-25 §3-5).
    public enum ChainStep { PotCommitted, PlateToPotSide, DishTaken, DishToServeSide }
    static readonly string[] ChainStatNames =
    {
        "Kitchen/PotCommitted",     // 냄비가 재료로 다 차서 조리가 확정됨
        "Kitchen/PlatesToChefA",    // 빈 그릇이 냄비 쪽 셰프에게 건너감 (보상 지급 여부와 무관)
        "Kitchen/DishesTaken",      // 냄비에서 완성 요리를 뜸
        "Kitchen/DishesToChefB",    // 완성 요리가 서빙구 쪽 셰프에게 건너감 (보상 지급 여부와 무관)
    };
    readonly int[] m_Chain = new int[ChainStatNames.Length];

    // 주문과 맞지 않은 요리가 어디서 생기는가. 보상과 무관한 순수 관측이다.
    // 최종 모델의 실패 에피소드는 요리를 충분히 만들고도 주문과 안 맞아 버렸다
    // (reports/2026-09-25-training-results.md §5). 그 원인을 둘로 가른다.
    //   ServedWrongOrder - PotCommittedWrong ≈ 냄비를 채울 땐 맞았는데 그 사이 주문이 사라진 경우
    public enum OrderMiss { PotCommittedWrong, ServedWrongOrder }
    static readonly string[] OrderMissStatNames =
    {
        "Kitchen/PotCommittedWrong",   // 냄비가 다 찬 순간, 그 레시피를 원하는 대기 주문이 없었음
        "Kitchen/ServedWrongOrder",    // 서빙구에 낸 요리를 원하는 대기 주문이 없었음
    };
    readonly int[] m_OrderMiss = new int[OrderMissStatNames.Length];

    void Awake()
    {
        if (env == null) env = GetComponent<KitchenEnv>();

        if (agents.Count == 0) agents.AddRange(GetComponentsInChildren<ChefAgent>(true));
        agents.Sort((a, b) => a.AgentIndex - b.AgentIndex);

        if (agents.Count != KitchenEnv.AgentCount)
        {
            Debug.LogError($"[{name}] ChefAgent가 {agents.Count}개다. {KitchenEnv.AgentCount}개여야 한다.");
            return;
        }

        m_Group = new SimpleMultiAgentGroup();
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i].AgentIndex != i)
                Debug.LogError($"[{name}] {agents[i].name} 의 AgentIndex가 {agents[i].AgentIndex}다. 0,1로 하나씩 배정할 것.");

            var partner = agents[(i + 1) % agents.Count];
            agents[i].Bind(this, env, partner);
            m_Group.RegisterAgent(agents[i]);
        }

        // 필드에 나와 있는 재료 수를 세려면 KitchenEnv가 셰프의 손을 볼 수 있어야 한다.
        env.BindAgents(agents.ToArray());

        m_Ready = true;
    }

    void Start()
    {
        ResetScene();
    }

    void FixedUpdate()
    {
        if (!m_Ready) return;

        // 주문 만료 패널티. KitchenEnv가 쌓아둔 것을 가져와서 보상으로 바꾼다.
        int expired = env.TakeExpiredOrderCount();
        if (expired > 0)
        {
            AddTeamReward(rewardOrderExpired * expired);
            m_OrdersExpired += expired;
        }

        if (env.IsGoalReached)
        {
            // 목표 수프 개수 달성 -> 성공 종료
            env.NoteEpisodeEnd($"목표 {env.TargetDishes}접시 달성! 새 라운드");
            SettleUnrealizedProgress();
            AddTeamReward(rewardGoalBonus);
            RecordStats(true);
            m_Group.EndGroupEpisode();
            ResetScene();
            return;
        }

        if (env.IsTimeUp)
        {
            env.NoteEpisodeEnd($"시간 초과 ({env.DishesServed}/{env.TargetDishes}접시) - 새 라운드");
            SettleUnrealizedProgress();
            RecordStats(false);
            // 타임아웃은 '실패'가 아니라 '중단'이다.
            // EndGroupEpisode로 끊으면 부트스트랩 없이 가치가 0으로 잘려서
            // value function이 "시간이 지나면 가치가 0" 이라고 잘못 배운다.
            m_Group.GroupEpisodeInterrupted();
            ResetScene();
        }
    }

    // ─────────────────────────── 팀 보상 ───────────────────────────

    // 손질/투입 같은 '진행 보상'은 서빙으로 이어졌을 때만 정당하다.
    // 지급은 즉시 하되(초반 학습에 기울기가 필요하다), 진행이 무산되면 도로 빼앗는다.
    // 그래서 지급액을 냄비에 기록해 둔다. OnPotDumped가 그걸 되돌린다.
    public float RewardPrepped => rewardPrepped;
    public float RewardIngredientInPot => rewardIngredientInPot;
    public float RewardServe => rewardServe;
    public float RewardGoalBonus => rewardGoalBonus;

    public void OnIngredientPrepped()
    {
        AddTeamReward(rewardPrepped);
    }

    public void OnIngredientPlacedInPot()
    {
        AddTeamReward(rewardIngredientInPot);
    }

    // 냄비를 비웠다. 그 배치에 지급됐던 진행 보상을 전부 회수한다.
    //
    // 이게 없으면 재료 획득 -> 손질(+0.2) -> 투입(+0.3) -> 비우기(-0.05) 무한 반복이
    // 30초 에피소드에서 셰프당 +9.5~12.5를 벌어, 정직하게 1접시 내는 것(+6.2)보다
    // 이득이 된다. 커리큘럼 lesson0 임계값(reward 3.0)도 서빙 0회로 통과해버린다.
    public void OnPotDumped(float refund)
    {
        ClawBack(refund);
    }

    // 진행이 무산됐다. 그 물건에 딸려 있던 진행 보상을 회수한다.
    //
    // 냄비를 비우는 경로(OnPotDumped)만으로는 부족하다. 요리를 완성해서 냄비에서
    // 꺼내버리면 크레딧이 냄비를 떠나므로, 그 요리를 주문에 없는데 제출하거나 버리면
    // 진행 보상 +1.0이 그대로 남는다. 크레딧이 요리를 따라가고 여기서 회수되어야 한다.
    public void OnProgressWasted(float credit)
    {
        ClawBack(credit);
    }

    // 무산된 진행 보상을 회수하는 유일한 통로. 회수액을 같이 센다.
    void ClawBack(float credit)
    {
        if (credit <= 0f) return;
        AddTeamReward(-credit);
        m_CreditClawedBack += credit;
    }

    public void OnDishServed()
    {
        AddTeamReward(rewardServe);
    }

    // 에피소드가 끝나기 직전, 서빙으로 실현되지 않은 진행 보상을 전부 회수한다.
    //
    // 리셋하면 손/카운터/냄비의 물건은 사라지는데 그 물건들에 지급된 보상은 남는다.
    // 그러면 '만들어서 쟁여두고 시간을 보내는' 것이 서빙 없이 팀 보상을 챙기는 길이 된다.
    // 목표 달성으로 끝나는 경우에도 똑같이 정산한다 - 서빙된 접시의 크레딧은 이미
    // 실현되어 빠져 있으므로, 남은 것은 전부 미실현분이다.
    void SettleUnrealizedProgress()
    {
        ClawBack(env.ConsumeUnrealizedCredit());
        ClawBackTransfer(env.ConsumeUnrealizedTransferCredit());
    }

    // 무산된 물건에 딸린 전달 보상을 회수하는 유일한 통로. 셰프 한 명당 금액을 받아
    // **양쪽 셰프의 개인 보상에서** 똑같이 뺀다 - 지급할 때 둘 다 받았기 때문이다.
    //
    // 팀 보상에서 빼면 안 된다. 학습 신호(POCA는 동료 개인 보상까지 더한다)로는 상쇄되지만
    // 커리큘럼 임계값은 개인 보상만 보므로, 관문은 여전히 서빙 없이 통과된다 (README 4-16).
    public void ClawBackTransfer(float perAgent)
    {
        if (perAgent <= 0f) return;
        foreach (var agent in agents) agent.AddReward(-perAgent);
        m_TransferClawedBack += perAgent;
    }

    // 학습 중에 '보상이 올랐다'가 무슨 뜻인지 해석할 수 있어야 한다.
    // 서빙이 0인데 보상이 오르는 상황을 TensorBoard에서 바로 구분하기 위한 통계다.
    void RecordStats(bool goalReached)
    {
        if (!Academy.IsInitialized) return;

        var stats = Academy.Instance.StatsRecorder;
        stats.Add("Kitchen/DishesServed", env.DishesServed);
        stats.Add("Kitchen/GoalReached", goalReached ? 1f : 0f);
        stats.Add("Kitchen/Transfers", m_Transfers);
        stats.Add("Kitchen/OrdersExpired", m_OrdersExpired);
        stats.Add("Kitchen/CreditClawedBack", m_CreditClawedBack);
        stats.Add("Kitchen/TransferClawedBack", m_TransferClawedBack);
        // 전달 한 번당 서빙이 몇 접시인가. 전달만 많고 서빙이 없으면 어뷰징 신호다.
        stats.Add("Kitchen/ServesPerTransfer", m_Transfers > 0 ? (float)env.DishesServed / m_Transfers : 0f);
        for (int i = 0; i < m_Chain.Length; i++) stats.Add(ChainStatNames[i], m_Chain[i]);
        for (int i = 0; i < m_OrderMiss.Length; i++) stats.Add(OrderMissStatNames[i], m_OrderMiss[i]);

        // 다음 에피소드를 위해 비우기 전에 값을 남긴다. 리셋 뒤에 읽어도 방금 끝난
        // 에피소드의 수치를 볼 수 있어야 한다 (회귀 검사와 사후 진단 모두 그걸 읽는다).
        LastEpisodeDishesServed = env.DishesServed;
        LastEpisodeCreditClawedBack = m_CreditClawedBack;
        LastEpisodeTransferClawedBack = m_TransferClawedBack;
        LastEpisodeTransfers = m_Transfers;
        System.Array.Copy(m_Chain, m_LastEpisodeChain, m_Chain.Length);

        ClearEpisodeStatsForTest();
    }

    // 진행 중인 에피소드의 누적 회수액.
    public float CreditClawedBack => m_CreditClawedBack;

    // 회귀 검사 전용. 시나리오마다 집계를 0에서 시작하게 한다.
    // (검사 시나리오는 대부분 에피소드를 끝내지 않으므로, 비워주지 않으면 앞 시나리오의
    //  회수액이 다음 시나리오 측정에 섞인다)
    // RecordStats도 에피소드 끝에 이걸로 비운다 - 리셋 항목이 두 곳에 따로 있으면
    // 새 지표를 한쪽에만 추가하는 실수가 생긴다.
    public void ClearEpisodeStatsForTest()
    {
        m_Transfers = 0;
        m_OrdersExpired = 0;
        m_CreditClawedBack = 0f;
        m_TransferClawedBack = 0f;
        System.Array.Clear(m_Chain, 0, m_Chain.Length);
        System.Array.Clear(m_OrderMiss, 0, m_OrderMiss.Length);
    }

    // 방금 끝난 에피소드의 수치. RecordStats가 리셋하기 직전에 채운다.
    public float LastEpisodeCreditClawedBack { get; private set; }
    public float LastEpisodeTransferClawedBack { get; private set; }
    public float TransferClawedBack => m_TransferClawedBack;
    public int LastEpisodeDishesServed { get; private set; }
    public int LastEpisodeTransfers { get; private set; }

    readonly int[] m_LastEpisodeChain = new int[ChainStatNames.Length];
    public int LastEpisodeChain(ChainStep step) => m_LastEpisodeChain[(int)step];

    // ChefAgent가 전달 보상을 실제로 지급했을 때 알려준다 (진단용 집계).
    public void NoteTransfer()
    {
        m_Transfers++;
    }

    // ChefAgent가 체인의 한 고리를 통과했을 때 알려준다 (진단용 집계).
    public void NoteChainStep(ChainStep step)
    {
        m_Chain[(int)step]++;
    }

    // 진행 중인 에피소드의 주문 불일치 횟수. 회귀 검사가 읽는다.
    public int OrderMissThisEpisode(OrderMiss miss) => m_OrderMiss[(int)miss];

    // ChefAgent가 주문과 맞지 않는 요리를 확정/제출했을 때 알려준다 (진단용 집계).
    public void NoteOrderMiss(OrderMiss miss)
    {
        m_OrderMiss[(int)miss]++;
    }

    // 팀 보상은 전부 여기를 지난다. 누적값을 같이 세기 위해서다.
    void AddTeamReward(float amount)
    {
        if (m_Group == null) return;
        m_Group.AddGroupReward(amount);
        TotalGroupReward += amount;
    }

    // 카운터 전달이 성립했을 때 '놓은 쪽'에 소급해서 주는 개인 보상.
    public void AwardPersonalReward(int agentIndex, float amount)
    {
        if (agentIndex < 0 || agentIndex >= agents.Count) return;
        agents[agentIndex].AddReward(amount);
    }

    // ─────────────────────────── 리셋 ───────────────────────────

    void ResetScene()
    {
        env.ResetEnv();
        foreach (var agent in agents) agent.ResetAgentState();
    }
}

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
        if (expired > 0) AddTeamReward(rewardOrderExpired * expired);

        if (env.IsGoalReached)
        {
            // 목표 수프 개수 달성 -> 성공 종료
            env.NoteEpisodeEnd($"목표 {env.TargetDishes}접시 달성! 새 라운드");
            AddTeamReward(rewardGoalBonus);
            m_Group.EndGroupEpisode();
            ResetScene();
            return;
        }

        if (env.IsTimeUp)
        {
            env.NoteEpisodeEnd($"시간 초과 ({env.DishesServed}/{env.TargetDishes}접시) - 새 라운드");
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
        if (refund > 0f) AddTeamReward(-refund);
    }

    // 진행이 무산됐다. 그 물건에 딸려 있던 진행 보상을 회수한다.
    //
    // 냄비를 비우는 경로(OnPotDumped)만으로는 부족하다. 요리를 완성해서 냄비에서
    // 꺼내버리면 크레딧이 냄비를 떠나므로, 그 요리를 주문에 없는데 제출하거나 버리면
    // 진행 보상 +1.0이 그대로 남는다. 크레딧이 요리를 따라가고 여기서 회수되어야 한다.
    public void OnProgressWasted(float credit)
    {
        if (credit > 0f) AddTeamReward(-credit);
    }

    public void OnDishServed()
    {
        AddTeamReward(rewardServe);
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

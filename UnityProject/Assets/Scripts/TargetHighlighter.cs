using Unity.MLAgents.Policies;
using UnityEngine;

// 사람이 플레이할 때 '지금 이걸 어디로 가져가야 하는지'를 보여준다.
//
// 예전 버전은 아이템 종류만 보고 목적지를 정했다. 그래서 RedSoup 주문만 남았는데
// 초록 재료를 들어도 냄비를 가리켰다 = 틀린 안내를 확신 있게 하고 있었다.
// 지금은 KitchenEnv.BuildPlan()이 정한 **목표 주문**을 기준으로 안내한다.
//
// 안내 결과(ChefGuidance)를 밖에서 읽을 수 있게 남겨둔다. OrderHud가 이걸 그대로
// 글로 옮겨 적는다 -> 깜빡이는 스테이션과 화면 글자가 절대 다른 말을 하지 않는다.
//
// 학습 중에는 아무 의미가 없고 프레임만 먹으므로, 에이전트가 Heuristic으로
// 움직이는 경우에만 켠다. 관측/보상에는 전혀 관여하지 않는다.
[RequireComponent(typeof(KitchenEnv))]
public class TargetHighlighter : MonoBehaviour
{
    public struct ChefGuidance
    {
        public Station Target;
        public StationHighlight.Kind Kind;
        public string Text;
    }

    KitchenEnv m_Env;
    ChefAgent[] m_Agents;
    StationHighlight[] m_All;
    ChefGuidance[] m_Guidance;

    public KitchenPlan Plan { get; private set; }

    public ChefGuidance GetGuidance(int agentIndex)
    {
        return m_Guidance != null && agentIndex >= 0 && agentIndex < m_Guidance.Length
            ? m_Guidance[agentIndex]
            : default;
    }

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        m_Agents = GetComponentsInChildren<ChefAgent>(true);
        m_All = GetComponentsInChildren<StationHighlight>(true);
        m_Guidance = new ChefGuidance[m_Agents.Length];

        enabled = IsAnyAgentHumanControlled();
    }

    bool IsAnyAgentHumanControlled()
    {
        foreach (var agent in m_Agents)
        {
            var behavior = agent.GetComponent<BehaviorParameters>();
            if (behavior != null && behavior.IsInHeuristicMode()) return true;
        }
        return false;
    }

    void LateUpdate()
    {
        foreach (var highlight in m_All) highlight.SetHighlight(StationHighlight.Kind.None);

        Plan = m_Env.BuildPlan();

        for (int i = 0; i < m_Agents.Length; i++)
        {
            var guidance = BuildGuidance(m_Agents[i]);
            m_Guidance[i] = guidance;

            if (guidance.Target == null) continue;
            var highlight = guidance.Target.GetComponent<StationHighlight>();
            if (highlight != null) highlight.SetHighlight(guidance.Kind);
        }
    }

    // ─────────────────────────── 안내 계산 ───────────────────────────

    ChefGuidance BuildGuidance(ChefAgent agent)
    {
        var held = agent.HeldItem;

        if (held.IsCookedDish()) return GuideCookedDish(agent, held);
        if (held == ItemType.EmptyPlate) return DropOff(agent, StationType.Pot, PlateText());
        if (held.IsIngredient()) return GuideIngredient(agent, held);
        return GuideEmptyHanded(agent);
    }

    // 완성 요리를 들었다. 주문이 있으면 제출, 없으면 버리는 것 말고 할 게 없다.
    ChefGuidance GuideCookedDish(ChefAgent agent, ItemType held)
    {
        bool wanted = m_Env.Orders.HasOrderFor(held);
        return DropOff(agent, StationType.ServingHatch,
            wanted ? "서빙구에 제출" : "주문에 없는 요리다 -> 버려라", !wanted);
    }

    string PlateText()
    {
        switch (Plan.Current)
        {
            case KitchenPlan.Step.Plate:   return "냄비에서 요리를 떠라";
            case KitchenPlan.Step.Cooking: return "냄비 앞에서 다 끓기를 기다려라";
            default:                       return "그릇을 든 채 냄비를 기다린다";
        }
    }

    // 재료를 들었다. 목표 주문이 그 색을 아직 원할 때만 조리 쪽으로 안내한다.
    ChefGuidance GuideIngredient(ChefAgent agent, ItemType held)
    {
        bool green = held.IsGreen();

        bool wanted = Plan.Current == KitchenPlan.Step.Gather && Plan.NeedsColor(green);
        if (!wanted)
            return DropOff(agent, StationType.ServingHatch,
                $"{ColorName(green)} 재료는 지금 주문에 필요 없다 -> 버려라", true);

        bool raw = held == ItemType.RawGreen || held == ItemType.RawRed;
        if (m_Env.NeedsPrep && raw)
            return DropOff(agent, green ? StationType.PrepGreen : StationType.PrepRed,
                $"{ColorName(green)} 손질대에서 손질해라");

        return DropOff(agent, StationType.Pot, "냄비에 넣어라");
    }

    // 빈손이다. 무엇을 가지러 갈지 정한다.
    ChefGuidance GuideEmptyHanded(ChefAgent agent)
    {
        // 1) 동료가 카운터에 올려둔 것 중 내가 쓸 수 있는 게 있으면 그게 최우선이다.
        //    IsItemUsefulFor가 이미 주문을 보고 판단하므로 쓸모없는 물건은 걸리지 않는다.
        foreach (var counter in m_Env.Counters)
        {
            if (counter.CounterItem == ItemType.None) continue;
            if (counter.CounterPlacedBy == agent.AgentIndex) continue;
            if (!m_Env.IsItemUsefulFor(agent.AgentIndex, counter.CounterItem)) continue;

            return new ChefGuidance
            {
                Target = counter,
                Kind = StationHighlight.Kind.Take,
                Text = "카운터에서 받아라"
            };
        }

        // 2) 냄비가 막다른 상태면 비우는 것이 최우선이다. 그대로 두면 아무 주문도 못 낸다.
        var pot = m_Env.Pot;
        if (Plan.PotIsDeadEnd && m_Env.CanAgentReach(pot, agent.AgentIndex))
        {
            return new ChefGuidance
            {
                Target = pot,
                Kind = StationHighlight.Kind.Trash,
                Text = "냄비 내용물로는 어떤 주문도 못 만든다 -> 비워라"
            };
        }

        // 3) 조리 중이거나 다 됐으면 빈 그릇을 준비한다. 미리 가져다 두는 게 이득이다.
        if (Plan.Current == KitchenPlan.Step.Cooking || Plan.Current == KitchenPlan.Step.Plate)
            return Fetch(agent, StationType.PlateStack, "빈 그릇을 가져와라");

        // 4) 재료를 모으는 중이면 필요한 색의 재료함으로.
        if (Plan.Current == KitchenPlan.Step.Gather)
        {
            bool green = ChooseColorFor(agent);
            return Fetch(agent, green ? StationType.GreenBox : StationType.RedBox,
                $"{ColorName(green)} 재료함에서 집어라");
        }

        // 5) 할 일이 없으면 카운터에 쌓인 쓸모없는 물건을 치운다.
        //    버릴 수 있는 곳이 서빙구(B 구역)뿐이라, 쓸모없는 물건은 B가 받아 가지 않으면
        //    카운터 칸을 영구히 차지한다. 칸이 다 막히면 전달 자체가 불가능해진다.
        if (m_Env.CanAgentReach(m_Env.GetStation(StationType.ServingHatch), agent.AgentIndex))
        {
            foreach (var counter in m_Env.Counters)
            {
                if (counter.CounterItem == ItemType.None) continue;
                if (m_Env.IsItemUsefulFor(agent.AgentIndex, counter.CounterItem)) continue;

                return new ChefGuidance
                {
                    Target = counter,
                    Kind = StationHighlight.Kind.Trash,
                    Text = "카운터에 쓸모없는 것이 있다 -> 집어서 서빙구에 버려라"
                };
            }
        }

        return new ChefGuidance { Text = "대기" };
    }

    // 두 색이 다 필요할 때(MixSoup) 누가 어느 쪽을 맡을지.
    // 자기 구역에서 손질까지 할 수 있는 색을 고른다 -> 전달 횟수가 줄어든다.
    bool ChooseColorFor(ChefAgent agent)
    {
        if (Plan.NeedGreen <= 0) return false;
        if (Plan.NeedRed <= 0) return true;

        bool canPrepGreen = m_Env.CanAgentReach(m_Env.GetStation(StationType.PrepGreen), agent.AgentIndex);
        return canPrepGreen;
    }

    // ─────────────────────────── 목적지 -> 실제 하이라이트 ───────────────────────────

    // 들고 있는 것을 놓을 곳. 내 구역에서 못 닿으면 '동료에게 넘겨라'로 바꾼다.
    //
    // trash면 카운터로 우회하더라도 **끝까지 '버리는 일'로 보여야 한다.**
    // 여기서 색과 문구가 평범한 전달로 바뀌면, 쓸모없는 물건을 넘기는 것이
    // 생산적인 협동처럼 보인다 - 화면이 틀린 말을 하는 가장 흔한 경로다.
    // (버릴 수 있는 곳은 서빙구뿐이고 그건 B 구역에만 있다)
    ChefGuidance DropOff(ChefAgent agent, StationType type, string text, bool trash = false)
    {
        var kind = trash ? StationHighlight.Kind.Trash : StationHighlight.Kind.Put;
        var station = m_Env.GetStation(type);

        if (m_Env.CanAgentReach(station, agent.AgentIndex))
            return new ChefGuidance { Target = station, Kind = kind, Text = text };

        // 넘겨야 한다. **빈 카운터 하나만** 고른다.
        // 4개를 전부 깜빡이면 "여기 놓아라"가 아니라 "아무데나"로 보인다.
        var counter = NearestEmptyCounter(agent);
        if (counter == null)
            return new ChefGuidance { Kind = kind, Text = "카운터가 꽉 찼다" };

        return new ChefGuidance
        {
            Target = counter,
            Kind = kind,
            Text = trash ? "버릴 것이다 -> 카운터로 넘겨서 버리게 해라" : "카운터로 동료에게 넘겨라"
        };
    }

    // 가지러 갈 곳. 내 구역에서 못 닿으면 동료가 넘겨줄 때까지 할 일이 없다.
    ChefGuidance Fetch(ChefAgent agent, StationType type, string text)
    {
        var station = m_Env.GetStation(type);

        if (!m_Env.CanAgentReach(station, agent.AgentIndex))
            return new ChefGuidance { Text = "동료가 넘겨줄 때까지 대기" };

        return new ChefGuidance { Target = station, Kind = StationHighlight.Kind.Take, Text = text };
    }

    Station NearestEmptyCounter(ChefAgent agent)
    {
        Station best = null;
        int bestDistance = int.MaxValue;

        foreach (var counter in m_Env.Counters)
        {
            if (counter.CounterItem != ItemType.None) continue;
            if (!m_Env.CanAgentReach(counter, agent.AgentIndex)) continue;

            int distance = Mathf.Abs(counter.Cell.x - agent.Cell.x) + Mathf.Abs(counter.Cell.y - agent.Cell.y);
            if (distance >= bestDistance) continue;

            best = counter;
            bestDistance = distance;
        }

        return best;
    }

    static string ColorName(bool green)
    {
        return green ? "초록" : "빨강";
    }
}

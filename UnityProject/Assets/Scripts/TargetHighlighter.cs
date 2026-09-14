using Unity.MLAgents.Policies;
using UnityEngine;

// 사람이 플레이할 때 '지금 이걸 어디로 가져가야 하는지'를 보여준다.
//   - 뭔가 들고 있으면  -> 그걸 넣어야 할 스테이션이 깜빡인다.
//     그 스테이션이 내 구역이 아니면 대신 (비어 있는) 카운터가 깜빡인다 = 넘겨라.
//   - 빈손인데 카운터에 나한테 쓸모있는 게 놓여 있으면 -> 그 카운터가 깜빡인다 = 받아라.
//
// 학습 중에는 아무 의미가 없고 프레임만 먹으므로, 에이전트가 Heuristic으로
// 움직이는 경우에만 켠다. 관측/보상에는 전혀 관여하지 않는다.
[RequireComponent(typeof(KitchenEnv))]
public class TargetHighlighter : MonoBehaviour
{
    KitchenEnv m_Env;
    ChefAgent[] m_Agents;
    StationHighlight[] m_All;
    bool m_Active;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        m_Agents = GetComponentsInChildren<ChefAgent>(true);
        m_All = GetComponentsInChildren<StationHighlight>(true);

        m_Active = IsAnyAgentHumanControlled();
        enabled = m_Active;
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
        foreach (var highlight in m_All) highlight.SetHighlighted(false);

        foreach (var agent in m_Agents)
        {
            if (agent.HeldItem == ItemType.None) HighlightPickup(agent);
            else HighlightDropOff(agent);
        }
    }

    // 들고 있는 것의 다음 목적지.
    bool TryGetTarget(ItemType held, out StationType target)
    {
        switch (held)
        {
            case ItemType.RawGreen:
                target = m_Env.NeedsPrep ? StationType.PrepGreen : StationType.Pot;
                return true;
            case ItemType.RawRed:
                target = m_Env.NeedsPrep ? StationType.PrepRed : StationType.Pot;
                return true;
            case ItemType.PrepGreen:
            case ItemType.PrepRed:
            case ItemType.EmptyPlate:
                target = StationType.Pot;
                return true;
            case ItemType.CookedDish:
                target = StationType.ServingHatch;
                return true;
            default:
                target = StationType.Counter;
                return false;
        }
    }

    void HighlightDropOff(ChefAgent agent)
    {
        if (!TryGetTarget(agent.HeldItem, out var targetType)) return;

        // 목적지가 내 구역이면 그 스테이션으로 간다.
        if (m_Env.GetStationZone(targetType) == agent.AgentIndex)
        {
            Highlight(m_Env.GetStation(targetType));
            return;
        }

        // 아니면 동료에게 넘겨야 한다. 놓을 수 있는 빈 카운터만 알려준다.
        foreach (var counter in m_Env.Counters)
            if (counter.CounterItem == ItemType.None) Highlight(counter);
    }

    void HighlightPickup(ChefAgent agent)
    {
        foreach (var counter in m_Env.Counters)
        {
            if (counter.CounterItem == ItemType.None) continue;
            if (counter.CounterPlacedBy == agent.AgentIndex) continue;
            if (!m_Env.IsItemUsefulFor(agent.AgentIndex, counter.CounterItem)) continue;
            Highlight(counter);
        }
    }

    static void Highlight(Station station)
    {
        if (station == null) return;
        var highlight = station.GetComponent<StationHighlight>();
        if (highlight != null) highlight.SetHighlighted(true);
    }
}

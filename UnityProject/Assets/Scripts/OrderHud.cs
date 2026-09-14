using Unity.MLAgents.Policies;
using UnityEngine;

// 사람이 플레이할 때 화면에 세 가지를 띄운다.
//
//   1. 무엇을 시켰나  - 대기 주문과 남은 시간, 그리고 **지금 목표로 삼은 주문**(▶)
//   2. 무엇을 하는 중인가 - 냄비에 뭐가 들어갔고 몇 초 남았는지
//   3. 무엇을 해야 하나  - 셰프별 다음 행동 한 줄
//
// 셋 다 TargetHighlighter가 계산한 것을 그대로 옮겨 적는다. 여기서 따로 판단하지 않는다.
// 깜빡이는 스테이션과 화면 글자가 다른 말을 하면 둘 다 못 믿게 된다.
//
// 학습 중에는 아무 의미가 없고 프레임만 먹으므로 Heuristic일 때만 켠다.
// 씬에 Canvas를 만들지 않고 OnGUI로 그린다. TrainingArea 프리팹 하나에만 붙이면
// 16개 복제본 전부에 딸려가는데, 겹쳐 그리면 읽을 수 없으므로 **먼저 잡은 하나만** 그린다.
[RequireComponent(typeof(KitchenEnv))]
public class OrderHud : MonoBehaviour
{
    static OrderHud s_Owner;

    static readonly Color ColorDim = new Color(0.70f, 0.70f, 0.72f);
    static readonly Color ColorUrgent = new Color(1.00f, 0.35f, 0.25f);
    static readonly Color ColorTake = new Color(0.45f, 1.00f, 0.55f);
    static readonly Color ColorPut = new Color(0.40f, 0.80f, 1.00f);
    static readonly Color ColorTrash = new Color(1.00f, 0.30f, 0.25f);

    KitchenEnv m_Env;
    TargetHighlighter m_Highlighter;
    ChefAgent[] m_Agents;
    GUIStyle m_Head;
    GUIStyle m_Line;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        m_Highlighter = GetComponent<TargetHighlighter>();
        m_Agents = GetComponentsInChildren<ChefAgent>(true);

        bool human = false;
        foreach (var agent in m_Agents)
        {
            var behavior = agent.GetComponent<BehaviorParameters>();
            if (behavior != null && behavior.IsInHeuristicMode()) { human = true; break; }
        }

        if (!human || (s_Owner != null && s_Owner != this)) { enabled = false; return; }
        s_Owner = this;
    }

    void OnDestroy()
    {
        if (s_Owner == this) s_Owner = null;
    }

    void EnsureStyles()
    {
        if (m_Head != null) return;
        m_Head = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        m_Line = new GUIStyle(GUI.skin.label) { fontSize = 15 };
    }

    void Label(GUIStyle style, Color color, string text)
    {
        style.normal.textColor = color;
        GUILayout.Label(text, style);
    }

    void OnGUI()
    {
        if (m_Env == null || m_Highlighter == null) return;
        EnsureStyles();

        var plan = m_Highlighter.Plan;

        GUI.Box(new Rect(12f, 12f, 340f, 260f), GUIContent.none);
        GUILayout.BeginArea(new Rect(24f, 20f, 320f, 250f));

        DrawOrders(plan);
        GUILayout.Space(6f);
        DrawPot(plan);
        GUILayout.Space(6f);
        DrawChefs();

        GUILayout.EndArea();
    }

    // 1) 무엇을 시켰나
    void DrawOrders(KitchenPlan plan)
    {
        var orders = m_Env.Orders;
        Label(m_Head, Color.white, $"주문   (완료 {m_Env.DishesServed} / {m_Env.TargetDishes})");

        for (int i = 0; i < OrderBoard.MaxSlots; i++)
        {
            var slot = orders.GetSlot(i);
            if (!slot.Active) continue;

            // 지금 만드는 중인 주문에만 ▶ 를 붙인다. 나머지는 흐리게.
            bool isTarget = plan.HasOrder && plan.OrderSlot == i;
            float ratio = Mathf.Clamp01(slot.Remaining / Mathf.Max(1f, orders.Duration));
            var color = isTarget
                ? Color.Lerp(ColorUrgent, Color.white, ratio)
                : Color.Lerp(ColorUrgent, ColorDim, ratio);

            Label(m_Line, color, $"{(isTarget ? "▶" : "  ")} {RecipeLabel(slot.Recipe)}   {slot.Remaining:0.0}s");
        }
    }

    // 2) 무엇을 하는 중인가
    void DrawPot(KitchenPlan plan)
    {
        var pot = m_Env.Pot;
        if (pot == null) return;

        Label(m_Head, Color.white, "냄비");

        string contents = pot.TotalCount == 0
            ? "(비어 있음)"
            : $"초록 {pot.GreenCount} / 빨강 {pot.RedCount}";

        switch (plan.Current)
        {
            case KitchenPlan.Step.Cooking:
            {
                float remain = Mathf.Max(0f, m_Env.CookTime - pot.CookTimer);
                Label(m_Line, ColorDim, $"{contents}  ->  {RecipeLabel(pot.CookedRecipe)} 끓는 중 {remain:0.0}s");
                break;
            }
            case KitchenPlan.Step.Plate:
                Label(m_Line, ColorTake, $"{contents}  ->  {RecipeLabel(pot.CookedRecipe)} 완성! 그릇으로 떠라");
                break;
            case KitchenPlan.Step.NoOrder:
                Label(m_Line, ColorTrash, $"{contents}  ->  어떤 주문도 못 만든다. 비워라");
                break;
            default:
                Label(m_Line, ColorDim,
                    $"{contents}  ->  {RecipeLabel(plan.Recipe)} 까지 초록 {plan.NeedGreen} / 빨강 {plan.NeedRed} 더");
                break;
        }
    }

    // 3) 무엇을 해야 하나
    void DrawChefs()
    {
        Label(m_Head, Color.white, "다음 행동");

        foreach (var agent in m_Agents)
        {
            var guidance = m_Highlighter.GetGuidance(agent.AgentIndex);
            string name = agent.AgentIndex == 0 ? "A(WASD)" : "B(방향키)";
            Label(m_Line, KindColor(guidance.Kind),
                $"{name}  [{ItemLabel(agent.HeldItem)}]  {guidance.Text}");
        }
    }

    static Color KindColor(StationHighlight.Kind kind)
    {
        switch (kind)
        {
            case StationHighlight.Kind.Take:  return ColorTake;
            case StationHighlight.Kind.Put:   return ColorPut;
            case StationHighlight.Kind.Trash: return ColorTrash;
            default:                          return ColorDim;
        }
    }

    static string RecipeLabel(RecipeType recipe)
    {
        switch (recipe)
        {
            case RecipeType.GreenSoup: return "GreenSoup(초록2)";
            case RecipeType.MixSoup:   return "MixSoup(초록1+빨강1)";
            default:                   return "RedSoup(빨강2)";
        }
    }

    static string ItemLabel(ItemType item)
    {
        switch (item)
        {
            case ItemType.None:        return "빈손";
            case ItemType.RawGreen:    return "생초록";
            case ItemType.RawRed:      return "생빨강";
            case ItemType.PrepGreen:   return "손질초록";
            case ItemType.PrepRed:     return "손질빨강";
            case ItemType.EmptyPlate:  return "빈그릇";
            case ItemType.CookedGreen: return "GreenSoup";
            case ItemType.CookedMix:   return "MixSoup";
            default:                   return "RedSoup";
        }
    }
}

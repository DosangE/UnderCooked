using UnityEngine;

// 사람이 플레이할 때 화면에 네 가지를 띄운다.
//
//   1. 지금 몇 라운드의 어디쯤인가 - 남은 시간 바, 완료 접시 수, 에피소드가 끝난 이유
//   2. 무엇을 시켰나             - 대기 주문 + 남은 시간 바, 목표 주문에 ▶
//   3. 무엇을 하는 중인가         - 냄비 내용물과 조리 진행 바
//   4. 무엇을 해야 하나           - 셰프별 다음 행동, 그리고 방금 무슨 일이 일어났는지
//
// 2~4는 TargetHighlighter가 계산한 것을 그대로 옮겨 적는다. 여기서 따로 판단하지 않는다.
// 깜빡이는 스테이션과 화면 글자가 다른 말을 하면 둘 다 못 믿게 된다.
//
// 사람이 플레이할 때는 KitchenEnv가 주방 하나만 남기므로 이 HUD도 하나뿐이다.
// 학습 중에는 KitchenEnv.HumanPlay가 false라서 통째로 꺼진다.
[RequireComponent(typeof(KitchenEnv))]
public class OrderHud : MonoBehaviour
{
    const float PanelWidth = 360f;
    const float BarWidth = 320f;

    static readonly Color ColorDim = new Color(0.68f, 0.68f, 0.72f);
    static readonly Color ColorUrgent = new Color(1.00f, 0.35f, 0.25f);
    static readonly Color ColorTake = new Color(0.45f, 1.00f, 0.55f);
    static readonly Color ColorPut = new Color(0.40f, 0.80f, 1.00f);
    static readonly Color ColorTrash = new Color(1.00f, 0.30f, 0.25f);

    [Tooltip("에피소드가 끝난 이유를 화면 가운데에 몇 초 동안 띄울지")]
    [SerializeField] float endBannerSeconds = 2.5f;

    KitchenEnv m_Env;
    TargetHighlighter m_Highlighter;
    ChefAgent[] m_Agents;
    GUIStyle m_Head;
    GUIStyle m_Line;
    GUIStyle m_Banner;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        m_Highlighter = GetComponent<TargetHighlighter>();
        m_Agents = GetComponentsInChildren<ChefAgent>(true);

        enabled = m_Env.HumanPlay;
    }

    void EnsureStyles()
    {
        if (m_Head != null) return;
        m_Head = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        m_Line = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        m_Banner = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    void Label(GUIStyle style, Color color, string text)
    {
        style.normal.textColor = color;
        GUILayout.Label(text, style);
    }

    // 남은 시간이나 진행률을 눈으로 보이게 한다. 숫자만 있으면 읽어야 알지만 바는 보면 안다.
    static void Bar(float ratio, Color color)
    {
        var rect = GUILayoutUtility.GetRect(BarWidth, 6f, GUILayout.Width(BarWidth));
        var previous = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height),
            Texture2D.whiteTexture);

        GUI.color = previous;
    }

    void OnGUI()
    {
        if (m_Env == null || m_Highlighter == null) return;
        EnsureStyles();

        var plan = m_Highlighter.Plan;

        GUI.Box(new Rect(12f, 12f, PanelWidth, 430f), GUIContent.none);
        GUILayout.BeginArea(new Rect(26f, 22f, PanelWidth - 28f, 420f));

        DrawRound();
        GUILayout.Space(8f);
        DrawOrders(plan);
        GUILayout.Space(8f);
        DrawPot(plan);
        GUILayout.Space(8f);
        DrawChefs();
        GUILayout.Space(8f);
        DrawLog();

        GUILayout.EndArea();

        DrawEndBanner();
    }

    // 1) 지금 라운드의 어디쯤인가
    void DrawRound()
    {
        float remain = Mathf.Max(0f, m_Env.EpisodeDuration - m_Env.EpisodeElapsed);
        Label(m_Head, Color.white,
            $"라운드  {remain:0.0}s 남음      접시 {m_Env.DishesServed} / {m_Env.TargetDishes}");
        Bar(m_Env.TimeRemainingNormalized,
            Color.Lerp(ColorUrgent, ColorTake, m_Env.TimeRemainingNormalized));
    }

    // 2) 무엇을 시켰나
    void DrawOrders(KitchenPlan plan)
    {
        var orders = m_Env.Orders;
        Label(m_Head, Color.white, "주문");

        for (int i = 0; i < OrderBoard.MaxSlots; i++)
        {
            var slot = orders.GetSlot(i);
            if (!slot.Active) continue;

            // 지금 만드는 중인 주문에만 ▶ 를 붙인다. 나머지는 흐리게.
            bool isTarget = plan.HasOrder && plan.OrderSlot == i;
            float ratio = Mathf.Clamp01(slot.Remaining / Mathf.Max(1f, orders.Duration));
            var color = isTarget ? Color.white : ColorDim;

            Label(m_Line, color, $"{(isTarget ? "▶" : "   ")} {RecipeLabel(slot.Recipe)}   {slot.Remaining:0.0}s");
            Bar(ratio, isTarget ? Color.Lerp(ColorUrgent, ColorTake, ratio) : ColorDim * 0.8f);
        }
    }

    // 3) 무엇을 하는 중인가
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
                float cookTime = Mathf.Max(0.0001f, m_Env.CookTime);
                float done = Mathf.Clamp01(pot.CookTimer / cookTime);
                Label(m_Line, ColorDim,
                    $"{contents} - {RecipeLabel(pot.CookedRecipe)} 끓는 중  {Mathf.Max(0f, cookTime - pot.CookTimer):0.0}s");
                Bar(done, ColorPut);
                break;
            }
            case KitchenPlan.Step.Plate:
                Label(m_Line, ColorTake, $"{contents} - {RecipeLabel(pot.CookedRecipe)} 완성! 빈 그릇으로 떠라");
                Bar(1f, ColorTake);
                break;
            case KitchenPlan.Step.NoOrder:
                Label(m_Line, ColorTrash, $"{contents} - 어떤 주문도 못 만든다. 비워라");
                Bar(0f, ColorTrash);
                break;
            default:
                Label(m_Line, ColorDim,
                    $"{contents} - {RecipeLabel(plan.Recipe)} 까지 초록 {plan.NeedGreen} / 빨강 {plan.NeedRed} 더");
                Bar((float)pot.TotalCount / RecipeTypeExtensions.Capacity, ColorDim);
                break;
        }
    }

    // 4a) 무엇을 해야 하나
    void DrawChefs()
    {
        Label(m_Head, Color.white, "다음 행동");

        foreach (var agent in m_Agents)
        {
            var guidance = m_Highlighter.GetGuidance(agent.AgentIndex);
            string who = agent.AgentIndex == 0 ? "A(WASD)" : "B(방향키)";
            Label(m_Line, KindColor(guidance.Kind),
                $"{who} [{ItemLabel(agent.HeldItem)}]  {guidance.Text}");
        }
    }

    // 4b) 방금 무슨 일이 일어났나.
    //     Interact가 실제로 먹혔는지를 이걸로 안다. 아무 반응이 없으면
    //     "냄비에 넣으면 처리가 되는 건지"를 알 방법이 없다.
    void DrawLog()
    {
        Label(m_Head, Color.white, "최근");

        var log = m_Env.HumanLog;
        for (int i = 0; i < log.Count; i++)
        {
            var entry = log[i];
            // 오래된 줄일수록 흐리게. 방금 일어난 것이 눈에 띄어야 한다.
            float age = Mathf.Clamp01((Time.time - entry.Time) / 4f);
            var color = Color.Lerp(LogColor(entry.Kind), ColorDim * 0.55f, age);
            Label(m_Line, color, entry.Text);
        }
    }

    // 위치가 갑자기 초기화되는 이유를 알려준다. 이게 없으면 "버그인가?" 하게 된다.
    void DrawEndBanner()
    {
        float age = Time.time - m_Env.LastEndTime;
        if (age > endBannerSeconds || string.IsNullOrEmpty(m_Env.LastEndReason)) return;

        float alpha = 1f - age / endBannerSeconds;
        m_Banner.normal.textColor = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 60f), m_Env.LastEndReason, m_Banner);
    }

    static Color LogColor(KitchenEnv.LogKind kind)
    {
        switch (kind)
        {
            case KitchenEnv.LogKind.Good: return ColorTake;
            case KitchenEnv.LogKind.Bad:  return ColorTrash;
            default:                      return Color.white;
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

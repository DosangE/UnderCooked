using Unity.MLAgents.Policies;
using UnityEngine;

// 사람이 플레이할 때 대기 주문을 화면에 띄운다.
//
// 학습 중에는 아무 의미가 없고 프레임만 먹으므로 TargetHighlighter와 같은 기준으로
// (에이전트가 Heuristic일 때만) 켠다. 관측/보상에는 전혀 관여하지 않는다.
//
// 씬에 Canvas를 만들지 않고 OnGUI로 그린다. TrainingArea 프리팹 하나에만 붙이면
// 16개 복제본 전부에 딸려가는데, 겹쳐 그리면 읽을 수 없으므로 **먼저 잡은 하나만** 그린다.
[RequireComponent(typeof(KitchenEnv))]
public class OrderHud : MonoBehaviour
{
    static OrderHud s_Owner;

    KitchenEnv m_Env;
    GUIStyle m_Style;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();

        bool human = false;
        foreach (var agent in GetComponentsInChildren<ChefAgent>(true))
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

    void OnGUI()
    {
        if (m_Env == null) return;

        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        var orders = m_Env.Orders;

        GUILayout.BeginArea(new Rect(16f, 16f, 280f, 200f));
        GUILayout.Label($"주문  ({m_Env.DishesServed} / {m_Env.TargetDishes})", m_Style);

        for (int i = 0; i < OrderBoard.MaxSlots; i++)
        {
            var slot = orders.GetSlot(i);
            if (!slot.Active) continue;

            // 남은 시간이 짧을수록 빨갛게. 어느 주문이 급한지 한눈에 보이게 한다.
            float ratio = Mathf.Clamp01(slot.Remaining / Mathf.Max(1f, orders.Duration));
            m_Style.normal.textColor = Color.Lerp(new Color(1f, 0.3f, 0.2f), Color.white, ratio);

            GUILayout.Label($"{RecipeLabel(slot.Recipe)}   {slot.Remaining:0.0}s", m_Style);
        }

        m_Style.normal.textColor = Color.white;
        GUILayout.EndArea();
    }

    static string RecipeLabel(RecipeType recipe)
    {
        switch (recipe)
        {
            case RecipeType.GreenSoup: return "GreenSoup  (초록 x2)";
            case RecipeType.MixSoup:   return "MixSoup    (초록+빨강)";
            default:                   return "RedSoup    (빨강 x2)";
        }
    }
}

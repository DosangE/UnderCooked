using System.Collections.Generic;
using UnityEngine;

// 카운터 전달칸에 놓인 물건을 작은 큐브로 띄운다. TrainingArea 루트에 붙인다.
//
// 카운터는 이 게임의 협동 통로 전체다. 두 구역이 물리적으로 분리되어 있어서
// 재료도 그릇도 완성 요리도 전부 여기를 지난다. 그런데 놓인 물건이 화면에
// 아무 표시도 없어서, **동료가 무엇을 넘겼는지 눈으로 알 수가 없었다.**
// 냄비는 내용물 슬롯을 띄워놓고 카운터만 빠져 있었다.
//
// 카운터마다 큐브 하나를 Start에서 만든다. 프리팹에 미리 넣어두면 카운터 개수나
// 위치가 바뀔 때마다 같이 손봐야 하는데, 런타임에 만들면 그럴 일이 없다.
//
// 색은 ItemColors 하나에서 가져온다 -> 손에 든 것과 카운터에 놓인 것이 항상 같은 색이다.
// 관측/보상에는 전혀 관여하지 않는다.
[RequireComponent(typeof(KitchenEnv))]
public class CounterContentsView : MonoBehaviour
{
    [Tooltip("큐브 한 변의 길이 (월드 단위)")]
    [SerializeField] float size = 0.3f;
    [Tooltip("카운터 중심에서 위로 얼마나 띄울지 (월드 단위)")]
    [SerializeField] float heightOffset = 0.6f;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    KitchenEnv m_Env;
    readonly List<Renderer> m_Views = new List<Renderer>();
    MaterialPropertyBlock m_Block;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        m_Block = new MaterialPropertyBlock();

        foreach (var counter in m_Env.Counters)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CounterItem";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(counter.transform, false);

            // 부모 스케일을 상쇄해서 카운터 크기와 무관하게 같은 크기로 보이게 한다.
            Vector3 parentScale = counter.transform.lossyScale;
            go.transform.localScale = Divide(Vector3.one * size, parentScale);
            go.transform.localPosition = Divide(new Vector3(0f, heightOffset, 0f), parentScale);

            m_Views.Add(go.GetComponent<Renderer>());
        }
    }

    static Vector3 Divide(Vector3 value, Vector3 scale)
    {
        return new Vector3(
            value.x / Mathf.Max(0.0001f, scale.x),
            value.y / Mathf.Max(0.0001f, scale.y),
            value.z / Mathf.Max(0.0001f, scale.z));
    }

    void LateUpdate()
    {
        var counters = m_Env.Counters;

        for (int i = 0; i < m_Views.Count && i < counters.Count; i++)
        {
            var item = counters[i].CounterItem;
            bool visible = item != ItemType.None;

            m_Views[i].enabled = visible;
            if (!visible) continue;

            var color = ItemColors.For(item);
            m_Views[i].GetPropertyBlock(m_Block);
            m_Block.SetColor(ColorId, color);
            m_Block.SetColor(EmissionId, color * 0.25f);
            m_Views[i].SetPropertyBlock(m_Block);
        }
    }
}

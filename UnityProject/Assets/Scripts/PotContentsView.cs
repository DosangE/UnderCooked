using UnityEngine;

// 냄비 위에 재료 슬롯 2개를 띄워서 '지금 무엇을 끓이는 중인지'를 월드에서 보이게 한다.
// 냄비에 붙인다.
//
// 화면 위 주문판(OrderHud)만으로는 부족하다. 플레이 중에는 냄비를 보고 있지 주문판을
// 보고 있지 않기 때문에, 냄비 자체가 내용물을 말해줘야 한다.
//
// 냄비 본체 색은 StationHighlight가 MaterialPropertyBlock으로 쓰고 있으므로
// 건드리지 않는다. 별도의 작은 큐브를 자식으로 만들어서 거기에만 색을 칠한다.
//
// 슬롯 큐브는 Awake에서 만든다. 프리팹에 미리 넣어두면 냄비의 위치/스케일이 바뀔 때마다
// 같이 손봐야 하는데, 부모 스케일을 상쇄해서 만들면 그럴 일이 없다.
//
// ★ 이건 렌더링일 뿐이고 관측에는 전혀 들어가지 않는다. 조리 완료를 눈으로 보여주는 것은
//   사람에게만 해당한다 - 정책은 CollectObservations에 적힌 것만 본다.
[RequireComponent(typeof(Station))]
public class PotContentsView : MonoBehaviour
{
    [Tooltip("슬롯 큐브 한 변의 길이 (월드 단위)")]
    [SerializeField] float slotSize = 0.22f;
    [Tooltip("냄비 중심에서 위로 얼마나 띄울지 (월드 단위)")]
    [SerializeField] float heightOffset = 0.65f;
    [Tooltip("슬롯 두 개 사이 간격 (월드 단위)")]
    [SerializeField] float spacing = 0.28f;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Station m_Pot;
    Renderer[] m_Slots;
    MaterialPropertyBlock m_Block;

    void Awake()
    {
        m_Pot = GetComponent<Station>();
        m_Block = new MaterialPropertyBlock();

        int count = RecipeTypeExtensions.Capacity;
        m_Slots = new Renderer[count];

        Vector3 parentScale = transform.lossyScale;
        float left = -(count - 1) * 0.5f * spacing;

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"PotSlot_{i}";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.SetParent(transform, false);
            // 부모(냄비) 스케일을 상쇄해서 냄비 크기와 무관하게 같은 크기로 보이게 한다.
            go.transform.localScale = Divide(Vector3.one * slotSize, parentScale);
            go.transform.localPosition = Divide(new Vector3(left + i * spacing, heightOffset, 0f), parentScale);

            m_Slots[i] = go.GetComponent<Renderer>();
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
        if (m_Pot == null || m_Slots == null) return;

        // 조리가 끝났으면 두 칸 모두 완성 색으로 깜빡인다.
        bool done = m_Pot.HasCookedDish;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f * Mathf.PI);

        for (int i = 0; i < m_Slots.Length; i++)
        {
            // 0번부터 초록, 그 다음 빨강 순으로 채운다.
            bool filledGreen = i < m_Pot.GreenCount;
            bool filledRed = !filledGreen && i < m_Pot.TotalCount;
            bool filled = filledGreen || filledRed;

            m_Slots[i].enabled = filled;
            if (!filled) continue;

            // 색은 ItemColors 한 곳에서 가져온다. 손에 든 재료와 같은 색으로 보여야 한다.
            Color color = ItemColors.For(filledGreen ? ItemType.PrepGreen : ItemType.PrepRed);
            if (done) color = Color.Lerp(color, ItemColors.Done, pulse);

            m_Slots[i].GetPropertyBlock(m_Block);
            m_Block.SetColor(ColorId, color);
            m_Block.SetColor(EmissionId, color * (done ? 0.8f : 0.25f));
            m_Slots[i].SetPropertyBlock(m_Block);
        }
    }
}

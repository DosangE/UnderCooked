using UnityEngine;

// 스테이션이 '지금 가야 할 곳'일 때 깜빡이게 한다. 사람이 플레이할 때만 쓰는 표시이고
// 관측이나 보상에는 전혀 관여하지 않는다.
//
// 깜빡임에 **의미별 색**을 붙인다. 전부 같은 색으로 깜빡이면 "집어라 / 놓아라 / 버려라"가
// 구분되지 않아서, 화면이 뭔가 알려주긴 하는데 무엇을 알려주는지 모르는 상태가 된다.
//
// 16개 환경 x 스테이션마다 renderer.material을 건드리면 머티리얼 인스턴스가 그만큼
// 복제된다. MaterialPropertyBlock으로 색만 덮어써서 공유 머티리얼을 유지한다.
[RequireComponent(typeof(Renderer))]
public class StationHighlight : MonoBehaviour
{
    public enum Kind
    {
        None = 0,
        Take,    // 여기서 집어라
        Put,     // 여기에 놓아라 / 넣어라
        Trash    // 여기에 버려라 (냄비 비우기 포함)
    }

    [Tooltip("초당 깜빡임 횟수")]
    [SerializeField] float pulsesPerSecond = 1.6f;
    [Tooltip("원래 색에서 표시 색 쪽으로 얼마나 물드는가 (0~1)")]
    [SerializeField] float maxBlend = 0.8f;

    [Header("의미별 색")]
    [SerializeField] Color takeColor = new Color(0.45f, 1.00f, 0.55f);
    [SerializeField] Color putColor = new Color(0.40f, 0.80f, 1.00f);
    [SerializeField] Color trashColor = new Color(1.00f, 0.30f, 0.25f);

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer m_Renderer;
    MaterialPropertyBlock m_Block;
    Color m_BaseColor;
    Kind m_Kind;
    bool m_NeedsWrite;

    void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
        m_Block = new MaterialPropertyBlock();
        m_BaseColor = m_Renderer.sharedMaterial != null ? m_Renderer.sharedMaterial.color : Color.white;
    }

    // TargetHighlighter가 매 프레임 정해준다.
    public void SetHighlight(Kind kind)
    {
        if (kind != m_Kind) m_NeedsWrite = true;
        m_Kind = kind;
    }

    Color TintFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Take:  return takeColor;
            case Kind.Put:   return putColor;
            case Kind.Trash: return trashColor;
            default:         return m_BaseColor;
        }
    }

    void LateUpdate()
    {
        // 꺼져 있고 이미 원래 색으로 돌려놨으면 아무것도 하지 않는다.
        if (m_Kind == Kind.None && !m_NeedsWrite) return;

        Color color = m_BaseColor;
        if (m_Kind != Kind.None)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f);
            color = Color.Lerp(m_BaseColor, TintFor(m_Kind), t * maxBlend);
        }

        m_Renderer.GetPropertyBlock(m_Block);
        m_Block.SetColor(ColorId, color);
        m_Block.SetColor(EmissionId, m_Kind != Kind.None ? color * 0.5f : Color.black);
        m_Renderer.SetPropertyBlock(m_Block);

        m_NeedsWrite = false;
    }
}

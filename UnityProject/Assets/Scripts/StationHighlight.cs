using UnityEngine;

// 스테이션이 '지금 가야 할 곳'일 때 깜빡이게 한다. 사람이 플레이할 때만 쓰는 표시이고
// 관측이나 보상에는 전혀 관여하지 않는다.
//
// 16개 환경 x 스테이션마다 renderer.material을 건드리면 머티리얼 인스턴스가 그만큼
// 복제된다. MaterialPropertyBlock으로 색만 덮어써서 공유 머티리얼을 유지한다.
[RequireComponent(typeof(Renderer))]
public class StationHighlight : MonoBehaviour
{
    [Tooltip("초당 깜빡임 횟수")]
    [SerializeField] float pulsesPerSecond = 1.6f;
    [Tooltip("원래 색에서 흰색 쪽으로 얼마나 밝아지는가 (0~1)")]
    [SerializeField] float maxBrightness = 0.7f;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer m_Renderer;
    MaterialPropertyBlock m_Block;
    Color m_BaseColor;
    bool m_Highlighted;
    bool m_NeedsWrite;

    void Awake()
    {
        m_Renderer = GetComponent<Renderer>();
        m_Block = new MaterialPropertyBlock();
        m_BaseColor = m_Renderer.sharedMaterial != null ? m_Renderer.sharedMaterial.color : Color.white;
    }

    // TargetHighlighter가 매 프레임 정해준다.
    public void SetHighlighted(bool on)
    {
        if (on != m_Highlighted) m_NeedsWrite = true;
        m_Highlighted = on;
    }

    void LateUpdate()
    {
        // 꺼져 있고 이미 원래 색으로 돌려놨으면 아무것도 하지 않는다.
        if (!m_Highlighted && !m_NeedsWrite) return;

        Color color = m_BaseColor;
        if (m_Highlighted)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f);
            color = Color.Lerp(m_BaseColor, Color.white, t * maxBrightness);
        }

        m_Renderer.GetPropertyBlock(m_Block);
        m_Block.SetColor(ColorId, color);
        m_Block.SetColor(EmissionId, m_Highlighted ? color * 0.5f : Color.black);
        m_Renderer.SetPropertyBlock(m_Block);

        m_NeedsWrite = false;
    }
}

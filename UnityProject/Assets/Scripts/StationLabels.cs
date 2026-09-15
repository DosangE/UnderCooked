using UnityEngine;

// 각 스테이션 위에 이름표를 띄운다. TrainingArea 루트에 붙인다.
//
// 스테이션이 색으로만 구분되면 "검정이 냄비, 노랑이 서빙구"를 외워야 한다.
// 외우기 전까지는 하이라이트가 어디를 가리켜도 그게 무슨 자리인지 모른다.
// 안내(TargetHighlighter)가 정확해도 이름이 없으면 읽을 수가 없다.
//
// 색은 정책의 입력이 아니다(관측은 Vector 103차원뿐, 센서 0개). 그러니 색 구분은
// 순전히 사람 문제이고, 사람 문제는 글자로 푸는 게 맞다.
//
// 월드 좌표를 화면 좌표로 투영해서 OnGUI로 그린다. 씬에 Canvas나 폰트 에셋을
// 만들지 않아도 되고, 이미 OrderHud가 같은 방식이라 손볼 곳이 한 군데로 모인다.
// 사람이 플레이할 때만 켠다(KitchenEnv가 주방 하나만 남기므로 라벨도 한 세트다).
[RequireComponent(typeof(KitchenEnv))]
public class StationLabels : MonoBehaviour
{
    [Tooltip("스테이션 중심에서 위로 얼마나 띄워 표시할지 (월드 단위)")]
    [SerializeField] float heightOffset = 1.0f;

    KitchenEnv m_Env;
    Camera m_Camera;
    GUIStyle m_Style;

    void Start()
    {
        m_Env = GetComponent<KitchenEnv>();
        enabled = m_Env.HumanPlay;
    }

    void OnGUI()
    {
        if (m_Env == null) return;
        if (m_Camera == null) m_Camera = Camera.main;
        if (m_Camera == null) return;

        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        foreach (StationType type in System.Enum.GetValues(typeof(StationType)))
        {
            if (type == StationType.Counter) continue;

            var station = m_Env.GetStation(type);
            if (station == null) continue;

            // 이번 에피소드에 안 쓰는 스테이션은 KitchenEnv가 숨긴다. 이름표도 같이 숨긴다.
            var renderer = station.GetComponent<Renderer>();
            if (renderer != null && !renderer.enabled) continue;

            Draw(station.transform.position, LabelFor(type));
        }

        foreach (var counter in m_Env.Counters) Draw(counter.transform.position, "전달칸");
    }

    void Draw(Vector3 worldPosition, string text)
    {
        Vector3 screen = m_Camera.WorldToScreenPoint(worldPosition + Vector3.up * heightOffset);
        if (screen.z <= 0f) return;   // 카메라 뒤

        var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 20f);

        // 배경이 밝든 어둡든 읽히도록 검은 테두리를 깔고 흰 글씨를 얹는다.
        m_Style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, m_Style);

        m_Style.normal.textColor = Color.white;
        GUI.Label(rect, text, m_Style);
    }

    static string LabelFor(StationType type)
    {
        switch (type)
        {
            case StationType.GreenBox:     return "초록 재료함";
            case StationType.RedBox:       return "빨강 재료함";
            case StationType.PrepA:        return "손질대 (A)";
            case StationType.PrepB:        return "손질대 (B)";
            case StationType.Pot:          return "냄비";
            case StationType.PlateStack:   return "그릇함";
            case StationType.ServingHatch: return "서빙구";
            default:                       return type.ToString();
        }
    }
}

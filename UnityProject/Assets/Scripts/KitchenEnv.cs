using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

// 주방 한 세트(TrainingArea)의 그리드 / 스테이션 / 조리 / 에피소드 타이머를 관리한다.
// TrainingArea 루트 오브젝트에 붙인다.
// 모든 좌표 계산은 이 오브젝트 기준 로컬 좌표다 -> 16개를 복제해도 관측이 동일하다.
public class KitchenEnv : MonoBehaviour
{
    // --- 레이아웃 문자 ---
    const char CharWall = '#';
    const char CharFloor = '.';
    const char CharSpawnA = 'A';
    const char CharSpawnB = 'B';
    const char CharCounter = 'C';
    const char CharGreenBox = 'G';
    const char CharRedBox = 'R';
    const char CharPrepA = 'a';
    const char CharPrepB = 'b';
    const char CharPot = 'P';
    const char CharPlateStack = 'D';
    const char CharServingHatch = 'S';

    // 맵 레이아웃. 배열 0번이 맵의 '위'(북쪽, row 최대)다. 파싱할 때 뒤집는다.
    //   # = 벽        . = 바닥       A/B = 셰프 스폰      C = 카운터 전달칸
    //   G = 초록 재료함   R = 빨강 재료함   (둘 다 경계 위 = 양쪽 구역에서 집을 수 있다)
    //   a = A 구역 손질대   b = B 구역 손질대   (둘 다 색을 가리지 않는다)
    //   P = 냄비(A 구역)   D = 그릇함(B 구역)   S = 서빙구(B 구역)
    //
    // 재료함과 손질대를 '색'이 아니라 '구역'으로 나눈 것은 '누가 무엇을 맡을지'를
    // 맵이 정해주지 않게 하려는 것이다. 색으로 나누면 주문이 색을 정하는 순간
    // 담당자까지 정해져서, RedSoup 주문에서는 B가 이동량의 81%를 지고 A는 냄비 앞에서
    // 받기만 한다(4.3:1). tools/measure_reach.py 로 잰 값이다.
    //
    // 협동은 색이 아니라 위치로 강제한다.
    //   - 냄비가 A 구역에만 있다   -> B가 손질한 재료는 카운터를 건너야 한다
    //   - 서빙구가 B 구역에만 있다 -> 완성 요리는 반드시 A에서 B로 건너가야 한다
    //   - 그릇함도 B 구역이다      -> 냄비를 뜨려면 빈 그릇이 A로 건너와야 한다
    static readonly string[] LayoutTopDown =
    {
        "####a####", // row 8  <- A 구역 손질대 (북쪽 벽)
        "#.......#", // row 7
        "#.......#", // row 6  <- Chef A 구역 (3행 x 7열)
        "#..A....P", // row 5     냄비는 A 구역 동쪽
        "#CCG#RCC#", // row 4  <- 경계: 카운터 4칸 + 재료함 2개(공용)
        "D..B....S", // row 3     그릇함 / 서빙구는 B 구역
        "#.......#", // row 2  <- Chef B 구역 (3행 x 7열)
        "#.......#", // row 1
        "####b####", // row 0  <- B 구역 손질대 (남쪽 벽)
    };

    // 행동 branch0의 1~4번(상/하/좌/우)에 대응하는 방향 벡터.
    // facing 인덱스(관측 one-hot 4)도 같은 순서를 쓴다.
    public static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),   // 0 North (+z)
        new Vector2Int(0, -1),  // 1 South (-z)
        new Vector2Int(-1, 0),  // 2 West  (-x)
        new Vector2Int(1, 0),   // 3 East  (+x)
    };

    public const int ZoneBlocked = -1;
    public const int AgentCount = 2;

    [Header("그리드")]
    [SerializeField] float cellSize = 1f;
    [SerializeField] float agentY = 0.5f;

    [Header("요리 규칙")]
    [Tooltip("손질대를 거친 재료만 냄비가 받는가. EnvironmentParameters의 needs_prep이 없을 때 쓰는 값")]
    [SerializeField] bool defaultNeedsPrep = true;
    [Tooltip("EnvironmentParameters의 cook_time이 없을 때 쓰는 값")]
    [SerializeField] float defaultCookTime = 5f;
    [Tooltip("EnvironmentParameters의 target_dishes가 없을 때 쓰는 값")]
    [SerializeField] int defaultTargetDishes = 2;

    [Header("주문")]
    [Tooltip("동시에 대기하는 주문 수. EnvironmentParameters의 order_slots가 없을 때 쓰는 값")]
    [SerializeField] int defaultOrderSlots = OrderBoard.MaxSlots;
    [Tooltip("주문에 나올 수 있는 레시피 수. RecipeType 순서대로 앞에서부터 풀린다. " +
             "1이면 GreenSoup만, 2면 +MixSoup, 3이면 +RedSoup")]
    [SerializeField] int defaultRecipePoolSize = RecipeTypeExtensions.Count;
    [Tooltip("주문 하나의 제한 시간(초). 넘기면 만료되고 팀 패널티가 붙는다")]
    [SerializeField] float defaultOrderDuration = 20f;
    [Tooltip("냄비 밖(손/카운터)에 동시에 존재할 수 있는 재료 개수. " +
             "넘으면 재료함 Interact가 막힌다 -> 한 접시씩 끝내게 만든다")]
    [SerializeField] int defaultMaxIngredients = 2;
    [Tooltip("동시에 나와 있을 수 있는 빈 그릇 수. 냄비가 하나뿐이라 실제로는 1개면 충분하고, " +
             "2는 '건너가는 중 1개 + 미리 꺼낸 1개'까지만 허용하는 여유다. " +
             "커리큘럼 대상이 아니라서 EnvironmentParameters로 받지 않는다")]
    [SerializeField] int maxPlates = 2;

    [Header("사람 플레이 (학습에는 영향 없음)")]
    [Tooltip("주방 하나만 남았을 때 카메라를 얼마나 위에 둘지 (셀 단위)")]
    [SerializeField] float cameraHeight = 11f;
    [Tooltip("카메라를 얼마나 뒤로 뺄지 (셀 단위)")]
    [SerializeField] float cameraBack = 8f;
    [Tooltip("카메라가 내려다보는 각도")]
    [SerializeField] float cameraPitch = 54f;

    [Header("에피소드")]
    [SerializeField] float episodeDuration = 45f;
    [Tooltip("끄면 항상 레이아웃의 A/B 마커 위치에서 시작한다 (디버깅용)")]
    [SerializeField] bool randomizeSpawn = true;

    int m_GridWidth;
    int m_GridHeight;
    int m_CounterRow;

    int[,] m_ZoneGrid;              // ZoneBlocked = 이동 불가, 0 = 북(ChefA), 1 = 남(ChefB)
    Station[,] m_StationGrid;       // 그 칸의 스테이션, 없으면 null
    List<Vector2Int>[] m_WalkableCells;
    Vector2Int[] m_DefaultSpawnCells;

    readonly List<Station> m_Counters = new List<Station>();
    readonly Dictionary<StationType, Station> m_TypedStations = new Dictionary<StationType, Station>();

    // 스테이션 종류별로 '어느 구역 에이전트가 쓸 수 있는지'. 등록 때 인접 칸에서 계산한다.
    int[] m_StationZoneByType;

    ChefAgent[] m_Agents;

    readonly OrderBoard m_Orders = new OrderBoard();

    bool m_NeedsPrep;
    float m_CookTime;
    int m_TargetDishes;
    int m_MaxIngredients;
    float m_EpisodeTimer;
    int m_ExpiredOrders;      // KitchenGroup이 가져가서 패널티로 바꾼다
    bool m_Initialized;

    // ── 사람 플레이 전용 ─────────────────────────────────────
    // 여기서부터는 전부 화면 표시용이다. 관측/보상/행동에 전혀 관여하지 않는다.

    static KitchenEnv s_SoloArea;   // 사람이 플레이할 때 살아남는 단 하나의 주방
    bool m_HumanPlay;
    bool m_HumanPlayResolved;

    public struct LogEntry
    {
        public float Time;      // 기록된 시각 (Time.time)
        public string Text;
        public LogKind Kind;
    }

    public enum LogKind { Neutral, Good, Bad }

    const int LogCapacity = 5;
    readonly List<LogEntry> m_Log = new List<LogEntry>(LogCapacity);
    public IReadOnlyList<LogEntry> HumanLog => m_Log;

    // 에피소드가 왜 끝났는지. 위치가 갑자기 초기화되는 이유를 사람이 알 수 있어야 한다.
    public string LastEndReason { get; private set; } = "";
    public float LastEndTime { get; private set; } = -99f;

    public void LogHumanEvent(string text, LogKind kind = LogKind.Neutral)
    {
        if (!HumanPlay) return;

        m_Log.Add(new LogEntry { Time = Time.time, Text = text, Kind = kind });
        if (m_Log.Count > LogCapacity) m_Log.RemoveAt(0);
    }

    public void NoteEpisodeEnd(string reason)
    {
        if (!HumanPlay) return;

        LastEndReason = reason;
        LastEndTime = Time.time;
        m_Log.Clear();
    }

    public int GridWidth => m_GridWidth;
    public int GridHeight => m_GridHeight;
    public float CellSize => cellSize;

    // 관측에도 그대로 넣어 정책이 지금 규칙을 알 수 있게 한다.
    public bool NeedsPrep => m_NeedsPrep;

    public OrderBoard Orders => m_Orders;

    // 사람용 화면 표시에서 조리 진행률을 계산하는 데 쓴다. 관측에는 쓰지 않는다.
    public float CookTime => m_CookTime;
    public float EpisodeDuration => episodeDuration;
    public float EpisodeElapsed => m_EpisodeTimer;

    // 같은 오브젝트에 붙은 다른 컴포넌트의 Start()가 KitchenEnv.Start()보다 먼저 돌 수 있다.
    // (Unity는 같은 오브젝트 안의 Start 순서를 보장하지 않는다)
    // 그래서 필드를 읽는 게 아니라 처음 물어볼 때 판정해서 캐시한다.
    // Start 시점이면 Agent.OnEnable이 이미 끝나 Academy가 초기화되어 있으므로 안전하다.
    public bool HumanPlay
    {
        get
        {
            if (m_HumanPlayResolved) return m_HumanPlay;

            m_HumanPlay = DetectHumanPlay();
            m_HumanPlayResolved = true;
            return m_HumanPlay;
        }
    }

    public int TargetDishes => m_TargetDishes;
    public int DishesServed { get; private set; }
    public bool IsGoalReached => DishesServed >= m_TargetDishes;

    public bool IsTimeUp => m_EpisodeTimer >= episodeDuration;
    public float TimeRemainingNormalized => Mathf.Clamp01(1f - m_EpisodeTimer / episodeDuration);

    public IReadOnlyList<Station> Counters => m_Counters;
    public Station Pot => GetStation(StationType.Pot);

    void Awake()
    {
        BuildGrid();
        RegisterStations();
        m_Initialized = true;
    }

    void Start()
    {
        // 사람이 플레이할 때는 주방 하나만 남긴다.
        //
        // 16개 TrainingArea의 셰프 32명이 전부 같은 키를 받으므로, 그대로 두면 16개
        // 주방이 동시에 움직이고 16세트의 하이라이트가 동시에 깜빡인다. 어느 것이
        // 내 주방인지 알 수 없다. 학습 때는 HumanPlay가 false라 아무 영향이 없다.
        if (HumanPlay)
        {
            if (!ClaimSoloArea())
            {
                gameObject.SetActive(false);
                return;
            }

            FrameCameraOnThisArea();
        }

        // 트레이너 없이 에디터에서 그냥 Play(휴리스틱 플레이) 해도 동작하도록 한 번 초기화한다.
        ResetEnv();
    }

    bool DetectHumanPlay()
    {
        foreach (var agent in GetComponentsInChildren<ChefAgent>(true))
        {
            var behavior = agent.GetComponent<BehaviorParameters>();
            if (behavior != null && behavior.IsInHeuristicMode()) return true;
        }
        return false;
    }

    bool ClaimSoloArea()
    {
        if (s_SoloArea != null && s_SoloArea != this) return false;
        s_SoloArea = this;
        return true;
    }

    // 씬 카메라는 16개 전체를 잡도록 놓여 있다. 주방 하나만 남겼으면 거기를 비춰야 한다.
    // 씬을 고치지 않고 런타임에만 옮긴다 -> 학습용 씬 배치는 그대로 둔다.
    void FrameCameraOnThisArea()
    {
        var camera = Camera.main;
        if (camera == null) return;

        camera.transform.position = transform.position
            + new Vector3(0f, cameraHeight * cellSize, -cameraBack * cellSize);
        camera.transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    void OnDestroy()
    {
        if (s_SoloArea == this) s_SoloArea = null;
    }

    void FixedUpdate()
    {
        if (!m_Initialized) return;

        m_EpisodeTimer += Time.fixedDeltaTime;

        var pot = Pot;
        if (pot != null) pot.TickCooking(Time.fixedDeltaTime, m_CookTime);

        // 만료된 주문 수를 쌓아두기만 한다. 보상으로 바꾸는 건 KitchenGroup의 일이고,
        // 두 FixedUpdate의 실행 순서에 결과가 흔들리지 않도록 누적 -> 소비 구조로 둔다.
        int expired = m_Orders.Tick(Time.fixedDeltaTime);
        if (expired > 0)
        {
            m_ExpiredOrders += expired;
            LogHumanEvent($"주문 {expired}건 시간 초과!", LogKind.Bad);
        }
    }

    // KitchenGroup이 매 FixedUpdate 가져간다. 읽으면 0으로 비워진다.
    public int TakeExpiredOrderCount()
    {
        int count = m_ExpiredOrders;
        m_ExpiredOrders = 0;
        return count;
    }

    // KitchenGroup이 에이전트를 묶을 때 알려준다. 필드 재료 수를 세는 데 필요하다.
    public void BindAgents(ChefAgent[] agents)
    {
        m_Agents = agents;
    }

    // ─────────────────────────── 초기화 ───────────────────────────

    void BuildGrid()
    {
        m_GridHeight = LayoutTopDown.Length;
        m_GridWidth = LayoutTopDown[0].Length;
        m_ZoneGrid = new int[m_GridWidth, m_GridHeight];
        m_StationGrid = new Station[m_GridWidth, m_GridHeight];

        m_WalkableCells = new List<Vector2Int>[AgentCount];
        m_DefaultSpawnCells = new Vector2Int[AgentCount];
        for (int i = 0; i < AgentCount; i++)
        {
            m_WalkableCells[i] = new List<Vector2Int>();
            m_DefaultSpawnCells[i] = new Vector2Int(-1, -1);
        }

        // 카운터가 놓인 행이 두 구역을 가르는 경계다.
        m_CounterRow = -1;
        for (int line = 0; line < m_GridHeight; line++)
        {
            if (LayoutTopDown[line].IndexOf(CharCounter) < 0) continue;
            m_CounterRow = (m_GridHeight - 1) - line;
            break;
        }
        if (m_CounterRow < 0)
        {
            Debug.LogError($"[{name}] 레이아웃에 카운터('{CharCounter}') 행이 없다.");
            return;
        }

        for (int row = 0; row < m_GridHeight; row++)
        {
            for (int col = 0; col < m_GridWidth; col++)
            {
                var cell = new Vector2Int(col, row);
                char c = LayoutCharAt(cell);
                int zone = ZoneBlocked;

                if (c == CharFloor || c == CharSpawnA || c == CharSpawnB)
                {
                    zone = row > m_CounterRow ? 0 : 1;
                    m_WalkableCells[zone].Add(cell);

                    if (c == CharSpawnA) m_DefaultSpawnCells[0] = cell;
                    else if (c == CharSpawnB) m_DefaultSpawnCells[1] = cell;
                }

                m_ZoneGrid[col, row] = zone;
            }
        }

        // 스폰 마커가 빠졌으면 그 구역의 첫 칸으로 대체한다.
        for (int i = 0; i < AgentCount; i++)
        {
            if (m_DefaultSpawnCells[i].x >= 0) continue;
            if (m_WalkableCells[i].Count == 0)
            {
                Debug.LogError($"[{name}] {i}번 구역에 걸을 수 있는 칸이 하나도 없다.");
                continue;
            }
            m_DefaultSpawnCells[i] = m_WalkableCells[i][0];
        }
    }

    // 씬에 배치된 Station들을 로컬 좌표로부터 그리드에 등록하고, 레이아웃과 어긋나면 에러를 낸다.
    // 씬과 코드가 따로 노는 사고(제일 잡기 어려운 종류)를 여기서 즉시 잡는다.
    void RegisterStations()
    {
        m_Counters.Clear();
        m_TypedStations.Clear();

        m_StationZoneByType = new int[System.Enum.GetValues(typeof(StationType)).Length];
        for (int i = 0; i < m_StationZoneByType.Length; i++) m_StationZoneByType[i] = ZoneBlocked;

        foreach (var station in GetComponentsInChildren<Station>(true))
        {
            Vector3 local = transform.InverseTransformPoint(station.transform.position);
            Vector2Int cell = LocalPositionToCell(local);

            if (!IsInsideGrid(cell))
            {
                Debug.LogError($"[{name}] {station.name} 이(가) 그리드 밖({cell})에 있다.");
                continue;
            }
            if (!TryLayoutStationType(LayoutCharAt(cell), out var expected))
            {
                Debug.LogError($"[{name}] {station.name} 의 칸 {cell} 은 레이아웃상 스테이션 자리가 아니다.");
                continue;
            }
            if (expected != station.Type)
            {
                Debug.LogError($"[{name}] {station.name} 타입 불일치: 레이아웃={expected}, 컴포넌트={station.Type}");
                continue;
            }

            station.Initialize(cell);
            m_StationGrid[cell.x, cell.y] = station;

            if (station.Type == StationType.Counter) m_Counters.Add(station);
            else m_TypedStations[station.Type] = station;
        }

        // 복제한 16개 환경이 같은 순서로 관측하도록 셀 기준으로 정렬한다.
        m_Counters.Sort((a, b) => a.Cell.x != b.Cell.x ? a.Cell.x - b.Cell.x : a.Cell.y - b.Cell.y);

        // 각 스테이션이 어느 구역에서 접근 가능한지 인접 칸으로 판정한다.
        // 재료함처럼 양쪽에서 닿는 것도 있지만, 이 값은 '소비하는' 스테이션에만 쓰이고
        // 소비 스테이션(손질대/냄비/서빙구)은 전부 한쪽 구역 전용이라 문제되지 않는다.
        foreach (var pair in m_TypedStations)
        {
            foreach (var dir in Directions)
            {
                var neighbour = pair.Value.Cell + dir;
                if (!IsInsideGrid(neighbour)) continue;
                int zone = m_ZoneGrid[neighbour.x, neighbour.y];
                if (zone == ZoneBlocked) continue;
                m_StationZoneByType[(int)pair.Key] = zone;
                break;
            }
        }

        // 레이아웃이 요구하는 스테이션이 전부 붙어 있는지 확인한다.
        for (int row = 0; row < m_GridHeight; row++)
        {
            for (int col = 0; col < m_GridWidth; col++)
            {
                var cell = new Vector2Int(col, row);
                if (!TryLayoutStationType(LayoutCharAt(cell), out _)) continue;
                if (m_StationGrid[col, row] != null) continue;
                Debug.LogError($"[{name}] 셀 {cell} 에 Station 컴포넌트가 없다. 씬 오브젝트를 확인할 것.");
            }
        }
    }

    char LayoutCharAt(Vector2Int cell)
    {
        return LayoutTopDown[(m_GridHeight - 1) - cell.y][cell.x];
    }

    static bool TryLayoutStationType(char c, out StationType type)
    {
        switch (c)
        {
            case CharGreenBox:     type = StationType.GreenBox;     return true;
            case CharRedBox:       type = StationType.RedBox;       return true;
            case CharPrepA:        type = StationType.PrepA;        return true;
            case CharPrepB:        type = StationType.PrepB;        return true;
            case CharPot:          type = StationType.Pot;          return true;
            case CharPlateStack:   type = StationType.PlateStack;   return true;
            case CharServingHatch: type = StationType.ServingHatch; return true;
            case CharCounter:      type = StationType.Counter;      return true;
            default:               type = StationType.Counter;      return false;
        }
    }

    // ─────────────────────────── 리셋 ───────────────────────────

    // 에피소드 시작마다 KitchenGroup이 호출한다.
    public void ResetEnv()
    {
        var envParams = Academy.Instance.EnvironmentParameters;
        m_NeedsPrep = envParams.GetWithDefault("needs_prep", defaultNeedsPrep ? 1f : 0f) >= 0.5f;
        m_TargetDishes = Mathf.Max(1, Mathf.RoundToInt(envParams.GetWithDefault("target_dishes", defaultTargetDishes)));
        m_CookTime = Mathf.Max(0f, envParams.GetWithDefault("cook_time", defaultCookTime));
        m_MaxIngredients = Mathf.Max(1, Mathf.RoundToInt(envParams.GetWithDefault("max_ingredients", defaultMaxIngredients)));

        m_Orders.Configure(
            Mathf.RoundToInt(envParams.GetWithDefault("order_slots", defaultOrderSlots)),
            Mathf.RoundToInt(envParams.GetWithDefault("recipe_pool_size", defaultRecipePoolSize)),
            envParams.GetWithDefault("order_duration", defaultOrderDuration));
        m_Orders.ResetBoard();

        foreach (var station in GetComponentsInChildren<Station>(true))
        {
            station.ConfigureRecipe(m_NeedsPrep);
            station.ResetState();
        }

        // 이번 에피소드에 쓰이지 않는 스테이션은 아예 숨긴다. 사람이 봐도, 정책이 봐도 헷갈리지 않게.
        // 손질대는 구역마다 하나씩이고 색과 무관하므로, 손질 단계에서는 둘 다 필요하다.
        SetStationVisible(StationType.PrepA, m_NeedsPrep);
        SetStationVisible(StationType.PrepB, m_NeedsPrep);
        SetStationVisible(StationType.RedBox, m_Orders.UsesRed);

        DishesServed = 0;
        m_EpisodeTimer = 0f;
        m_ExpiredOrders = 0;
    }

    void SetStationVisible(StationType type, bool visible)
    {
        var station = GetStation(type);
        if (station == null) return;

        foreach (var renderer in station.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
    }

    public Vector2Int GetSpawnCell(int agentIndex)
    {
        // 사람이 플레이할 때는 항상 같은 자리에서 시작한다. 에피소드가 바뀔 때마다
        // 무작위 칸으로 순간이동하면 "왜 갑자기 여기 있지?"가 되어 흐름이 끊긴다.
        if (!randomizeSpawn || HumanPlay) return m_DefaultSpawnCells[agentIndex];

        var cells = m_WalkableCells[agentIndex];
        return cells.Count == 0 ? m_DefaultSpawnCells[agentIndex] : cells[Random.Range(0, cells.Count)];
    }

    // ─────────────────────────── 좌표 변환 ───────────────────────────

    // 셀 (col,row) -> TrainingArea 기준 로컬 좌표. 그리드 중앙이 로컬 원점이 된다.
    public Vector3 CellToLocalPosition(Vector2Int cell)
    {
        float ox = (m_GridWidth - 1) * 0.5f;
        float oz = (m_GridHeight - 1) * 0.5f;
        return new Vector3((cell.x - ox) * cellSize, agentY, (cell.y - oz) * cellSize);
    }

    public Vector2Int LocalPositionToCell(Vector3 local)
    {
        float ox = (m_GridWidth - 1) * 0.5f;
        float oz = (m_GridHeight - 1) * 0.5f;
        return new Vector2Int(
            Mathf.RoundToInt(local.x / cellSize + ox),
            Mathf.RoundToInt(local.z / cellSize + oz));
    }

    // 관측용: 셀 좌표를 -1~1 범위로 정규화한다.
    public Vector2 NormalizeCell(Vector2Int cell)
    {
        float ox = (m_GridWidth - 1) * 0.5f;
        float oz = (m_GridHeight - 1) * 0.5f;
        return new Vector2((cell.x - ox) / ox, (cell.y - oz) / oz);
    }

    // ─────────────────────────── 질의 ───────────────────────────

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < m_GridWidth && cell.y >= 0 && cell.y < m_GridHeight;
    }

    // 그 에이전트가 이 칸으로 '이동'할 수 있는가. 자기 구역의 바닥만 true.
    public bool IsWalkable(Vector2Int cell, int agentIndex)
    {
        return IsInsideGrid(cell) && m_ZoneGrid[cell.x, cell.y] == agentIndex;
    }

    public Station GetStationAt(Vector2Int cell)
    {
        return IsInsideGrid(cell) ? m_StationGrid[cell.x, cell.y] : null;
    }

    public Station GetStation(StationType type)
    {
        return m_TypedStations.TryGetValue(type, out var station) ? station : null;
    }

    // 그 스테이션을 쓸 수 있는 구역. 경계 위(재료함/카운터)는 양쪽에서 닿으므로
    // 먼저 찾은 쪽이 나오고, 한쪽 전용 스테이션에서만 의미가 있다.
    public int GetStationZone(StationType type)
    {
        if (m_StationZoneByType == null) return ZoneBlocked;
        return m_StationZoneByType[(int)type];
    }

    // 그 에이전트가 이 스테이션을 쓸 수 있는가. 인접 칸 중 자기 구역 바닥이 하나라도 있으면 된다.
    //
    // GetStationZone은 경계 위 스테이션(재료함/카운터)에서 '먼저 찾은 쪽'만 돌려주므로
    // 양쪽에서 닿는 것을 판정할 수 없다. 사람용 안내는 그걸 정확히 알아야 한다.
    public bool CanAgentReach(Station station, int agentIndex)
    {
        if (station == null) return false;

        foreach (var dir in Directions)
            if (IsWalkable(station.Cell + dir, agentIndex)) return true;

        return false;
    }

    // 아직 서빙으로 실현되지 않은 진행 보상을 **전부** 걷어서 돌려주고 0으로 비운다.
    // 손에 든 것, 카운터에 놓인 것, 냄비에 들어 있는 것 세 군데를 모두 훑는다.
    //
    // 에피소드가 끝나면 남은 물건은 그냥 사라진다. 그런데 그 물건들에 딸려 지급된
    // 진행 보상은 남는다. 그러면 '만들어서 카운터와 손에 쟁여두고 시간을 보내는' 것이
    // 서빙 없이 팀 보상을 챙기는 길이 된다. 카운터 4칸 + 양손 2개 = 최대 6개까지
    // 쟁일 수 있어 무시할 양이 아니다.
    public float ConsumeUnrealizedCredit()
    {
        float total = 0f;

        var pot = Pot;
        if (pot != null) total += pot.ConsumeProgressCredit();

        foreach (var counter in m_Counters)
        {
            // 물건이 실제로 놓여 있는 칸만 센다. 집어간 뒤 남은 찌꺼기 값을 회수하면
            // 이미 실현된 진행까지 도로 빼앗게 된다 (집기 경로에서도 비우지만 이중 방어).
            if (counter.CounterItem == ItemType.None)
            {
                counter.SetCounterItemCredit(0f);
                continue;
            }

            total += counter.CounterItemCredit;
            counter.SetCounterItemCredit(0f);
        }

        if (m_Agents != null)
            foreach (var agent in m_Agents)
                if (agent != null) total += agent.ConsumeHeldCredit();

        return total;
    }

    // ConsumeUnrealizedCredit의 전달 보상판. 셰프 한 명당 금액으로 돌려준다.
    // 두 크레딧은 늘 같은 물건에 같이 붙어 다니므로 훑는 곳도 같다.
    public float ConsumeUnrealizedTransferCredit()
    {
        float total = 0f;

        var pot = Pot;
        if (pot != null) total += pot.ConsumeTransferCredit();

        foreach (var counter in m_Counters)
        {
            if (counter.CounterItem != ItemType.None) total += counter.CounterItemTransferCredit;
            counter.SetCounterItemTransferCredit(0f);
        }

        if (m_Agents != null)
            foreach (var agent in m_Agents)
                if (agent != null) total += agent.ConsumeHeldTransferCredit();

        return total;
    }

    // 그 에이전트가 쓸 수 있는 손질대. 구역마다 하나씩 있으므로 항상 하나 나온다.
    public Station GetPrepFor(int agentIndex)
    {
        var prepA = GetStation(StationType.PrepA);
        return CanAgentReach(prepA, agentIndex) ? prepA : GetStation(StationType.PrepB);
    }

    // ─────────────────────────── 사람용 안내 ───────────────────────────

    // '지금 이 주방이 무슨 요리를 만드는 중인가'. 하이라이트와 주문판이 같은 답을 보게 한다.
    // 사람이 플레이할 때만 쓰인다. 관측/보상/행동에는 전혀 관여하지 않는다.
    public KitchenPlan BuildPlan()
    {
        var plan = new KitchenPlan();
        var pot = Pot;
        if (pot == null) return plan;

        // 1) 조리가 확정된 냄비 -> 만들 요리는 이미 정해졌다. 주문은 그 요리로 역추적한다.
        if (pot.IsCommitted)
        {
            plan.Recipe = pot.CookedRecipe;
            plan.OrderSlot = m_Orders.FindMostUrgentFor(plan.Recipe.Dish());
            plan.HasOrder = plan.OrderSlot >= 0;
            plan.Current = pot.HasCookedDish ? KitchenPlan.Step.Plate : KitchenPlan.Step.Cooking;
            return plan;
        }

        // 2) 아직 재료를 받는 중 -> 지금 내용물로 만들 수 있는 주문 중 가장 급한 것을 목표로.
        plan.OrderSlot = m_Orders.FindMostUrgentReachable(pot.GreenCount, pot.RedCount);
        if (plan.OrderSlot < 0)
        {
            // 냄비에 든 것으로는 어떤 주문도 못 만든다. 비워야 한다.
            plan.Current = KitchenPlan.Step.NoOrder;
            return plan;
        }

        plan.HasOrder = true;
        plan.Recipe = m_Orders.GetSlot(plan.OrderSlot).Recipe;
        plan.NeedGreen = plan.Recipe.RequiredGreen() - pot.GreenCount;
        plan.NeedRed = plan.Recipe.RequiredRed() - pot.RedCount;
        plan.Current = KitchenPlan.Step.Gather;
        return plan;
    }

    // 냄비 밖에 나와 있는 재료 수. 손에 든 것과 카운터에 놓인 것만 센다.
    public int IngredientsInPlay()
    {
        int count = 0;

        if (m_Agents != null)
        {
            foreach (var agent in m_Agents)
                if (agent != null && agent.HeldItem.IsIngredient()) count++;
        }

        foreach (var counter in m_Counters)
            if (counter.CounterItem.IsIngredient()) count++;

        return count;
    }

    // 냄비 밖에 나와 있는 빈 그릇 수. 재료와 달리 그릇은 냄비가 소비하지 않으므로
    // (요리를 담을 때만 없어진다) 따로 센다.
    public int PlatesInPlay()
    {
        int count = 0;

        if (m_Agents != null)
        {
            foreach (var agent in m_Agents)
                if (agent != null && agent.HeldItem == ItemType.EmptyPlate) count++;
        }

        foreach (var counter in m_Counters)
            if (counter.CounterItem == ItemType.EmptyPlate) count++;

        return count;
    }

    // 재료함/그릇함을 더 열 수 있는가. 필드에 너무 많이 나와 있으면 막아서
    // 한 접시씩 끝내게 만든다.
    //
    // '지금 주문에 필요한 색인가'까지는 여기서 막지 않는다. 그건 마스크가 아니라
    // 보상이 가르쳐야 할 것이고, 막아버리면 "잘못 가져오는 실수"라는 학습 대상 자체가 사라진다.
    // 다만 이번 에피소드에 아예 등장하지 않는 색(lesson0의 빨강)은 구조적으로 막는다.
    //
    // ★ 그릇함에도 상한이 필요하다. 예전에는 그릇이 어떤 상한에도 걸리지 않아서
    //   카운터 4칸 + 양손에 빈 그릇을 쟁여두는 것만으로 꺼내기 보상(+0.05)을 공짜로
    //   챙길 수 있었다. 회수 대상도 아니다(그릇에는 진행 크레딧이 없다).
    //   상한을 걸어도 막다른 상태는 생기지 않는다 - 그릇은 서빙구에 버려서 되돌릴 수 있다.
    bool SourceAllowed(Station station)
    {
        if (station.Type == StationType.RedBox && !m_Orders.UsesRed) return false;
        if (station.Type == StationType.PlateStack) return PlatesInPlay() < maxPlates;
        if (station.Type != StationType.GreenBox && station.Type != StationType.RedBox) return true;
        return IngredientsInPlay() < m_MaxIngredients;
    }

    // Action Masking용: 이 칸을 향해 Interact 하면 뭐라도 일어나는가.
    public bool CanInteractAt(Vector2Int cell, ItemType heldItem)
    {
        var station = GetStationAt(cell);
        return station != null && station.CanInteract(heldItem) && SourceAllowed(station);
    }

    // 그 스테이션이 이 아이템을 '소비'하는가 (재료함/그릇함은 생산만 하므로 false).
    //
    // ★ 손질대는 일부러 빠져 있다.
    //   손질대가 색을 안 가리게 되면서 A 구역 손질대도 B 구역 손질대도 모든 생재료를
    //   받는다. 손질대를 소비자로 세면 생재료가 **양쪽 구역 모두에서 쓸모 있는 것**이
    //   되고, 그 순간 README 4-1(b)의 A<->B 핑퐁 어뷰징이 그대로 되살아난다.
    //   (예전에는 빨강 손질대가 B에만 있어서 생빨강은 B 방향으로만 쓸모가 있었다)
    //
    //   실제로도 생재료를 넘길 이유가 없다. 자기 구역 손질대에서 손질한 뒤 넘기면 된다.
    //   그래서 전달 보상은 '손질을 마친 재료'와 '빈 그릇'과 '완성 요리'에만 붙는다.
    bool StationConsumes(StationType type, ItemType item)
    {
        switch (type)
        {
            case StationType.Pot:
                if (item == ItemType.EmptyPlate) return true;
                if (!item.IsIngredient()) return false;
                // 냄비가 지금 받는 형태여야 쓸모가 있다. Station.PotAccepts와 같은 규칙.
                bool raw = item == ItemType.RawGreen || item == ItemType.RawRed;
                return m_NeedsPrep ? !raw : raw;

            case StationType.ServingHatch: return item.IsCookedDish();
            default:                       return false;
        }
    }

    // 이 아이템이 그 에이전트 구역에서 실제로 쓸모가 있는가.
    //
    // 카운터 전달 보상을 '쓸모 있는 방향'으로만 제한하기 위해 필요하다.
    // 이게 없으면 A가 놓고 B가 집고 B가 놓고 A가 집는 핑퐁만으로 보상을 긁을 수 있고,
    // 그게 제대로 서빙하는 것보다 이득이 되어 협동 시늉만 하는 정책으로 수렴한다.
    // 되돌아가는 방향에는 보상이 없으므로 핑퐁 이득이 절반 이하로 떨어진다.
    public bool IsItemUsefulFor(int agentIndex, ItemType item)
    {
        if (item == ItemType.None || m_StationZoneByType == null) return false;

        // 주문과 무관한 물건은 전달해봐야 소용없다. 이 조건이 없으면 대기 주문이 전부
        // GreenSoup인데 빨강 재료를 서로 넘기는 것만으로 전달 보상을 긁을 수 있다.
        if (!IsWantedNow(item)) return false;

        foreach (var pair in m_TypedStations)
        {
            if (!StationConsumes(pair.Key, item)) continue;
            if (m_StationZoneByType[(int)pair.Key] == agentIndex) return true;
        }
        return false;
    }

    // 지금 대기 중인 주문들을 기준으로 이 물건이 쓸모가 있는가.
    public bool IsWantedNow(ItemType item)
    {
        // 빈 그릇은 '담을 요리가 있을 때'만 쓸모가 있다.
        //
        // ★ 예전에는 무조건 true였다. 그러면 그릇은 어떤 방어에도 안 걸린다 -
        //   max_ingredients는 재료만 세고, 그릇에는 회수할 진행 크레딧도 없다.
        //   그래서 'B가 새 그릇을 꺼내(+0.05) 카운터로 넘기면(양쪽 +0.15) B가 되받아
        //   서빙구에 버린다(-0.2)'가 사이클당 +0.15짜리 무한 반복이 됐다.
        //   450 decision이면 서빙 0회로 개인 보상이 당시 랜덤 기준선(-0.78, README 4-19
        //   수정 전 측정값)에서 0 근처까지 올라가, target_dishes lesson0 임계값(-0.3)을
        //   서빙 없이 통과했다. 현재 기준선은 .claude/docs/TRAINING.md 3절.
        //   README 4-12와 정확히 같은 실패인데 그릇 쪽에만 남아 있었다.
        //
        //   IsCommitted를 기준으로 삼는 것이 안전한 이유: 냄비 내용물 개수는 이미 관측에
        //   들어 있고(그리고 조리가 끝나도 안 변한다), 개수가 2 = IsCommitted이므로
        //   정책이 이미 볼 수 있는 정보다. 숨긴 정보를 보상으로 흘리는 것이 아니다.
        //
        //   담을 요리가 생기기 전에 미리 그릇을 건네두는 것에는 보상이 안 붙는다.
        //   그건 감수한다 - 조리 시간(최대 5초 = 50 decision)이면 그 뒤에 건네도 늦지 않고,
        //   무조건 주면 위의 반복이 다시 열린다.
        if (item == ItemType.EmptyPlate) return Pot != null && Pot.IsCommitted;
        if (item.IsCookedDish()) return m_Orders.HasOrderFor(item);
        if (item.IsIngredient()) return m_Orders.WantsColor(item.IsGreen(), PlanningGreen, PlanningRed);
        return false;
    }

    // '다음에 냄비가 어떤 상태에서 출발하는가'. 조리가 확정된 냄비는 어차피 비워진 뒤
    // 새 배치가 시작되므로 0,0으로 본다. 재료를 미리 손질해 두는 행동을 벌하지 않기 위해서다.
    int PlanningGreen => Pot != null && !Pot.IsCommitted ? Pot.GreenCount : 0;
    int PlanningRed => Pot != null && !Pot.IsCommitted ? Pot.RedCount : 0;

    // ─────────────────────────── 상호작용 ───────────────────────────

    // 에이전트가 cell 을 향해 Interact 했을 때. 실제 상태 변경까지 여기서 일어난다.
    public InteractOutcome TryInteract(int agentIndex, Vector2Int cell, ItemType heldItem,
                                       bool heldTransferred = false, float heldCredit = 0f,
                                       float heldTransferCredit = 0f)
    {
        var outcome = new InteractOutcome
        {
            Result = InteractResult.Nothing,
            NewHeldItem = heldItem,
            NewItemTransferred = heldTransferred,
            NewItemCredit = heldCredit,
            NewItemTransferCredit = heldTransferCredit,
            TransferPartnerIndex = -1
        };

        var station = GetStationAt(cell);
        if (station == null) return outcome;

        // Heuristic 플레이는 Action Mask를 거치지 않으므로 재료 수 제한을 여기서도 막는다.
        if (!SourceAllowed(station)) return outcome;

        // Interact가 카운터 상태를 지워버리므로 미리 받아둔다.
        int placedBy = station.Type == StationType.Counter ? station.CounterPlacedBy : -1;
        bool counterTransferred = station.Type == StationType.Counter && station.CounterItemTransferred;
        float counterCredit = station.Type == StationType.Counter ? station.CounterItemCredit : 0f;
        float counterTransferCredit = station.Type == StationType.Counter ? station.CounterItemTransferCredit : 0f;

        outcome.Result = station.Interact(agentIndex, heldItem, out var newHeld);
        outcome.NewHeldItem = newHeld;

        if (outcome.Result == InteractResult.TookFromCounter) outcome.TransferPartnerIndex = placedBy;

        // 물건의 '이미 전달 보상을 받았다' 기록을 손과 카운터 사이에서 옮긴다.
        UpdateItemRecords(station, outcome.Result, heldTransferred, heldCredit, heldTransferCredit,
                          counterTransferred, counterCredit, counterTransferCredit, ref outcome);

        // Station은 주문표를 모른다. 주문과의 대조는 전부 여기서 한다.
        switch (outcome.Result)
        {
            case InteractResult.PlacedInPot:
            case InteractResult.PlacedInPotWrong:
                // 넣고 난 뒤의 냄비 내용물로 아직 어떤 대기 주문이든 만들 수 있는가.
                // 조리가 확정된 경우(재료가 다 찬 경우)는 만들어질 요리 자체를 주문과 대조한다.
                bool ok = station.IsCommitted
                    ? m_Orders.HasOrderFor(station.CookedRecipe.Dish())
                    : m_Orders.IsReachable(station.GreenCount, station.RedCount);
                if (!ok) outcome.Result = InteractResult.PlacedInPotWrong;
                break;

            case InteractResult.Served:
                // 그 요리를 주문한 손님이 있어야 점수다. 없으면 완성품이어도 버린 것이다.
                if (m_Orders.TryConsume(heldItem)) DishesServed++;
                else outcome.Result = InteractResult.ServedWrongOrder;
                break;
        }

        if (HumanPlay) LogInteraction(agentIndex, station, outcome.Result, heldItem);

        return outcome;
    }

    // 물건마다 따라다니는 '이미 전달 보상을 받았다' 기록을 갱신한다.
    //
    // 손질대나 냄비를 거치면 다른 물건이 되므로 기록이 지워진다 -> 정상 파이프라인의
    // 전달은 매번 보상받는다. 반면 카운터를 왕복하기만 하면 기록이 그대로 따라다녀서
    // 두 번째 건널 때부터는 보상이 없다.
    // 물건마다 따라다니는 두 가지 기록을 갱신한다.
    //   (1) 이미 전달 보상을 받았는가        -> 같은 물건 왕복으로 보상을 긁는 것 차단
    //   (2) 딸려 있는 진행 보상 크레딧       -> 서빙이 무산되면 회수하기 위해
    //
    // 손질대나 냄비를 거치면 '다른 물건'이 되므로 (1)은 지워진다 -> 정상 파이프라인의
    // 전달은 매번 보상받는다. 반면 카운터를 왕복하기만 하면 기록이 따라다녀서
    // 두 번째 건널 때부터는 보상이 없다.
    //
    // (2)는 반대로 냄비 -> 완성 요리 -> 카운터 -> 동료까지 **계속 따라간다.**
    // 서빙이 성공해야 비로소 확정되기 때문이다.
    //
    // (3) 딸려 있는 전달 보상 크레딧도 (2)와 똑같이 따라간다. 다른 점은 냄비에 넣는 순간
    //     여기서 바로 냄비로 옮긴다는 것뿐이다 (진행 크레딧은 투입 보상과 합산해야 해서
    //     ChefAgent가 옮긴다). 요리를 뜰 때는 냄비 몫 + 그 요리를 담은 **그릇의 몫**을
    //     합친다 - 건너온 그릇은 그 요리의 일부가 되었기 때문이다.
    static void UpdateItemRecords(Station station, InteractResult result,
                                  bool heldTransferred, float heldCredit, float heldTransferCredit,
                                  bool counterTransferred, float counterCredit, float counterTransferCredit,
                                  ref InteractOutcome outcome)
    {
        switch (result)
        {
            // 재료함/그릇함에서 새로 꺼냈거나 손질했다 -> 전달 기록 초기화, 크레딧 없음
            case InteractResult.PickedFromSource:
            case InteractResult.Prepped:
                outcome.NewItemTransferred = false;
                outcome.NewItemCredit = heldCredit;   // 손질은 크레딧을 유지(냄비에서 합산된다)
                outcome.NewItemTransferCredit = heldTransferCredit;
                break;

            // 냄비에서 요리를 떴다 -> 새 물건이지만 **냄비의 크레딧을 그대로 물려받는다**
            case InteractResult.TookDishFromPot:
                outcome.NewItemTransferred = false;
                outcome.NewItemCredit = station.ConsumeProgressCredit();
                outcome.NewItemTransferCredit = station.ConsumeTransferCredit() + heldTransferCredit;
                break;

            // 카운터에서 집었다 -> 카운터가 들고 있던 기록을 **옮겨온다**.
            // 카운터 쪽을 비우지 않으면 같은 크레딧이 손과 카운터 양쪽에 남아서
            // 종료 정산 때 두 번 회수된다. 정상 서빙까지 벌하게 되는 경로였다.
            case InteractResult.TookFromCounter:
            case InteractResult.TookOwnFromCounter:
                outcome.NewItemTransferred = counterTransferred;
                outcome.NewItemCredit = counterCredit;
                outcome.NewItemTransferCredit = counterTransferCredit;
                station.SetCounterItemTransferred(false);
                station.SetCounterItemCredit(0f);
                station.SetCounterItemTransferCredit(0f);
                break;

            // 카운터에 놓았다 -> 손이 들고 있던 기록을 카운터에 넘긴다
            case InteractResult.PlacedOnCounter:
                station.SetCounterItemTransferred(heldTransferred);
                station.SetCounterItemCredit(heldCredit);
                station.SetCounterItemTransferCredit(heldTransferCredit);
                outcome.NewItemTransferred = false;   // 손은 비었다
                outcome.NewItemCredit = 0f;
                outcome.NewItemTransferCredit = 0f;
                break;

            // 냄비에 넣었다 -> 전달 크레딧은 냄비 배치로 옮긴다
            case InteractResult.PlacedInPot:
            case InteractResult.PlacedInPotWrong:
                station.AddTransferCredit(heldTransferCredit);
                outcome.NewItemTransferred = false;
                outcome.NewItemCredit = 0f;
                outcome.NewItemTransferCredit = 0f;
                break;

            // 물건이 손에서 사라진다 -> 손의 기록도 비운다.
            // (크레딧의 확정/회수는 ChefAgent가 결과별로 처리한다)
            case InteractResult.Served:
            case InteractResult.ServedWrongOrder:
            case InteractResult.Wasted:
                outcome.NewItemTransferred = false;
                outcome.NewItemCredit = 0f;
                outcome.NewItemTransferCredit = 0f;
                break;
        }
    }

    // 누른 것이 실제로 먹혔는지를 사람이 알 수 있게 한 줄 남긴다.
    // "냄비에 넣으면 처리가 되는 건지 모르겠다"가 여기서 해결된다.
    void LogInteraction(int agentIndex, Station station, InteractResult result, ItemType heldItem)
    {
        string who = agentIndex == 0 ? "A" : "B";
        var pot = Pot;

        switch (result)
        {
            case InteractResult.Nothing:
                // 마스킹 덕분에 정책에서는 안 나오지만, 사람은 아무 때나 누를 수 있다.
                LogHumanEvent($"{who}: 여기서는 할 수 있는 게 없다", LogKind.Bad);
                break;

            case InteractResult.PickedFromSource:
                LogHumanEvent($"{who}: {station.Type} 에서 집었다");
                break;

            case InteractResult.Prepped:
                LogHumanEvent($"{who}: 손질했다", LogKind.Good);
                break;

            case InteractResult.PlacedInPot:
                LogHumanEvent(pot != null && pot.IsCommitted
                    ? $"{who}: 냄비에 넣었다 -> 재료 다 찼다! {pot.CookedRecipe} 끓기 시작"
                    : $"{who}: 냄비에 넣었다 (초록 {pot?.GreenCount} / 빨강 {pot?.RedCount})", LogKind.Good);
                break;

            case InteractResult.PlacedInPotWrong:
                LogHumanEvent($"{who}: 냄비에 넣었지만 이걸로는 어떤 주문도 못 만든다", LogKind.Bad);
                break;

            case InteractResult.PotDumped:
                LogHumanEvent($"{who}: 냄비를 비웠다");
                break;

            case InteractResult.TookDishFromPot:
                LogHumanEvent($"{who}: 요리를 그릇에 담았다", LogKind.Good);
                break;

            case InteractResult.PotNotReady:
                LogHumanEvent($"{who}: 아직 덜 끓었다", LogKind.Bad);
                break;

            case InteractResult.PlacedOnCounter:
                LogHumanEvent($"{who}: 카운터에 올렸다");
                break;

            case InteractResult.TookFromCounter:
            case InteractResult.TookOwnFromCounter:
                LogHumanEvent($"{who}: 카운터에서 집었다");
                break;

            case InteractResult.Served:
                LogHumanEvent($"{who}: 서빙 성공! ({DishesServed}/{m_TargetDishes})", LogKind.Good);
                break;

            case InteractResult.ServedWrongOrder:
                LogHumanEvent($"{who}: 주문에 없는 요리를 냈다", LogKind.Bad);
                break;

            case InteractResult.Wasted:
                LogHumanEvent($"{who}: 버렸다", LogKind.Bad);
                break;
        }
    }
}

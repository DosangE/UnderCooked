using UnityEngine;

// 재료함 / 손질대 / 냄비 / 그릇함 / 서빙구 / 카운터 전달칸 공통 컴포넌트.
// 자기 상태와 상호작용 규칙만 들고 있다.
// 그리드 등록(Cell 부여), 조리 시간 진행, 레시피 단계 주입은 KitchenEnv가 담당한다.
//
// 주문과의 대조는 여기서 하지 않는다. Station은 주문표를 모른다.
// "완성 요리를 냈다"까지만 판정하고, 그게 맞는 주문인지는 KitchenEnv가 결정한다.
public class Station : MonoBehaviour
{
    [SerializeField] StationType type = StationType.Counter;

    float m_CookTimer;

    // KitchenEnv가 에피소드마다 넣어준다. 손질을 거친 재료만 받는지가 여기서 갈린다.
    bool m_NeedsPrep = true;

    public StationType Type => type;

    // KitchenEnv가 초기화 때 로컬 좌표로부터 계산해서 넣어준다.
    public Vector2Int Cell { get; private set; }

    // --- 카운터 상태 ---
    public ItemType CounterItem { get; private set; } = ItemType.None;

    // 마지막으로 이 카운터에 물건을 올린 에이전트 인덱스. 비어 있으면 -1.
    public int CounterPlacedBy { get; private set; } = -1;

    // --- 냄비 상태 ---
    // 색깔별 개수다. 레시피가 '초록x2' 같은 조합이므로 bool로는 표현할 수 없다.
    public int GreenCount { get; private set; }
    public int RedCount { get; private set; }
    public int TotalCount => GreenCount + RedCount;

    public bool IsCooking { get; private set; }
    public bool HasCookedDish { get; private set; }

    // 재료가 다 차서 조리가 '확정'된 상태. 조리 중이든 다 됐든 둘 다 true다.
    //
    // ★ 이 플래그의 존재 이유는 정보 누설 차단이다.
    //   IsCooking이나 HasCookedDish로 Action Mask를 가르면, 조리가 끝나는 순간
    //   마스크가 바뀌면서 '완료 여부'가 관측 밖으로 새어나간다. 그러면 기억할 이유가
    //   없어지고 RNN이 장식이 된다. m_Committed는 완료 시점에 **변하지 않으므로**
    //   마스크가 조리 완료를 알려주지 않는다.
    public bool IsCommitted { get; private set; }

    // 이 냄비가 만들고 있는(또는 만들어 둔) 요리. 재료가 다 찬 시점에 확정된다.
    RecipeType m_CookedRecipe;
    public RecipeType CookedRecipe => m_CookedRecipe;

    // 조리 경과 시간. **사람용 화면 표시 전용이다.**
    // 관측에 절대 넣지 말 것. 넣는 순간 "언제 다 넣었는지 기억한다"는 RNN의 근거가 사라진다.
    public float CookTimer => m_CookTimer;

    public void Initialize(Vector2Int cell)
    {
        Cell = cell;
        ResetState();
    }

    // 에피소드 시작마다 KitchenEnv가 호출한다.
    public void ConfigureRecipe(bool needsPrep)
    {
        m_NeedsPrep = needsPrep;
    }

    public void ResetState()
    {
        CounterItem = ItemType.None;
        CounterPlacedBy = -1;
        GreenCount = 0;
        RedCount = 0;
        IsCooking = false;
        HasCookedDish = false;
        IsCommitted = false;
        m_CookTimer = 0f;
    }

    // 냄비 조리 진행. KitchenEnv가 매 FixedUpdate 호출한다.
    // 조리 진행도(m_CookTimer)는 일부러 밖으로 노출하지 않는다 -> 관측에서 빼고 RNN이 세게 한다.
    public void TickCooking(float deltaTime, float cookTime)
    {
        if (!IsCooking) return;

        m_CookTimer += deltaTime;
        if (m_CookTimer < cookTime) return;

        m_CookTimer = 0f;
        IsCooking = false;
        HasCookedDish = true;
        // GreenCount/RedCount는 일부러 그대로 둔다. 여기서 0으로 만들면
        // 관측(냄비 내용물)이 조리 완료 순간에 바뀌어서 완료 여부가 새어나간다.
    }

    // 냄비가 지금 이 재료를 받을 수 있는가.
    bool PotAccepts(ItemType heldItem)
    {
        if (IsCommitted) return false;
        if (TotalCount >= RecipeTypeExtensions.Capacity) return false;

        // 손질이 필요한 단계에서는 생재료를 거부한다. 손질대를 거치라는 뜻.
        if (m_NeedsPrep && (heldItem == ItemType.RawGreen || heldItem == ItemType.RawRed)) return false;
        if (!m_NeedsPrep && (heldItem == ItemType.PrepGreen || heldItem == ItemType.PrepRed)) return false;

        return heldItem.IsIngredient();
    }

    // 빈손으로 냄비를 비울 수 있는가.
    //
    // 이게 없으면 주문 시스템이 데드락을 만든다. RedSoup만 남았는데 냄비에 초록을
    // 하나 넣어버리면 그 냄비는 영영 RedSoup을 만들 수 없고, 실수 한 번이 에피소드를 죽인다.
    // 조리가 확정된 뒤에는 못 비운다(그래야 m_Committed 경계가 마스크 누설을 막는다).
    bool CanDumpPot()
    {
        return !IsCommitted && TotalCount > 0;
    }

    // 지금 손에 든 것으로 이 스테이션에 '의미 있는' 행동을 할 수 있는가.
    // Action Masking이 이 값을 그대로 쓴다 -> false면 Interact 행동이 마스킹된다.
    public bool CanInteract(ItemType heldItem)
    {
        switch (type)
        {
            case StationType.GreenBox:
            case StationType.RedBox:
            case StationType.PlateStack:
                // 손이 비었을 때만 꺼낼 수 있다.
                // 재료함의 '필드 동시 재료 수' 제한은 KitchenEnv가 따로 건다.
                return heldItem == ItemType.None;

            case StationType.PrepA:
            case StationType.PrepB:
                // 손질대는 색을 가리지 않는다. 구역마다 하나씩 있고 아무 생재료나 받는다.
                // 색을 가리면 재료의 색이 담당 구역을 정해버려서 주문에 따라 한쪽이 논다.
                return m_NeedsPrep && (heldItem == ItemType.RawGreen || heldItem == ItemType.RawRed);

            case StationType.Pot:
                if (heldItem == ItemType.None) return CanDumpPot();

                if (heldItem == ItemType.EmptyPlate)
                    // 재료가 다 차 있기만 하면 '떠보는 것'까지 허용한다.
                    // HasCookedDish로 가르면 관측에서 일부러 뺀 '조리 다 됐는지'가
                    // Action Mask를 통해 그대로 새어나간다. 덜 됐으면 헛도리로 남겨둔다.
                    return IsCommitted;

                return PotAccepts(heldItem);

            case StationType.ServingHatch:
                // 엉뚱한 물건을 버리는 것도 허용한다. 손이 비었을 때만 막는다.
                // 막다른 상태(팔리지 않는 요리를 든 채 굳음)를 빠져나갈 구멍이기도 하다.
                return heldItem != ItemType.None;

            case StationType.Counter:
                return heldItem == ItemType.None
                    ? CounterItem != ItemType.None   // 빈손이면 놓인 게 있어야 집는다
                    : CounterItem == ItemType.None;  // 뭘 들었으면 자리가 비어야 놓는다

            default:
                return false;
        }
    }

    // 실제 상태 변경까지 여기서 일어난다. 반드시 CanInteract가 true일 때만 효과가 있다.
    public InteractResult Interact(int agentIndex, ItemType heldItem, out ItemType newHeldItem)
    {
        newHeldItem = heldItem;
        if (!CanInteract(heldItem)) return InteractResult.Nothing;

        switch (type)
        {
            case StationType.GreenBox:
                newHeldItem = ItemType.RawGreen;
                return InteractResult.PickedFromSource;

            case StationType.RedBox:
                newHeldItem = ItemType.RawRed;
                return InteractResult.PickedFromSource;

            case StationType.PlateStack:
                newHeldItem = ItemType.EmptyPlate;
                return InteractResult.PickedFromSource;

            case StationType.PrepA:
            case StationType.PrepB:
                // 들고 온 재료의 색을 그대로 유지한다. 손질대가 색을 바꾸지는 않는다.
                newHeldItem = heldItem.IsGreen() ? ItemType.PrepGreen : ItemType.PrepRed;
                return InteractResult.Prepped;

            case StationType.Pot:
                return InteractPot(heldItem, out newHeldItem);

            case StationType.ServingHatch:
            {
                // 주문과 맞는지는 여기서 모른다. KitchenEnv가 Served를 받아서 대조한다.
                bool isDish = heldItem.IsCookedDish();
                newHeldItem = ItemType.None;
                return isDish ? InteractResult.Served : InteractResult.Wasted;
            }

            case StationType.Counter:
                if (heldItem == ItemType.None)
                {
                    // 동료가 놓은 것인지 내가 놓은 것인지 구분해서 돌려준다.
                    // 내가 놓고 내가 집는 건 보상 0 -> 놓기/집기 반복으로 보상을 긁는 짓을 막는다.
                    bool fromPartner = CounterPlacedBy >= 0 && CounterPlacedBy != agentIndex;
                    newHeldItem = CounterItem;
                    CounterItem = ItemType.None;
                    CounterPlacedBy = -1;
                    return fromPartner ? InteractResult.TookFromCounter : InteractResult.TookOwnFromCounter;
                }

                CounterItem = heldItem;
                CounterPlacedBy = agentIndex;
                newHeldItem = ItemType.None;
                return InteractResult.PlacedOnCounter;

            default:
                return InteractResult.Nothing;
        }
    }

    InteractResult InteractPot(ItemType heldItem, out ItemType newHeldItem)
    {
        newHeldItem = heldItem;

        // 1) 빈손 -> 내용물 버리기
        if (heldItem == ItemType.None)
        {
            GreenCount = 0;
            RedCount = 0;
            return InteractResult.PotDumped;
        }

        // 2) 재료 투입
        if (heldItem != ItemType.EmptyPlate)
        {
            if (heldItem.IsGreen()) GreenCount++;
            else RedCount++;

            newHeldItem = ItemType.None;

            if (TotalCount >= RecipeTypeExtensions.Capacity)
            {
                // 재료가 다 찼다. 이 시점에 만들 요리가 확정된다.
                m_CookedRecipe = RecipeTypeExtensions.FromCounts(GreenCount, RedCount);
                IsCommitted = true;
                IsCooking = true;
                m_CookTimer = 0f;
            }

            return InteractResult.PlacedInPot;
        }

        // 3) 빈 그릇 + 조리 확정. 아직 덜 끓었으면 헛도리로 끝난다.
        if (!HasCookedDish) return InteractResult.PotNotReady;

        // 완성됐으면 담아서 들고 나간다. 냄비는 다음 배치를 위해 비워진다.
        newHeldItem = m_CookedRecipe.Dish();
        GreenCount = 0;
        RedCount = 0;
        IsCommitted = false;
        HasCookedDish = false;
        return InteractResult.TookDishFromPot;
    }
}

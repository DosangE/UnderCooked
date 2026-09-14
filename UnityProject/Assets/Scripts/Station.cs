using UnityEngine;

// 재료함 / 손질대 / 냄비 / 그릇함 / 서빙구 / 카운터 전달칸 공통 컴포넌트.
// 자기 상태와 상호작용 규칙만 들고 있다.
// 그리드 등록(Cell 부여), 조리 시간 진행, 레시피 단계 주입은 KitchenEnv가 담당한다.
public class Station : MonoBehaviour
{
    [SerializeField] StationType type = StationType.Counter;

    float m_CookTimer;

    // KitchenEnv가 단계마다 넣어준다. 냄비가 빨강 재료까지 요구하는지,
    // 손질을 거친 재료만 받는지가 여기서 갈린다.
    bool m_NeedsRed = true;
    bool m_NeedsPrep = true;

    public StationType Type => type;

    // KitchenEnv가 초기화 때 로컬 좌표로부터 계산해서 넣어준다.
    public Vector2Int Cell { get; private set; }

    // --- 카운터 상태 ---
    public ItemType CounterItem { get; private set; } = ItemType.None;

    // 마지막으로 이 카운터에 물건을 올린 에이전트 인덱스. 비어 있으면 -1.
    public int CounterPlacedBy { get; private set; } = -1;

    // --- 냄비 상태 ---
    public bool HasGreen { get; private set; }
    public bool HasRed { get; private set; }
    public bool IsCooking { get; private set; }
    public bool HasCookedDish { get; private set; }

    // 이번 단계에서 냄비가 요구하는 재료가 다 들어왔는가.
    public bool PotFilled => HasGreen && (!m_NeedsRed || HasRed);

    public void Initialize(Vector2Int cell)
    {
        Cell = cell;
        ResetState();
    }

    // 레시피 단계가 바뀌면 KitchenEnv가 에피소드 시작마다 호출한다.
    public void ConfigureRecipe(bool needsRed, bool needsPrep)
    {
        m_NeedsRed = needsRed;
        m_NeedsPrep = needsPrep;
    }

    public void ResetState()
    {
        CounterItem = ItemType.None;
        CounterPlacedBy = -1;
        HasGreen = false;
        HasRed = false;
        IsCooking = false;
        HasCookedDish = false;
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
    }

    // 냄비가 지금 이 재료를 받을 수 있는가. 색깔별로 한 번씩만 받는다.
    bool PotAccepts(ItemType heldItem)
    {
        if (IsCooking || HasCookedDish) return false;

        // 손질이 필요한 단계에서는 생재료를 거부한다. 손질대를 거치라는 뜻.
        if (m_NeedsPrep && (heldItem == ItemType.RawGreen || heldItem == ItemType.RawRed)) return false;
        if (!m_NeedsPrep && (heldItem == ItemType.PrepGreen || heldItem == ItemType.PrepRed)) return false;

        if (heldItem.IsGreen()) return !HasGreen;
        if (heldItem.IsRed()) return m_NeedsRed && !HasRed;
        return false;
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

            case StationType.PrepGreen:
                return m_NeedsPrep && heldItem == ItemType.RawGreen;

            case StationType.PrepRed:
                return m_NeedsPrep && heldItem == ItemType.RawRed;

            case StationType.Pot:
                if (heldItem == ItemType.EmptyPlate)
                    // 냄비가 차 있기만 하면 '떠보는 것'까지 허용한다.
                    // 여기서 HasCookedDish로 마스크를 가르면, 관측에서 일부러 뺀
                    // '조리 다 됐는지'가 Action Mask를 통해 그대로 새어나간다.
                    // 그러면 정책이 기억할 필요 없이 마스크가 열릴 때 누르기만 하면 되고,
                    // RNN을 쓸 이유가 사라진다. 덜 됐으면 헛도리가 되도록 남겨둔다.
                    return PotFilled;

                return PotAccepts(heldItem);

            case StationType.ServingHatch:
                // 엉뚱한 물건을 버리는 것도 허용한다(-0.2). 손이 비었을 때만 막는다.
                // 막다른 상태(쓸모없는 걸 든 채 굳음)를 빠져나갈 구멍이기도 하다.
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

            case StationType.PrepGreen:
                newHeldItem = ItemType.PrepGreen;
                return InteractResult.Prepped;

            case StationType.PrepRed:
                newHeldItem = ItemType.PrepRed;
                return InteractResult.Prepped;

            case StationType.Pot:
                if (heldItem != ItemType.EmptyPlate)
                {
                    if (heldItem.IsGreen()) HasGreen = true;
                    else HasRed = true;

                    newHeldItem = ItemType.None;
                    if (PotFilled) IsCooking = true;
                    return InteractResult.PlacedInPot;
                }

                // 빈 그릇 + 냄비가 참. 아직 조리 중이면 헛도리로 끝난다.
                if (!HasCookedDish) return InteractResult.PotNotReady;

                // 완성됐으면 담아서 들고 나간다. 냄비는 다음 배치를 위해 비워진다.
                newHeldItem = ItemType.CookedDish;
                HasGreen = false;
                HasRed = false;
                HasCookedDish = false;
                return InteractResult.TookDishFromPot;

            case StationType.ServingHatch:
            {
                bool correct = heldItem == ItemType.CookedDish;
                newHeldItem = ItemType.None;
                return correct ? InteractResult.Served : InteractResult.Wasted;
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
}

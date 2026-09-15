using UnityEngine;

// 대기 중인 주문표. MonoBehaviour가 아니라 KitchenEnv가 들고 있는 순수 C# 객체다.
// (씬 오브젝트로 만들면 TrainingArea 16개에 전부 붙여야 하는데 얻는 게 없다)
//
// 슬롯 수는 항상 채워진 상태로 유지한다. 하나가 소진되거나 만료되면 즉시 새 주문이 들어온다.
// 그래서 '대기 주문 수'는 에피소드 내내 m_ActiveSlots로 고정이고, 관측 차원도 고정이다.
//
// 슬롯 인덱스는 절대 섞지 않는다. 같은 주문이 매 스텝 같은 관측 자리에 있어야
// 정책이 "2번 슬롯이 급하다" 같은 걸 배울 수 있다.
public class OrderBoard
{
    public const int MaxSlots = 3;

    // 관측 한 슬롯당 차원: 요리 one-hot(3) + 남은 시간 정규화(1) + 유효 플래그(1)
    public const int ObservationPerSlot = RecipeTypeExtensions.Count + 2;
    public const int ObservationSize = MaxSlots * ObservationPerSlot;

    public struct Slot
    {
        public bool Active;
        public RecipeType Recipe;
        public float Remaining;   // 초
    }

    readonly Slot[] m_Slots = new Slot[MaxSlots];

    int m_ActiveSlots = 1;    // 커리큘럼 order_slots
    int m_PoolSize = 1;       // 커리큘럼 recipe_pool_size. RecipeType 순서대로 앞에서부터 풀린다
    float m_Duration = 20f;   // 주문 하나의 제한 시간

    public int ActiveSlots => m_ActiveSlots;
    public int PoolSize => m_PoolSize;
    public float Duration => m_Duration;

    // 이번 에피소드에 빨강 재료가 쓰이는가. 재료함을 숨길지 결정하는 데 쓴다.
    // GreenSoup만 있는 lesson0에서는 빨강이 등장하지 않는다.
    public bool UsesRed => m_PoolSize > 1;

    public Slot GetSlot(int index)
    {
        return index >= 0 && index < MaxSlots ? m_Slots[index] : default;
    }

    public void Configure(int activeSlots, int poolSize, float duration)
    {
        m_ActiveSlots = Mathf.Clamp(activeSlots, 1, MaxSlots);
        m_PoolSize = Mathf.Clamp(poolSize, 1, RecipeTypeExtensions.Count);
        m_Duration = Mathf.Max(1f, duration);
    }

    public void ResetBoard()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            m_Slots[i] = default;
            if (i < m_ActiveSlots) Fill(i);
        }
    }

    void Fill(int index)
    {
        m_Slots[index] = new Slot
        {
            Active = true,
            Recipe = (RecipeType)Random.Range(0, m_PoolSize),
            Remaining = m_Duration
        };
    }

    // 시간을 흘린다. 이번 틱에 만료된 주문 수를 돌려주고, 그 자리는 새 주문으로 채운다.
    // 만료 패널티는 KitchenGroup이 이 반환값으로 매긴다.
    public int Tick(float deltaTime)
    {
        int expired = 0;

        for (int i = 0; i < m_ActiveSlots; i++)
        {
            if (!m_Slots[i].Active) { Fill(i); continue; }

            m_Slots[i].Remaining -= deltaTime;
            if (m_Slots[i].Remaining > 0f) continue;

            expired++;
            Fill(i);
        }

        return expired;
    }

    // 완성 요리를 주문과 대조해서 소진시킨다.
    // 같은 요리를 요구하는 주문이 여럿이면 **가장 급한 것(남은 시간이 짧은 것)**부터 없앤다.
    // 안 그러면 여유 있는 주문을 먼저 지워서 급한 쪽이 괜히 만료된다.
    public bool TryConsume(ItemType dish)
    {
        int best = FindMostUrgentFor(dish);
        if (best < 0) return false;

        Fill(best);
        return true;
    }

    // 냄비에 (green, red)가 들어 있을 때, 아직 어떤 주문으로든 이어질 수 있는가.
    // 재료 투입 보상의 부호를 여기서 가른다 -> 아무 재료나 처넣는 것이 이득이 되지 않게 한다.
    public bool IsReachable(int green, int red)
    {
        for (int i = 0; i < m_ActiveSlots; i++)
        {
            if (!m_Slots[i].Active) continue;
            if (m_Slots[i].Recipe.StillReachable(green, red)) return true;
        }
        return false;
    }

    // 그 색 재료를 원하는 대기 주문이 있는가. 카운터 전달 보상을 주문과 무관한
    // 재료에까지 주지 않기 위해 쓴다.
    //
    // '지금 끓이는 배치'뿐 아니라 **다음 배치용으로 미리 준비하는 것**도 쓸모로 친다.
    //
    // 예전에는 지금 배치로 만들 수 있는 주문만 봤다. 그러면 냄비에 빨강이 하나 들어간
    // 순간 대기 중인 GreenSoup 주문을 위한 초록이 '쓸모없음'이 되어, 미리 준비하는
    // 행동에 보상이 안 붙고 하이라이트는 "버려라"라고 말했다.
    // 냄비가 하나뿐이라 한 번에 한 접시씩만 끓는데, 그 사이에 한가한 셰프가 다음 주문을
    // 준비하는 것이야말로 두 사람이 놀지 않는 유일한 길이다. 그걸 벌하고 있었다.
    //
    // (필드에 나와 있을 수 있는 재료 수는 max_ingredients가 따로 막으므로, 이걸 넓혀도
    //  재료를 무한정 쌓아둘 수는 없다)
    public bool WantsColor(bool green, int potGreen, int potRed)
    {
        for (int i = 0; i < m_ActiveSlots; i++)
        {
            if (!m_Slots[i].Active) continue;

            var recipe = m_Slots[i].Recipe;
            int need = green ? recipe.RequiredGreen() : recipe.RequiredRed();
            if (need <= 0) continue;

            // 지금 배치로는 못 만드는 주문 -> 다음 배치용으로 준비해 둘 값어치가 있다.
            if (!recipe.StillReachable(potGreen, potRed)) return true;

            // 지금 배치에 바로 들어간다.
            int have = green ? potGreen : potRed;
            if (need > have) return true;
        }
        return false;
    }

    // 이 완성 요리를 받아줄 주문이 지금 있는가. (소진시키지 않고 확인만)
    public bool HasOrderFor(ItemType dish)
    {
        return FindMostUrgentFor(dish) >= 0;
    }

    // 이 완성 요리를 받아줄 주문 중 가장 급한 것의 슬롯 번호. 없으면 -1.
    public int FindMostUrgentFor(ItemType dish)
    {
        return FindMostUrgent((slot) => slot.Recipe.Dish() == dish);
    }

    // 냄비가 (green, red)인 상태에서 아직 만들 수 있는 주문 중 가장 급한 것. 없으면 -1.
    // 사람용 안내가 "지금 무슨 요리를 만드는 중인가"를 정하는 기준이다.
    public int FindMostUrgentReachable(int green, int red)
    {
        return FindMostUrgent((slot) => slot.Recipe.StillReachable(green, red));
    }

    int FindMostUrgent(System.Func<Slot, bool> accept)
    {
        int best = -1;

        for (int i = 0; i < m_ActiveSlots; i++)
        {
            if (!m_Slots[i].Active) continue;
            if (!accept(m_Slots[i])) continue;
            if (best >= 0 && m_Slots[i].Remaining >= m_Slots[best].Remaining) continue;
            best = i;
        }

        return best;
    }
}

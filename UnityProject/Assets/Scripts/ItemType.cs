// 셰프가 손에 들 수 있는 아이템 종류.
// 관측의 one-hot 인덱스와 enum 값이 그대로 일치한다. 순서를 바꾸면 관측이 깨진다.
//
// 완성 요리가 하나(CookedDish)가 아니라 레시피별로 나뉘어 있는 것이 핵심이다.
// 요리에 정체성이 없으면 서빙구는 "요리인가 아닌가"만 볼 수 있고,
// 그러면 무엇을 만들든 성공이라 주문을 읽을 이유가 사라진다.
public enum ItemType
{
    None = 0,
    RawGreen = 1,      // 초록 생재료 (재료함 G에서 나온다)
    RawRed = 2,        // 빨강 생재료 (재료함 R에서 나온다)
    PrepGreen = 3,     // 초록 손질됨 (초록 손질대를 거친 것)
    PrepRed = 4,       // 빨강 손질됨 (빨강 손질대를 거친 것)
    EmptyPlate = 5,
    CookedGreen = 6,   // 초록x2  -> GreenSoup
    CookedMix = 7,     // 초록+빨강 -> MixSoup
    CookedRed = 8      // 빨강x2  -> RedSoup
}

public static class ItemTypeExtensions
{
    public const int Count = 9;

    // 재료함에서 나와 아직 냄비에 들어가지 않은 것. 필드 동시 재료 수 제한에 쓴다.
    public static bool IsIngredient(this ItemType item)
    {
        return item == ItemType.RawGreen || item == ItemType.RawRed
            || item == ItemType.PrepGreen || item == ItemType.PrepRed;
    }

    // 손질 여부와 무관하게 어느 색 계열인지. 냄비가 색깔별 개수를 세는 데 쓴다.
    public static bool IsGreen(this ItemType item)
    {
        return item == ItemType.RawGreen || item == ItemType.PrepGreen;
    }

    public static bool IsRed(this ItemType item)
    {
        return item == ItemType.RawRed || item == ItemType.PrepRed;
    }

    // 완성된 요리인가. 서빙구가 주문과 대조할 대상인지 가리는 데 쓴다.
    public static bool IsCookedDish(this ItemType item)
    {
        return item == ItemType.CookedGreen || item == ItemType.CookedMix || item == ItemType.CookedRed;
    }
}

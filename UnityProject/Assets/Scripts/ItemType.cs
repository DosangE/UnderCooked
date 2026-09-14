// 셰프가 손에 들 수 있는 아이템 종류.
// 관측의 one-hot 인덱스와 enum 값이 그대로 일치한다. 순서를 바꾸면 관측이 깨진다.
public enum ItemType
{
    None = 0,
    RawGreen = 1,     // 초록 생재료 (재료함 G에서 나온다)
    RawRed = 2,       // 빨강 생재료 (재료함 R에서 나온다)
    PrepGreen = 3,    // 초록 손질됨 (초록 손질대를 거친 것)
    PrepRed = 4,      // 빨강 손질됨 (빨강 손질대를 거친 것)
    EmptyPlate = 5,
    CookedDish = 6    // 완성 요리. 서빙구에 내면 점수
}

public static class ItemTypeExtensions
{
    public const int Count = 7;

    // 재료함에서 나와 아직 냄비에 들어가지 않은 것. 필드 동시 재료 수 제한에 쓴다.
    public static bool IsIngredient(this ItemType item)
    {
        return item == ItemType.RawGreen || item == ItemType.RawRed
            || item == ItemType.PrepGreen || item == ItemType.PrepRed;
    }

    // 손질 여부와 무관하게 어느 색 계열인지. 냄비가 색깔별로 한 번씩만 받도록 하는 데 쓴다.
    public static bool IsGreen(this ItemType item)
    {
        return item == ItemType.RawGreen || item == ItemType.PrepGreen;
    }

    public static bool IsRed(this ItemType item)
    {
        return item == ItemType.RawRed || item == ItemType.PrepRed;
    }
}

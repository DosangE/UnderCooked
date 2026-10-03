// 셰프가 손에 들 수 있는 아이템 종류.
// 관측의 one-hot 인덱스와 enum 값이 그대로 일치한다. 순서를 바꾸면 관측이 깨진다.
//
// 완성 요리가 하나(CookedDish)가 아니라 레시피별로 나뉘어 있는 것이 핵심이다.
// 요리에 정체성이 없으면 서빙구는 "요리인가 아닌가"만 볼 수 있고,
// 그러면 무엇을 만들든 성공이라 주문을 읽을 이유가 사라진다.
//
// 파랑(9~13)은 뒤에 덧붙였다. 앞 번호를 그대로 두어 회귀 검사와 로그의 의미가 바뀌지 않게 한다.
// (관측 one-hot 길이는 Count를 따라 늘어난다)
public enum ItemType
{
    None = 0,
    RawGreen = 1,      // 초록 생재료 (재료함 G에서 나온다)
    RawRed = 2,        // 빨강 생재료 (재료함 R에서 나온다)
    PrepGreen = 3,     // 초록 손질됨 (손질대를 거친 것. 손질대는 색을 가리지 않는다)
    PrepRed = 4,       // 빨강 손질됨 (위와 같음. 색은 손질대가 아니라 재료가 정한다)
    EmptyPlate = 5,
    CookedGreen = 6,   // 초록x2  -> GreenSoup
    CookedMix = 7,     // 초록+빨강 -> MixSoup
    CookedRed = 8,     // 빨강x2  -> RedSoup
    RawBlue = 9,       // 파랑 생재료 (재료함 U에서 나온다)
    PrepBlue = 10,     // 파랑 손질됨
    CookedBlue = 11,   // 파랑x2  -> BlueSoup
    CookedGreenBlue = 12,  // 초록+파랑 -> GreenBlueSoup
    CookedRedBlue = 13     // 빨강+파랑 -> RedBlueSoup
}

public static class ItemTypeExtensions
{
    public const int Count = 14;

    // 재료함에서 나와 아직 냄비에 들어가지 않은 것. 필드 동시 재료 수 제한에 쓴다.
    // 어느 재료인지, 생인지 손질됐는지는 IngredientTypeExtensions가 안다.
    public static bool IsIngredient(this ItemType item)
    {
        return item.TryGetIngredient(out _);
    }

    // 완성된 요리인가. 서빙구가 주문과 대조할 대상인지 가리는 데 쓴다.
    public static bool IsCookedDish(this ItemType item)
    {
        return item.TryGetRecipe(out _);
    }
}

// 주문될 수 있는 요리 종류.
//
// 레시피 축을 '재료 2개 조합'으로 잡았다. 새 재료함이나 새 손질대 없이
// 3종이 나오고, 무엇보다 **어떤 요리든 재료가 정확히 2개**라서 작업량이 같다.
// 즉 주문이 바뀌어도 "얼마나 일하는가"는 그대로고 "어느 재료함에서 가져오는가"만
// 달라진다 -> 순수하게 '주문을 읽고 역할을 다시 나누는 것'만 학습 문제로 남는다.
//
// 어느 요리든 협동의 모양은 같다. 손질대는 색이 아니라 **구역**으로 나뉘어 있어서
// (A 구역에 하나, B 구역에 하나, 둘 다 색을 안 가린다) 재료의 색이 담당자를 정하지 않는다.
// 냄비가 A, 그릇함과 서빙구가 B라는 **위치**만이 전달을 강제한다.
//
// 색으로 나눴던 예전 설계는 RedSoup에서 B가 이동량의 81%를 지고 A는 냄비 앞에서
// 받기만 했다(4.3:1). 누가 무엇을 맡을지는 맵이 아니라 둘이 런타임에 정해야 한다.
// 측정값과 경위는 README 4-10에 있다.
//
// 값은 관측 one-hot 인덱스로 쓰인다. 순서를 바꾸면 관측이 깨진다.
// 커리큘럼(recipe_pool_size)은 이 순서대로 앞에서부터 풀어준다.
public enum RecipeType
{
    GreenSoup = 0,   // 초록 x2
    MixSoup = 1,     // 초록 + 빨강
    RedSoup = 2      // 빨강 x2
}

public static class RecipeTypeExtensions
{
    public const int Count = 3;

    // 모든 레시피가 재료 2개다. 냄비 용량이자 조리 시작 조건이다.
    public const int Capacity = 2;

    public static int RequiredGreen(this RecipeType recipe)
    {
        switch (recipe)
        {
            case RecipeType.GreenSoup: return 2;
            case RecipeType.MixSoup:   return 1;
            default:                   return 0;   // RedSoup
        }
    }

    public static int RequiredRed(this RecipeType recipe)
    {
        return Capacity - recipe.RequiredGreen();
    }

    // 이 레시피가 완성되면 나오는 아이템.
    public static ItemType Dish(this RecipeType recipe)
    {
        switch (recipe)
        {
            case RecipeType.GreenSoup: return ItemType.CookedGreen;
            case RecipeType.MixSoup:   return ItemType.CookedMix;
            default:                   return ItemType.CookedRed;
        }
    }

    // 냄비 내용물(초록 g개, 빨강 r개)이 어떤 레시피가 되는가.
    // 합이 Capacity일 때만 의미가 있다.
    public static RecipeType FromCounts(int green, int red)
    {
        if (green >= 2) return RecipeType.GreenSoup;
        if (red >= 2) return RecipeType.RedSoup;
        return RecipeType.MixSoup;
    }

    // 냄비에 지금 (green, red)가 들어 있을 때, 아직 이 레시피가 될 수 있는가.
    // '재료 투입이 잘한 짓인지'를 판정하는 기준이다 -> 초과해서 넣은 순간 false.
    public static bool StillReachable(this RecipeType recipe, int green, int red)
    {
        return green <= recipe.RequiredGreen() && red <= recipe.RequiredRed();
    }
}

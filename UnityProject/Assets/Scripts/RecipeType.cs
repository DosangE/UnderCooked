using System.Collections.Generic;

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
// 측정값과 경위는 docs/DESIGN.md 4-10에 있다.
//
// 값은 관측 one-hot 인덱스로 쓰인다. 순서를 바꾸면 관측이 깨진다.
// 커리큘럼(recipe_pool_size)은 이 순서대로 앞에서부터 풀어준다.
//   1~3: 초록/빨강만 (예전 3종 그대로)
//   4:   파랑이 처음 등장한다. 파랑 하나만 새로 익히도록 BlueSoup(파랑x2)을 먼저 푼다
//   5~6: 파랑과 기존 재료의 조합
// 재료 3종 x 2개 조합 = 6가지가 전부 레시피라서 냄비가 차면 반드시 어떤 요리가 된다.
public enum RecipeType
{
    GreenSoup = 0,       // 초록 x2
    MixSoup = 1,         // 초록 + 빨강
    RedSoup = 2,         // 빨강 x2
    BlueSoup = 3,        // 파랑 x2
    GreenBlueSoup = 4,   // 초록 + 파랑
    RedBlueSoup = 5      // 빨강 + 파랑
}

public static class RecipeTypeExtensions
{
    public const int Count = 6;

    // 모든 레시피가 재료 2개다. 냄비 용량이자 조리 시작 조건이다.
    public const int Capacity = 2;

    // 레시피마다 재료별 필요 개수. [레시피][재료], 행의 합은 항상 Capacity다.
    // 재료 개수 조합이 곧 레시피이므로, 냄비에 Capacity개가 차면 정확히 한 행과 맞아야 한다.
    static readonly int[][] s_Required =
    {
        //       초록 빨강 파랑
        new[] { 2, 0, 0 },   // GreenSoup
        new[] { 1, 1, 0 },   // MixSoup
        new[] { 0, 2, 0 },   // RedSoup
        new[] { 0, 0, 2 },   // BlueSoup
        new[] { 1, 0, 1 },   // GreenBlueSoup
        new[] { 0, 1, 1 },   // RedBlueSoup
    };

    // 이 레시피가 완성되면 나오는 아이템.
    static readonly ItemType[] s_Dish =
    {
        ItemType.CookedGreen, ItemType.CookedMix, ItemType.CookedRed,
        ItemType.CookedBlue, ItemType.CookedGreenBlue, ItemType.CookedRedBlue
    };

    public static int Required(this RecipeType recipe, IngredientType ingredient)
    {
        return s_Required[(int)recipe][(int)ingredient];
    }

    public static ItemType Dish(this RecipeType recipe)
    {
        return s_Dish[(int)recipe];
    }

    // 이 완성 요리가 어느 레시피의 것인가.
    public static bool TryGetRecipe(this ItemType dish, out RecipeType recipe)
    {
        for (int i = 0; i < Count; i++)
        {
            if (s_Dish[i] != dish) continue;
            recipe = (RecipeType)i;
            return true;
        }
        recipe = default;
        return false;
    }

    // 냄비 내용물(재료별 개수)이 어떤 레시피가 되는가.
    // 합이 Capacity일 때만 의미가 있다. 그때는 s_Required의 행 하나와 정확히 맞는다.
    public static RecipeType FromCounts(IReadOnlyList<int> counts)
    {
        for (int r = 0; r < Count; r++)
        {
            bool match = true;
            for (int i = 0; i < IngredientTypeExtensions.Count && match; i++)
                match = counts[i] == s_Required[r][i];
            if (match) return (RecipeType)r;
        }

        UnityEngine.Debug.LogError($"[RecipeType] 재료 조합 ({IngredientTypeExtensions.Describe(counts)})에 맞는 레시피가 없다");
        return default;
    }

    // 냄비에 지금 이 개수만큼 들어 있을 때, 아직 이 레시피가 될 수 있는가.
    // '재료 투입이 잘한 짓인지'를 판정하는 기준이다 -> 초과해서 넣은 순간 false.
    public static bool StillReachable(this RecipeType recipe, IReadOnlyList<int> counts)
    {
        for (int i = 0; i < IngredientTypeExtensions.Count; i++)
            if (counts[i] > s_Required[(int)recipe][i]) return false;
        return true;
    }

    // 이 레시피를 만들려면 냄비에 이 재료를 '앞으로 더' 몇 개 넣어야 하는가.
    public static int Missing(this RecipeType recipe, IngredientType ingredient, IReadOnlyList<int> counts)
    {
        return recipe.Required(ingredient) - counts[(int)ingredient];
    }
}

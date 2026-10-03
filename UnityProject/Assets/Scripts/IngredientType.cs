using System.Collections.Generic;
using System.Text;

// 재료 종류. 재료함 하나가 재료 한 종류를 낸다.
//
// 예전에는 '초록/빨강'이 코드 곳곳에 따로 박혀 있었다(GreenCount/RedCount, IsGreen/IsRed,
// WantsColor(bool green) ...). 재료를 늘리려면 그걸 전부 찾아 고쳐야 했으므로,
// 재료에 관한 사실은 여기 한 곳에 모은다. 새 재료는 이 enum과 아래 표 세 개만 늘리면 된다.
//
// 값은 냄비의 재료별 개수 배열과 관측(냄비 내용물, 재료함 좌표)의 순서로 쓰인다.
// 순서를 바꾸면 관측이 깨진다.
public enum IngredientType
{
    Green = 0,
    Red = 1
}

public static class IngredientTypeExtensions
{
    public const int Count = 2;

    // 재료마다 생재료 / 손질된 재료 / 그 재료를 내는 재료함. 인덱스 = IngredientType.
    static readonly ItemType[] s_Raw = { ItemType.RawGreen, ItemType.RawRed };
    static readonly ItemType[] s_Prepped = { ItemType.PrepGreen, ItemType.PrepRed };
    static readonly StationType[] s_Box = { StationType.GreenBox, StationType.RedBox };

    // 사람용 표시 이름. 관측/보상에는 쓰이지 않는다.
    static readonly string[] s_Label = { "초록", "빨강" };

    public static ItemType Raw(this IngredientType ingredient) => s_Raw[(int)ingredient];
    public static ItemType Prepped(this IngredientType ingredient) => s_Prepped[(int)ingredient];
    public static StationType Box(this IngredientType ingredient) => s_Box[(int)ingredient];
    public static string Label(this IngredientType ingredient) => s_Label[(int)ingredient];

    // 이 스테이션이 재료함이면 어느 재료를 내는가.
    public static bool TryGetBoxIngredient(this StationType type, out IngredientType ingredient)
    {
        for (int i = 0; i < Count; i++)
        {
            if (s_Box[i] != type) continue;
            ingredient = (IngredientType)i;
            return true;
        }
        ingredient = default;
        return false;
    }

    // 이 아이템이 재료(생이든 손질됐든)면 어느 재료인가.
    public static bool TryGetIngredient(this ItemType item, out IngredientType ingredient)
    {
        for (int i = 0; i < Count; i++)
        {
            if (s_Raw[i] != item && s_Prepped[i] != item) continue;
            ingredient = (IngredientType)i;
            return true;
        }
        ingredient = default;
        return false;
    }

    // 재료인 것이 확실할 때만 쓴다 (IsIngredient로 거른 뒤).
    public static IngredientType Ingredient(this ItemType item)
    {
        item.TryGetIngredient(out var ingredient);
        return ingredient;
    }

    public static bool IsRawIngredient(this ItemType item)
    {
        for (int i = 0; i < Count; i++) if (s_Raw[i] == item) return true;
        return false;
    }

    public static bool IsPreppedIngredient(this ItemType item)
    {
        for (int i = 0; i < Count; i++) if (s_Prepped[i] == item) return true;
        return false;
    }

    // 재료별 개수를 "초록 1 / 빨강 0" 꼴로. 사람용 화면과 로그가 같은 표기를 쓰게 한다.
    public static string Describe(IReadOnlyList<int> counts)
    {
        var text = new StringBuilder();
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) text.Append(" / ");
            text.Append(((IngredientType)i).Label()).Append(' ').Append(counts[i]);
        }
        return text.ToString();
    }
}

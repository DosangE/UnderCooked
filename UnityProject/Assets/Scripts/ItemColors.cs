using UnityEngine;

// 아이템을 화면에 그릴 때 쓰는 색. 사람이 보는 것 전부가 여기 하나를 본다.
//   - 셰프 손에 든 큐브      (ChefAgent)
//   - 냄비 안의 재료 슬롯     (PotContentsView)
//   - 카운터에 놓인 물건      (CounterContentsView)
//
// 한 곳으로 모은 이유: 예전에는 손에 든 것의 색을 ChefAgent가, 냄비 색을
// PotContentsView가 각자 들고 있었다. 그러면 한쪽만 바꿨을 때 같은 물건이 장소마다
// 다른 색으로 보이고, 사람은 그걸 '다른 물건'으로 읽는다. 화면이 거짓말하는 또 하나의 경로다.
//
// 관측/보상/행동에는 전혀 관여하지 않는다. 정책은 화면을 보지 않는다
// (센서는 Vector 관측 하나뿐이고, 스테이션 정체성은 관측 슬롯 번호로 정해진다).
public static class ItemColors
{
    public static readonly Color RawGreen = new Color(0.20f, 0.80f, 0.25f);
    public static readonly Color RawRed = new Color(0.85f, 0.20f, 0.20f);

    // 손질된 재료는 같은 색 계열이되 밝게 해서 생재료와 구분한다.
    public static readonly Color PrepGreen = new Color(0.55f, 1.00f, 0.45f);
    public static readonly Color PrepRed = new Color(1.00f, 0.55f, 0.45f);

    public static readonly Color EmptyPlate = new Color(0.95f, 0.95f, 0.95f);

    // 완성 요리는 레시피별로 다른 색. 손만 보고 어느 주문용인지 알 수 있어야 한다.
    public static readonly Color CookedGreen = new Color(0.60f, 0.95f, 0.30f);
    public static readonly Color CookedMix = new Color(1.00f, 0.78f, 0.05f);
    public static readonly Color CookedRed = new Color(0.95f, 0.35f, 0.20f);

    // 조리가 끝났다는 표시. 냄비 슬롯이 이 색으로 깜빡인다.
    public static readonly Color Done = new Color(1.00f, 0.85f, 0.20f);

    public static Color For(ItemType item)
    {
        switch (item)
        {
            case ItemType.RawGreen:    return RawGreen;
            case ItemType.RawRed:      return RawRed;
            case ItemType.PrepGreen:   return PrepGreen;
            case ItemType.PrepRed:     return PrepRed;
            case ItemType.EmptyPlate:  return EmptyPlate;
            case ItemType.CookedGreen: return CookedGreen;
            case ItemType.CookedMix:   return CookedMix;
            case ItemType.CookedRed:   return CookedRed;
            default:                   return Color.gray;
        }
    }
}

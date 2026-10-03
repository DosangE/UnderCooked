using System.Collections.Generic;

// '지금 이 주방이 무슨 요리를 만드는 중인가'를 한 곳에서 계산한 결과.
//
// 사람이 플레이할 때 쓰는 안내 전용이다. 관측/보상/행동에는 전혀 쓰이지 않는다.
// 하이라이트(TargetHighlighter)와 주문판(OrderHud)이 **같은 계획을 보게** 하려고
// 따로 뺐다. 둘이 각자 판단하면 화면이 서로 다른 말을 한다.
public struct KitchenPlan
{
    public enum Step
    {
        NoOrder,        // 만들 수 있는 주문이 없다 (냄비 내용물이 어떤 주문과도 안 맞음)
        Gather,         // 재료를 더 모아야 한다
        Cooking,        // 재료는 다 들어갔고 끓는 중
        Plate,          // 다 됐다. 빈 그릇으로 뜨면 된다
        Serve           // 완성 요리가 나와 있다. 서빙구로
    }

    public Step Current;

    // 지금 목표로 삼은 주문. 없으면 HasOrder = false.
    public bool HasOrder;
    public int OrderSlot;
    public RecipeType Recipe;

    // 재료를 모으는 중(Gather)일 때의 냄비 내용물. 그 밖의 단계에서는 null이다.
    // 냄비의 배열을 그대로 가리키므로, 계획은 만든 그 프레임 안에서만 쓴다(매 프레임 새로 만든다).
    public IReadOnlyList<int> PotCounts;

    // 목표 요리까지 냄비에 이 재료를 '앞으로 더' 넣어야 할 개수. 모으는 중이 아니면 0.
    public int Need(IngredientType ingredient)
    {
        return Current == Step.Gather && PotCounts != null ? Recipe.Missing(ingredient, PotCounts) : 0;
    }

    // 냄비에 들어 있는 것으로는 어떤 주문도 만들 수 없는 상태. 비워야 한다.
    public bool PotIsDeadEnd => Current == Step.NoOrder;
}

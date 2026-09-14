// 그리드 위 상호작용 지점의 종류.
//
// 값은 프리팹의 Station 컴포넌트에 직렬화되어 있으므로 기존 번호를 바꾸지 않는다.
// GreenBox(0)와 RedBox(5)는 예전 IngredientBox / IngredientBoxMid 자리를 물려받았다.
//
// 재료함 두 개는 카운터 경계 위에 있어 양쪽 구역에서 모두 집을 수 있다.
// 대신 냄비가 A 구역, 서빙구가 B 구역에 있어서 협동은 그쪽에서 강제된다.
public enum StationType
{
    GreenBox = 0,      // 초록 재료함 (경계, 공용)
    Pot = 1,           // 냄비      (A 구역)
    PlateStack = 2,    // 그릇함    (B 구역)
    ServingHatch = 3,  // 서빙구    (B 구역)
    Counter = 4,       // 카운터 전달칸 (경계, 공용)
    RedBox = 5,        // 빨강 재료함 (경계, 공용)
    PrepGreen = 6,     // 초록 손질대 (A 구역)
    PrepRed = 7        // 빨강 손질대 (B 구역)
}

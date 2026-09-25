// Interact 한 번이 실제로 무슨 일을 했는지.
// ChefAgent가 이 값으로 개인 보상을, KitchenGroup이 팀 보상을 결정한다.
public enum InteractResult
{
    Nothing = 0,          // 아무 일도 없음 (Action Masking이 걸려 있으면 나오지 않아야 정상)
    PickedFromSource,     // 재료함 / 그릇함에서 집음
    Prepped,              // 손질대에서 생재료 -> 손질된 재료로 바꿈
    PlacedInPot,          // 냄비에 재료 투입
    TookDishFromPot,      // 빈 그릇으로 완성된 요리를 담음
    PotNotReady,          // 빈 그릇을 들고 떠봤지만 아직 조리 중이었음 (헛도리)
    PotDumped,            // 빈손으로 냄비를 비움. 잘못 넣은 재료에서 빠져나오는 유일한 길
    PlacedInPotWrong,     // 냄비에 넣었지만 이제 어떤 대기 주문도 만들 수 없게 됨
    PlacedOnCounter,      // 카운터에 올림 (전달 시도)
    TookFromCounter,      // 카운터에서 '동료가 놓은' 물건을 집음 -> 전달 성립
    TookOwnFromCounter,   // 카운터에서 '자기가 놓은' 물건을 도로 집음 -> 보상 없음
    Served,               // 서빙구에 완성 요리 제출, 대기 주문과 일치 -> 성공
    ServedWrongOrder,     // 완성 요리이긴 한데 그걸 주문한 손님이 없음
    Wasted                // 서빙구에 요리도 아닌 엉뚱한 물건을 버림
}

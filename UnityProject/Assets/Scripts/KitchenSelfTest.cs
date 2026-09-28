using System.Text;
using Unity.MLAgents.Actuators;
using UnityEngine;

// 보상 설계가 의도대로 동작하는지 확인하는 회귀 검사.
//
// ★ 반드시 실제 ChefAgent.OnActionReceived 경로를 거쳐서 검사한다.
//   이전에 검사 스크립트가 지급 조건("이미 전달 보상을 받은 물건인가")을 자기가 직접
//   계산해서 통과 판정을 냈는데, 정작 ChefAgent의 지급부에는 그 조건이 빠져 있었다.
//   구현이 아니라 의도를 검사한 것이다. 그래서 여기서는 행동만 넣고 보상 변화만 읽는다.
//
// 직렬화 값 대조(StartupValidator)로는 이런 종류를 못 잡는다. 보상 지급 조건이 빠진 것은
// 씬/코드 불일치가 아니라 로직 누락이기 때문이다. 두 검사는 잡는 게 서로 다르다.
//
// 실행: Unity 메뉴 [UnderCooked/보상 회귀 검사] 또는 KitchenSelfTest.RunAll()
public static class KitchenSelfTest
{
    const int InteractAction = 1;

    public static string RunAll()
    {
        var env = Object.FindFirstObjectByType<KitchenEnv>();
        if (env == null) return "★ Play 모드에서 실행할 것 (KitchenEnv를 찾지 못했다)";

        var group = env.GetComponent<KitchenGroup>();
        var agents = env.GetComponentsInChildren<ChefAgent>(true);
        if (group == null || agents.Length < 2) return "★ KitchenGroup / ChefAgent 2명을 찾지 못했다";
        System.Array.Sort(agents, (x, y) => x.AgentIndex - y.AgentIndex);

        var sb = new StringBuilder();
        bool allOk = true;

        allOk &= SamePlateRoundTrips(sb, env, group, agents);
        allOk &= PrepPotDumpLoop(sb, env, group, agents);
        allOk &= WrongOrderServeClawsBack(sb, env, group, agents);
        allOk &= HonestPipelineStillPaid(sb, env, group, agents);
        allOk &= TimeoutSettlesExactly(sb, env, group, agents);
        allOk &= HonestServeIsNotClawedBack(sb, env, group, agents);
        allOk &= FreshPlateShuttleIsNotProfitable(sb, env, group, agents);
        allOk &= TransferPotDumpLoop(sb, env, group, agents);
        allOk &= CommittedPotPlateShuttle(sb, env, group, agents);
        allOk &= WrongRecipeCommitIsCounted(sb, env, group, agents);
        allOk &= UndercookedScoopIsCounted(sb, env, group, agents);
        allOk &= ObservationCountIsActuallyMeasured(sb, agents[0]);

        sb.AppendLine();
        sb.AppendLine(allOk ? "==> 회귀 검사 전부 통과" : "==> ★ 회귀 검사 실패");
        return sb.ToString();
    }

    // 1) 같은 빈 그릇을 카운터로 10왕복. 전달 보상은 첫 건널목 한 번만 나와야 한다.
    static bool SamePlateRoundTrips(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        var counter = env.Counters[0];

        // 냄비를 채워서 '담을 요리가 있는' 상태로 만든다. 빈 그릇의 전달 보상은
        // 이 조건에서만 나오므로(KitchenEnv.IsWantedNow), 이게 없으면 이 검사는
        // 지급이 0인 것을 '왕복 차단이 동작한다'로 잘못 읽는다.
        CommitPot(env);

        // 접시는 **딱 하나**를 처음에 한 번만 쥐여준다. 매 왕복 새로 쥐여주면 그건
        // 그때마다 다른 물건이라 보상이 나오는 게 맞고, 어뷰징을 재현한 게 아니다.
        agents[1].GiveForTest(ItemType.EmptyPlate);
        float a0 = agents[0].GetCumulativeReward();

        for (int trip = 0; trip < 10; trip++)
        {
            Act(env, agents[1], counter, keepHeld: true);   // B 놓기
            Act(env, agents[0], counter);                   // A 집기
            Act(env, agents[0], counter, keepHeld: true);   // A 되놓기
            Act(env, agents[1], counter);                   // B 집기
        }

        // A는 왕복마다 2번 행동한다(집기/되놓기). 스텝 비용은 그만큼만 뺀다.
        float gain = agents[0].GetCumulativeReward() - a0 - 20f * StepCost(agents[0]);
        int paid = Mathf.RoundToInt(gain / TransferReward(agents[0]));
        bool ok = paid == 1;

        sb.AppendLine("[1] 같은 접시 10왕복   전달보상 " + paid + "회 (1 기대), A 개인 +"
                      + gain.ToString("0.00") + "   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 2) 손질 -> 투입 -> 비우기 3회. 팀 보상 합이 0 이하여야 한다.
    static bool PrepPotDumpLoop(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        var box = env.GetStation(StationType.GreenBox);
        var prep = env.GetPrepFor(0);
        var pot = env.Pot;
        float team0 = group.TotalGroupReward;

        for (int i = 0; i < 3; i++)
        {
            Act(env, agents[0], box);                     // 집기
            Act(env, agents[0], prep, keepHeld: true);    // 손질
            Act(env, agents[0], pot, keepHeld: true);     // 투입
            Act(env, agents[0], pot);                     // 비우기 (빈손)
        }

        float team = group.TotalGroupReward - team0;
        bool ok = team <= 0.001f;
        sb.AppendLine("[2] 손질->투입->비우기 3회   팀보상 " + team.ToString("+0.00;-0.00;0.00")
                      + " (0 이하 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 3) 완성한 요리를 '주문에 없는' 상태로 제출. 진행 보상이 회수되어야 한다.
    static bool WrongOrderServeClawsBack(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);

        var box = env.GetStation(StationType.GreenBox);
        var prep = env.GetPrepFor(0);
        var pot = env.Pot;
        float team0 = group.TotalGroupReward;

        for (int i = 0; i < RecipeTypeExtensions.Capacity; i++)
        {
            Act(env, agents[0], box);
            Act(env, agents[0], prep, keepHeld: true);
            Act(env, agents[0], pot, keepHeld: true);
        }
        float teamAfterCook = group.TotalGroupReward - team0;

        pot.TickCooking(999f, 0f);
        Act(env, agents[0], pot, ItemType.EmptyPlate);        // 요리를 담는다
        float creditOnDish = agents[0].HeldCreditForTest;
        var dish = agents[0].HeldItem;

        // 주문을 전부 RedSoup으로 바꿔서 방금 만든 GreenSoup이 팔리지 않게 한다
        ForceAllOrders(env, RecipeType.RedSoup);

        // 서빙구는 B 구역이므로 B가 낸다. 크레딧도 같이 옮겨야 실제 경로와 같다.
        Act(env, agents[0], env.Counters[0], keepHeld: true); // A가 카운터에 놓고
        Act(env, agents[1], env.Counters[0]);                 // B가 집는다 (크레딧 승계)
        float creditOnPartner = agents[1].HeldCreditForTest;
        Act(env, agents[1], env.GetStation(StationType.ServingHatch), keepHeld: true);

        float team = group.TotalGroupReward - team0;
        // 채울 때는 주문이 있었고(GreenSoup) 제출할 때 사라졌다 -> 확정 불일치 0, 제출 불일치 1
        int committedWrong = group.OrderMissThisEpisode(KitchenGroup.OrderMiss.PotCommittedWrong);
        int servedWrong = group.OrderMissThisEpisode(KitchenGroup.OrderMiss.ServedWrongOrder);
        bool ok = creditOnDish > 0.001f && creditOnPartner > 0.001f && team <= 0.001f
                  && committedWrong == 0 && servedWrong == 1;

        sb.AppendLine("[3] 주문에 없는 요리 제출   조리 후 팀보상 +" + teamAfterCook.ToString("0.00")
                      + " -> 요리에 실린 크레딧 " + creditOnDish.ToString("0.00")
                      + " -> 동료에게 승계 " + creditOnPartner.ToString("0.00")
                      + " -> 제출 후 팀보상 " + team.ToString("+0.00;-0.00;0.00") + " (0 이하 기대)"
                      + " / 불일치(확정/제출) " + committedWrong + "/" + servedWrong + " (0/1 기대)   " + Verdict(ok));
        sb.AppendLine("    (제출한 요리 = " + dish + ", 서빙 성공 " + env.DishesServed + "회)");
        Reset(env, agents);
        return ok;
    }

    // 10) 처음부터 주문에 없는 레시피로 냄비를 채우면 '확정 불일치'로 세야 한다.
    //     [3]은 채운 뒤에 주문이 사라지는 경우다. 둘을 가르는 것이 이 지표의 목적이다.
    static bool WrongRecipeCommitIsCounted(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.RedSoup);   // 주문은 전부 RedSoup인데 초록 2개로 채운다

        var box = env.GetStation(StationType.GreenBox);
        var prep = env.GetPrepFor(0);
        var pot = env.Pot;
        for (int i = 0; i < RecipeTypeExtensions.Capacity; i++)
        {
            Act(env, agents[0], box);
            Act(env, agents[0], prep, keepHeld: true);
            Act(env, agents[0], pot, keepHeld: true);
        }

        int committed = pot.IsCommitted ? 1 : 0;
        int committedWrong = group.OrderMissThisEpisode(KitchenGroup.OrderMiss.PotCommittedWrong);
        int servedWrong = group.OrderMissThisEpisode(KitchenGroup.OrderMiss.ServedWrongOrder);
        bool ok = committed == 1 && committedWrong == 1 && servedWrong == 0;

        sb.AppendLine("[10] 주문에 없는 레시피로 냄비 확정   확정 " + committed + "회"
                      + " / 불일치(확정/제출) " + committedWrong + "/" + servedWrong + " (1/0 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 11) 덜 끓은 냄비를 그릇으로 뜨면 '헛도리'로 세고, 다 끓은 뒤 뜨는 건 세지 않아야 한다.
    //     memory on/off 비교에서 '조리 완료 시점을 기억하는가'를 직접 보는 지표다.
    static bool UndercookedScoopIsCounted(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);

        var box = env.GetStation(StationType.GreenBox);
        var prep = env.GetPrepFor(0);
        var pot = env.Pot;
        for (int i = 0; i < RecipeTypeExtensions.Capacity; i++)
        {
            Act(env, agents[0], box);
            Act(env, agents[0], prep, keepHeld: true);
            Act(env, agents[0], pot, keepHeld: true);
        }

        Act(env, agents[0], pot, ItemType.EmptyPlate);       // 조리 중에 뜬다 -> 헛도리
        int early = group.PotNotReadyThisEpisode;
        bool stillPlate = agents[0].HeldItem == ItemType.EmptyPlate;

        pot.TickCooking(999f, 0f);
        Act(env, agents[0], pot, keepHeld: true);             // 다 끓은 뒤 뜬다 -> 정상
        int after = group.PotNotReadyThisEpisode;
        bool gotDish = agents[0].HeldItem.IsCookedDish();

        bool ok = early == 1 && stillPlate && after == 1 && gotDish;
        sb.AppendLine("[11] 덜 끓은 냄비 뜨기   헛도리 조리 중 " + early + "회 (1 기대)"
                      + " -> 완료 후 " + after + "회 (1 기대), 요리 담김 " + gotDish + "   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 4) 정상 파이프라인의 전달 2회는 그대로 보상받아야 한다.
    static bool HonestPipelineStillPaid(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);   // 초록/GreenSoup이 '쓸모 있는' 상태로 고정
        var counter = env.Counters[0];
        var box = env.GetStation(StationType.GreenBox);

        // (1) B가 손질한 재료를 A에게
        Act(env, agents[1], box);
        Act(env, agents[1], env.GetPrepFor(1), keepHeld: true);
        Act(env, agents[1], counter, keepHeld: true);
        float a0 = agents[0].GetCumulativeReward();
        Act(env, agents[0], counter);
        float gain1 = agents[0].GetCumulativeReward() - a0 - StepCost(agents[0]);
        bool paid1 = gain1 > TransferReward(agents[0]) * 0.5f;

        // (2) A가 완성 요리를 B에게. 냄비를 거쳤으니 '새 물건'이라 다시 보상받아야 한다.
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        var pot = env.Pot;
        ItemType dummy;
        var ing = env.NeedsPrep ? ItemType.PrepGreen : ItemType.RawGreen;
        pot.Interact(0, ing, out dummy);
        pot.Interact(0, ing, out dummy);
        pot.TickCooking(999f, 0f);
        Act(env, agents[0], pot, ItemType.EmptyPlate);
        Act(env, agents[0], counter, keepHeld: true);
        float b0 = agents[1].GetCumulativeReward();
        Act(env, agents[1], counter);
        float gain2 = agents[1].GetCumulativeReward() - b0 - StepCost(agents[1]);
        bool paid2 = gain2 > TransferReward(agents[1]) * 0.5f;

        bool ok = paid1 && paid2;
        sb.AppendLine("[4] 정상 파이프라인   손질재료 B->A +" + gain1.ToString("0.00")
                      + " / 완성요리 A->B +" + gain2.ToString("0.00")
                      + " (둘 다 보상 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 5) 전달까지 하고 타임아웃. 지급한 만큼만 회수되어야 한다.
    //
    //    예전 검사는 ConsumeUnrealizedCredit()의 반환값만 봤다. 그래서 '집어간 뒤 빈
    //    카운터에 크레딧이 남는' 결함을 놓쳤다. 이제 실제 KitchenGroup 종료 경로까지
    //    돌려서 팀 보상 합계로 판정한다.
    static bool TimeoutSettlesExactly(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        float team0 = group.TotalGroupReward;

        // B가 손질해서 카운터로 -> A가 집는다 (카운터는 비지만 크레딧 기록이 남을 수 있다)
        Act(env, agents[1], env.GetStation(StationType.GreenBox));
        Act(env, agents[1], env.GetPrepFor(1), keepHeld: true);
        Act(env, agents[1], env.Counters[0], keepHeld: true);
        Act(env, agents[0], env.Counters[0]);

        float earned = group.TotalGroupReward - team0;
        ForceTimeout(env, group);

        float total = group.TotalGroupReward - team0;
        // RecordStats가 누적값을 비우므로 '방금 끝난 에피소드' 값을 읽는다.
        float clawed = group.LastEpisodeCreditClawedBack;
        // A가 든 재료는 한 번 건너왔으므로 전달 보상 1회분이 개인 보상에서 회수되어야 한다.
        float transferClawed = group.LastEpisodeTransferClawedBack;
        bool ok = Mathf.Abs(total) < 0.001f && Mathf.Abs(clawed - earned) < 0.001f
                  && Mathf.Abs(transferClawed - TransferReward(agents[0])) < 0.001f;

        sb.AppendLine("[5] 전달 후 타임아웃   지급 +" + earned.ToString("0.00")
                      + " / 회수 " + clawed.ToString("0.00") + " (지급액과 같아야 함)"
                      + " / 최종 팀보상 " + total.ToString("+0.00;-0.00;0.00") + " (0.00 기대)"
                      + " / 전달보상 회수 " + transferClawed.ToString("0.00")
                      + " (" + TransferReward(agents[0]).ToString("0.00") + " 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 5b) 정상 서빙은 회수되면 안 된다.
    //     이미 실현된 진행이 빈 카운터에 남은 찌꺼기 때문에 도로 빼앗기던 경로다.
    static bool HonestServeIsNotClawedBack(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        float team0 = group.TotalGroupReward;

        var pot = env.Pot;
        var counter = env.Counters[0];

        // A가 재료 2개를 손질해 냄비에 (진행 보상 +1.0)
        for (int i = 0; i < RecipeTypeExtensions.Capacity; i++)
        {
            Act(env, agents[0], env.GetStation(StationType.GreenBox));
            Act(env, agents[0], env.GetPrepFor(0), keepHeld: true);
            Act(env, agents[0], pot, keepHeld: true);
        }
        pot.TickCooking(999f, 0f);

        // B가 빈 그릇을 카운터로 -> A가 받아 요리를 담고 -> 카운터로 -> B가 서빙
        Act(env, agents[1], env.GetStation(StationType.PlateStack));
        Act(env, agents[1], counter, keepHeld: true);
        Act(env, agents[0], counter);
        Act(env, agents[0], pot, keepHeld: true);          // 요리를 담는다
        Act(env, agents[0], counter, keepHeld: true);
        Act(env, agents[1], counter);
        Act(env, agents[1], env.GetStation(StationType.ServingHatch), keepHeld: true);

        int served = env.DishesServed;
        bool goal = env.IsGoalReached;
        ForceTimeout(env, group);

        float total = group.TotalGroupReward - team0;
        float clawed = group.LastEpisodeCreditClawedBack;
        // 그릇과 요리가 한 번씩 건너왔지만 둘 다 서빙으로 실현됐다. 전달 보상도 회수되면 안 된다.
        float transferClawed = group.LastEpisodeTransferClawedBack;
        float expected = 1.0f + group.RewardServe + (goal ? group.RewardGoalBonus : 0f);
        // 체인 진단 지표: 이 시나리오는 모든 고리를 정확히 한 번씩 지난다.
        string chain = "";
        bool chainOk = true;
        foreach (KitchenGroup.ChainStep step in System.Enum.GetValues(typeof(KitchenGroup.ChainStep)))
        {
            int n = group.LastEpisodeChain(step);
            chainOk &= n == 1;
            chain += (chain.Length > 0 ? "/" : "") + n;
        }

        bool ok = served == 1 && clawed < 0.001f && transferClawed < 0.001f
                  && Mathf.Abs(total - expected) < 0.001f && chainOk;

        sb.AppendLine("[5b] 정상 서빙 후 종료   서빙 " + served + "회"
                      + " / 회수 " + clawed.ToString("0.00") + " (0.00 기대)"
                      + " / 전달보상 회수 " + transferClawed.ToString("0.00") + " (0.00 기대)"
                      + " / 팀보상 " + total.ToString("0.00") + " (기대 " + expected.ToString("0.00") + ")"
                      + " / 체인(냄비확정/그릇→A/뜨기/요리→B) " + chain + " (1/1/1/1 기대)   "
                      + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 7) **매번 새 그릇**을 왕복시키는 것이 이득이면 안 된다.
    //
    //    검사 [1]은 '같은 물건'의 왕복만 막는 것을 확인한다. 어뷰징은 그 바깥에 있었다 -
    //    매 사이클 새 그릇을 꺼내면 전달 기록이 없는 새 물건이라 [1]에 걸리지 않고,
    //    그릇은 max_ingredients에도 진행 크레딧 회수에도 걸리지 않았다.
    //    담을 요리가 없는데 그릇을 나르는 것은 손해여야 한다.
    static bool FreshPlateShuttleIsNotProfitable(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);

        // 냄비는 비워둔다. 담을 요리가 없는 상태가 이 검사의 전제다.
        var counter = env.Counters[0];
        var hatch = env.GetStation(StationType.ServingHatch);
        var plates = env.GetStation(StationType.PlateStack);

        float before = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward()
                       + group.TotalGroupReward;

        const int Cycles = 3;
        for (int i = 0; i < Cycles; i++)
        {
            Act(env, agents[1], plates);                   // B: 새 그릇 (+0.05)
            Act(env, agents[1], counter, keepHeld: true);  // B: 카운터에 놓기
            Act(env, agents[0], counter);                  // A: 집기  <- 여기서 전달 보상이 나오면 안 된다
            Act(env, agents[0], counter, keepHeld: true);  // A: 되놓기
            Act(env, agents[1], counter);                  // B: 회수
            Act(env, agents[1], hatch, keepHeld: true);    // B: 버린다 (-0.2)
        }

        float gain = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward()
                     + group.TotalGroupReward - before;
        bool ok = gain < 0f;

        sb.AppendLine("[7] 새 그릇 " + Cycles + "회 왕복(담을 요리 없음)   개인+팀 합계 "
                      + gain.ToString("+0.00;-0.00;0.00") + " (0 미만 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 8) 건너온 재료를 냄비에 넣고 비우기를 반복해도 이득이 없어야 한다.
    //
    //    [2]는 A 혼자 집고 손질해서 넣고 비운다. 그 경로에는 전달 보상이 없어서 검사가
    //    통과했지만, 사이에 B -> A 전달을 끼우면 전달 보상(양쪽 +0.15)이 되돌려지지 않아
    //    사이클당 개인 합계 +0.30이 남았다. 팀 보상은 회수되니 [2]로는 안 보인다.
    //    커리큘럼 관문은 개인 보상만 보므로, 이 경로로 서빙 없이 lesson0를 통과했다.
    static bool TransferPotDumpLoop(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        var counter = env.Counters[env.Counters.Count - 1];
        var box = env.GetStation(StationType.GreenBox);
        var pot = env.Pot;

        float before = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward();
        float team0 = group.TotalGroupReward;
        int steps = 0;

        const int Cycles = 3;
        for (int i = 0; i < Cycles; i++)
        {
            Act(env, agents[1], box); steps++;                                    // B: 집기 (+0.05)
            if (env.NeedsPrep) { Act(env, agents[1], env.GetPrepFor(1), keepHeld: true); steps++; }
            Act(env, agents[1], counter, keepHeld: true); steps++;                // B: 카운터에
            Act(env, agents[0], counter); steps++;                                // A: 집기 (양쪽 +0.15)
            Act(env, agents[0], pot, keepHeld: true); steps++;                    // A: 투입
            Act(env, agents[0], pot); steps++;                                    // A: 비우기
        }

        // 스텝 비용을 빼고 본다. 스텝 비용 덕분에 음수가 되는 것은 방어가 아니다.
        float gain = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward()
                     - before - steps * StepCost(agents[0]);
        float team = group.TotalGroupReward - team0;
        bool ok = gain <= 0.001f && team <= 0.001f;

        sb.AppendLine("[8] 전달->투입->비우기 " + Cycles + "회   개인 합계(스텝비용 제외) "
                      + gain.ToString("+0.00;-0.00;0.00") + " / 팀 " + team.ToString("+0.00;-0.00;0.00")
                      + " (둘 다 0 이하 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 9) 냄비가 차 있는(담을 요리가 있는) 동안 새 그릇을 건넸다가 되받아 버려도 이득이 없어야 한다.
    //
    //    [7]은 냄비가 빈 경우만 본다. 냄비를 채워 두고 요리를 안 뜨면 IsCommitted가 계속
    //    참이라 그릇은 계속 '쓸모 있음'이고, 전달 보상(양쪽 +0.15)이 버리기(-0.2)보다 커서
    //    사이클당 개인 합계 +0.15가 남았다.
    static bool CommittedPotPlateShuttle(StringBuilder sb, KitchenEnv env, KitchenGroup group, ChefAgent[] agents)
    {
        Reset(env, agents);
        ForceAllOrders(env, RecipeType.GreenSoup);
        CommitPot(env);

        var counter = env.Counters[0];
        var hatch = env.GetStation(StationType.ServingHatch);
        var plates = env.GetStation(StationType.PlateStack);

        float before = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward();
        float team0 = group.TotalGroupReward;

        const int Cycles = 3;
        for (int i = 0; i < Cycles; i++)
        {
            Act(env, agents[1], plates);                   // B: 새 그릇 (+0.05)
            Act(env, agents[1], counter, keepHeld: true);  // B: 카운터에
            Act(env, agents[0], counter);                  // A: 집기 (양쪽 +0.15, 담을 요리가 있으니 지급은 정상)
            Act(env, agents[0], counter, keepHeld: true);  // A: 되놓기
            Act(env, agents[1], counter);                  // B: 회수
            Act(env, agents[1], hatch, keepHeld: true);    // B: 버린다 -> 전달 보상도 회수되어야 한다
        }

        float gain = agents[0].GetCumulativeReward() + agents[1].GetCumulativeReward()
                     - before - 6 * Cycles * StepCost(agents[0]);
        float team = group.TotalGroupReward - team0;
        bool ok = env.Pot.IsCommitted && gain < 0f && team <= 0.001f;

        sb.AppendLine("[9] 냄비가 찬 동안 새 그릇 " + Cycles + "회 왕복   개인 합계(스텝비용 제외) "
                      + gain.ToString("+0.00;-0.00;0.00") + " (0 미만 기대)   " + Verdict(ok));
        Reset(env, agents);
        return ok;
    }

    // 6) 관측 개수 검사가 '진짜로' 세는가.
    //    VectorSensor.GetObservationSpec()은 생성자 인자를 그대로 돌려주므로,
    //    그걸 읽던 예전 검사는 관측이 1개여도 103으로 통과했다.
    static bool ObservationCountIsActuallyMeasured(StringBuilder sb, ChefAgent agent)
    {
        // (a) 실제 에이전트는 선언값과 같아야 한다
        int actual = StartupValidator.CountObservations(agent);

        // (b) 일부러 1개만 넣은 센서를 1로 세는가 (검사가 껍데기가 아닌지)
        var probe = new Unity.MLAgents.Sensors.VectorSensor(ChefAgent.ObservationSize);
        probe.AddObservation(1f);
        int specSays = probe.GetObservationSpec().Shape[0];
        var field = typeof(Unity.MLAgents.Sensors.VectorSensor).GetField("m_Observations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = field?.GetValue(probe) as System.Collections.Generic.List<float>;
        int counted = list?.Count ?? -1;

        bool ok = actual == ChefAgent.ObservationSize && counted == 1 && specSays != 1;
        sb.AppendLine("[6] 관측 개수 검사   실측 " + actual + " (선언 " + ChefAgent.ObservationSize + ")"
                      + " / 1개만 넣은 센서: 실측 " + counted + " vs Spec " + specSays
                      + " (Spec은 못 믿는다는 확인)   " + Verdict(ok));
        return ok;
    }

    // ── 도우미 ────────────────────────────────────────────────

    // 그 스테이션을 마주 보는 칸에 셰프를 세우고, Interact 한 번을 **실제 행동으로** 넣는다.
    static void Act(KitchenEnv env, ChefAgent agent, Station station,
                    ItemType held = ItemType.None, bool keepHeld = false)
    {
        for (int dir = 0; dir < 4; dir++)
        {
            var stand = station.Cell + KitchenEnv.Directions[dir];
            if (!env.IsWalkable(stand, agent.AgentIndex)) continue;

            // stand에서 station을 보려면 dir의 반대 방향을 봐야 한다.
            int facing = dir == 0 ? 1 : dir == 1 ? 0 : dir == 2 ? 3 : 2;
            agent.PlaceForTest(stand, facing);
            // keepHeld면 손을 그대로 둔다 -- 물건에 딸린 기록(전달 플래그/크레딧)이
            // 살아 있어야 검사가 의미가 있다.
            if (!keepHeld) agent.GiveForTest(held);

            agent.OnActionReceived(new ActionBuffers(
                new ActionSegment<float>(new float[0]),
                new ActionSegment<int>(new[] { 0, InteractAction })));
            return;
        }
    }

    // 타이머를 만료시키고 **실제 종료 경로**(KitchenGroup.FixedUpdate)를 태운다.
    // 정산 함수를 직접 부르면 종료 경로에 연결이 빠져 있어도 검사가 통과한다.
    static void ForceTimeout(KitchenEnv env, KitchenGroup group)
    {
        var timer = typeof(KitchenEnv).GetField("m_EpisodeTimer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        timer.SetValue(env, env.EpisodeDuration + 1f);

        var fixedUpdate = typeof(KitchenGroup).GetMethod("FixedUpdate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fixedUpdate.Invoke(group, null);
    }

    // 냄비를 재료로 채워 '담을 요리가 있는' 상태(IsCommitted)로 만든다.
    // 조리 완료까지는 가지 않는다 - 빈 그릇의 쓸모 판정은 IsCommitted만 본다.
    // 냄비를 직접 조작하므로 팀 보상이 붙지 않는다 -> 보상 측정에 섞이지 않는다.
    static void CommitPot(KitchenEnv env)
    {
        var pot = env.Pot;
        if (pot == null) return;

        var ing = env.NeedsPrep ? ItemType.PrepGreen : ItemType.RawGreen;
        for (int i = 0; i < RecipeTypeExtensions.Capacity; i++) pot.Interact(0, ing, out _);
    }

    static void ForceAllOrders(KitchenEnv env, RecipeType recipe)
    {
        var field = typeof(OrderBoard).GetField("m_Slots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slots = (OrderBoard.Slot[])field.GetValue(env.Orders);
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new OrderBoard.Slot { Active = true, Recipe = recipe, Remaining = 999f };
    }

    static void Reset(KitchenEnv env, ChefAgent[] agents)
    {
        env.ResetEnv();
        foreach (var agent in agents) agent.ResetAgentState();

        // 시나리오 간 집계 격리. 앞 시나리오의 회수액이 섞이면 측정이 무의미해진다.
        var group = env.GetComponent<KitchenGroup>();
        if (group != null) group.ClearEpisodeStatsForTest();
    }

    static string Verdict(bool ok)
    {
        return ok ? "OK" : "★ 실패";
    }

    static float TransferReward(ChefAgent agent)
    {
        return FloatField(agent, "rewardTransfer");
    }

    static float StepCost(ChefAgent agent)
    {
        return FloatField(agent, "rewardPerStep");
    }

    static float FloatField(ChefAgent agent, string name)
    {
        var field = typeof(ChefAgent).GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (float)field.GetValue(agent);
    }
}

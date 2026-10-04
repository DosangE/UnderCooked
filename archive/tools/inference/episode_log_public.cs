// Episode log for the 6-dish code (same row format as episode_log.cs, public APIs only).
// Unity's RunCommand tool (unity-mcp) rejects System.Reflection, so episode_log.cs cannot run there.
// This version reads KitchenGroup.LastEpisodeChain / LastEpisodeDishesServed / OrderMissThisEpisode /
// WrongDishSeconds and counts expired orders from the order board.
//
// Run with Unity_RunCommand after pressing Play (trainer started first, e.g. --inference).
// Rows are appended to `path` as episodes finish. Summarize with archive/tools/analyze_episodes.py.
// Row: served,target,duration,potCommitted,dishesTaken,dishesToB,potWrong,servedWrong,ordersExpired,wrongDishSeconds,serveTimes;
//
// Notes:
// - The episode already running when the hook attaches is skipped.
// - ordersExpired: a slot whose remaining time jumps up while it had < 0.6 s left counts as expired.
//   A serve that consumes an order in its last 0.6 s is miscounted as an expiry (rare).
// - potWrong / servedWrong / wrongDishSeconds are the last editor frame's snapshot before the reset,
//   so a step in the very last frame can be missed (same as episode_log.cs).
// - The episode-ending serve resets the timer in the same frame, so serveTimes lists target-1 times.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string path = "D:/PCUBE/UnderCooked/results/eval_undercooked_blue_urgent3_episodes/episodes.txt";
        System.IO.File.WriteAllText(path, "");
        var envs = new List<KitchenEnv>();
        foreach (var e in Object.FindObjectsByType<KitchenEnv>(FindObjectsSortMode.None)) if (e.isActiveAndEnabled) envs.Add(e);
        int n = envs.Count;
        var lastT = new float[n]; var lastServed = new int[n]; var started = new bool[n];
        var serveTimes = new string[n]; var potW = new int[n]; var servW = new int[n]; var wsec = new float[n]; var expired = new int[n];
        var slotRem = new float[n, 3];
        for (int k = 0; k < n; k++) { lastT[k] = envs[k].EpisodeElapsed; serveTimes[k] = ""; for (int i = 0; i < 3; i++) slotRem[k, i] = envs[k].Orders.GetSlot(i).Remaining; }

        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < n; k++)
            {
                var e = envs[k]; if (e == null) continue;
                var g = e.GetComponent<KitchenGroup>();
                float t = e.EpisodeElapsed;
                bool reset = t < lastT[k] - 0.5f;
                if (reset)
                {
                    if (started[k])
                    {
                        sb.Append(g.LastEpisodeDishesServed).Append(',').Append(e.TargetDishes).Append(',').Append(lastT[k].ToString("0.00")).Append(',')
                          .Append(g.LastEpisodeChain(KitchenGroup.ChainStep.PotCommitted)).Append(',')
                          .Append(g.LastEpisodeChain(KitchenGroup.ChainStep.DishTaken)).Append(',')
                          .Append(g.LastEpisodeChain(KitchenGroup.ChainStep.DishToServeSide)).Append(',')
                          .Append(potW[k]).Append(',').Append(servW[k]).Append(',').Append(expired[k]).Append(',').Append(wsec[k].ToString("0.00")).Append(',')
                          .Append(serveTimes[k].TrimEnd('/')).Append(';');
                    }
                    started[k] = true; serveTimes[k] = ""; lastServed[k] = 0; expired[k] = 0;
                }
                else
                {
                    // an order slot refilled at ~0 s left = expired (not counted across a reset)
                    for (int i = 0; i < e.Orders.ActiveSlots; i++)
                    {
                        float rem = e.Orders.GetSlot(i).Remaining;
                        if (rem > slotRem[k, i] + 1f && slotRem[k, i] < 0.6f) expired[k]++;
                    }
                }
                for (int i = 0; i < 3; i++) slotRem[k, i] = e.Orders.GetSlot(i).Remaining;
                if (e.DishesServed > lastServed[k]) { serveTimes[k] += t.ToString("0.0") + "/"; lastServed[k] = e.DishesServed; }
                potW[k] = g.OrderMissThisEpisode(KitchenGroup.OrderMiss.PotCommittedWrong);
                servW[k] = g.OrderMissThisEpisode(KitchenGroup.OrderMiss.ServedWrongOrder);
                wsec[k] = g.WrongDishSeconds;
                lastT[k] = t;
            }
            if (sb.Length > 0) System.IO.File.AppendAllText(path, sb.ToString());
        };
        EditorApplication.update += cb;
        result.Log("hooked kitchens {0}", n);
    }
}

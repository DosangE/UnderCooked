// Order outcome observer. Run with mcp__unity-mcp__Unity_RunCommand while Play is running
// (training or inference). Read-only: it never touches agents, rewards or the order board.
// For ~3 minutes it watches every active kitchen's order slots and counts, per recipe,
// how each order ended: served (slot refilled early) or expired (refilled at ~0 s left).
// Replace __OUT__ with the output path, e.g. D:/PCUBE/UnderCooked/results/<run>_orders.txt.
// An order cut off by an episode reset is counted as served (small bias toward served).
// Used for archive/runs/undercooked_blue_long8_g995/order_outcomes.txt.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string path = "__OUT__";
        var envs = new List<KitchenEnv>();
        foreach (var e in Object.FindObjectsByType<KitchenEnv>(FindObjectsSortMode.None)) if (e.gameObject.activeInHierarchy) envs.Add(e);
        var prevRecipe = new int[envs.Count, 3];
        var prevRemain = new float[envs.Count, 3];
        for (int k = 0; k < envs.Count; k++) for (int i = 0; i < 3; i++) { prevRecipe[k, i] = -1; prevRemain[k, i] = 0; }
        var served = new int[6]; var expired = new int[6]; var appeared = new int[6];
        double start = EditorApplication.timeSinceStartup;
        double lastWrite = start;

        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            bool done = !EditorApplication.isPlaying || EditorApplication.timeSinceStartup - start > 180;
            if (!done)
            {
                for (int k = 0; k < envs.Count; k++)
                {
                    var o = envs[k].Orders;
                    for (int i = 0; i < o.ActiveSlots; i++)
                    {
                        var s = o.GetSlot(i);
                        int r = (int)s.Recipe;
                        if (prevRecipe[k, i] >= 0 && s.Remaining > prevRemain[k, i] + 1f)
                        {
                            // slot refilled: decide how the previous order ended
                            if (prevRemain[k, i] < 0.6f) expired[prevRecipe[k, i]]++; else served[prevRecipe[k, i]]++;
                            appeared[r]++;
                        }
                        prevRecipe[k, i] = r; prevRemain[k, i] = s.Remaining;
                    }
                }
            }
            if (done || EditorApplication.timeSinceStartup - lastWrite > 20)
            {
                lastWrite = EditorApplication.timeSinceStartup;
                string[] names = { "G2", "GR", "R2", "B2", "GB", "RB" };
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"elapsed {EditorApplication.timeSinceStartup - start:0}s kitchens {envs.Count} (an order ending at an episode reset is counted as served; small bias)");
                for (int r = 0; r < 6; r++)
                {
                    int tot = served[r] + expired[r];
                    sb.AppendLine($"{names[r]}: ended {tot} served {served[r]} expired {expired[r]} ({(tot > 0 ? 100.0 * expired[r] / tot : 0):0}% expired) newlyAppeared {appeared[r]}");
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                if (done) EditorApplication.update -= cb;
            }
        };
        EditorApplication.update += cb;
        result.Log("observing {0} kitchens for 180 s", envs.Count);
    }
}

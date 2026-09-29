// Records one row per finished episode in every kitchen while a trainer (e.g. --inference) drives the agents.
// Run with UnityMCP execute_code after pressing Play. Read the result with
//   UnityEditor.SessionState.GetString("ucFail", "")
// Row: served,target,duration,potCommitted,dishesTaken,dishesToB,potWrong,servedWrong,ordersExpired,serveTimes(a/b/c);
// Counters other than served are the last frame's snapshot before the reset, so a step in the very last frame can be missed.
var envs = UnityEngine.Object.FindObjectsByType<KitchenEnv>(UnityEngine.FindObjectsSortMode.None);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var fExp = typeof(KitchenGroup).GetField("m_OrdersExpired", bf);
var fChain = typeof(KitchenGroup).GetField("m_Chain", bf);
if (fExp == null || fChain == null) return "field missing";
var last = new System.Collections.Generic.Dictionary<KitchenEnv, string>();
var lastT = new System.Collections.Generic.Dictionary<KitchenEnv, float>();
var lastServed = new System.Collections.Generic.Dictionary<KitchenEnv, int>();
var serveTimes = new System.Collections.Generic.Dictionary<KitchenEnv, string>();
foreach (var e in envs) { lastT[e] = e.EpisodeElapsed; last[e] = ""; lastServed[e] = 0; serveTimes[e] = ""; }
UnityEditor.SessionState.SetString("ucFail", "");
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying) { UnityEditor.EditorApplication.update -= cb; return; }
    foreach (var e in envs)
    {
        if (e == null || !e.isActiveAndEnabled) continue;
        var g = e.GetComponent<KitchenGroup>();
        float t = e.EpisodeElapsed;
        if (t < lastT[e] - 0.5f)
        {
            string row = g.LastEpisodeDishesServed + "," + e.TargetDishes + "," + lastT[e].ToString("0.00") + "," + last[e] + "," + serveTimes[e].TrimEnd('/') + ";";
            UnityEditor.SessionState.SetString("ucFail", UnityEditor.SessionState.GetString("ucFail", "") + row);
            serveTimes[e] = ""; lastServed[e] = 0;
        }
        if (e.DishesServed > lastServed[e]) { serveTimes[e] += t.ToString("0.0") + "/"; lastServed[e] = e.DishesServed; }
        var ch = (int[])fChain.GetValue(g);
        last[e] = ch[0] + "," + ch[2] + "," + ch[3] + "," + g.OrderMissThisEpisode(KitchenGroup.OrderMiss.PotCommittedWrong) + "," + g.OrderMissThisEpisode(KitchenGroup.OrderMiss.ServedWrongOrder) + "," + (int)fExp.GetValue(g);
        lastT[e] = t;
    }
};
UnityEditor.EditorApplication.update += cb;
int active = 0; foreach (var e in envs) if (e.isActiveAndEnabled) active++;
return "hooked kitchens " + active;

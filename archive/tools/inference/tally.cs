var envs = UnityEngine.Object.FindObjectsByType<KitchenEnv>(UnityEngine.FindObjectsSortMode.None);
var timerField = typeof(KitchenEnv).GetField("m_EpisodeTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
if (timerField == null) return "no m_EpisodeTimer";
var last = new System.Collections.Generic.Dictionary<KitchenEnv, float>();
foreach (var e in envs) last[e] = (float)timerField.GetValue(e);
UnityEditor.SessionState.SetString("ucTally", "");
UnityEngine.Time.timeScale = 10f;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying) { UnityEditor.EditorApplication.update -= cb; return; }
    foreach (var e in envs)
    {
        if (e == null || !e.isActiveAndEnabled) continue;
        float t = (float)timerField.GetValue(e);
        if (t < last[e] - 0.5f)
        {
            var g = e.GetComponent<KitchenGroup>();
            string row = g.LastEpisodeDishesServed + "," + g.LastEpisodeChain(KitchenGroup.ChainStep.PotCommitted) + "," + g.LastEpisodeChain(KitchenGroup.ChainStep.DishTaken) + "," + g.LastEpisodeChain(KitchenGroup.ChainStep.DishToServeSide) + "," + last[e].ToString("0.0") + ";";
            UnityEditor.SessionState.SetString("ucTally", UnityEditor.SessionState.GetString("ucTally", "") + row);
        }
        last[e] = t;
    }
};
UnityEditor.EditorApplication.update += cb;
int active = 0; foreach (var e in envs) if (e.isActiveAndEnabled) active++;
return "kitchens active " + active + " / humanPlay " + envs[0].HumanPlay + " / timeScale " + UnityEngine.Time.timeScale;

var bps = UnityEngine.Object.FindObjectsByType<Unity.MLAgents.Policies.BehaviorParameters>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
int withModel = 0, heur = 0;
foreach (var bp in bps) { if (bp.Model != null) withModel++; if (bp.IsInHeuristicMode()) heur++; }
var env = UnityEngine.Object.FindFirstObjectByType<KitchenEnv>();
return "playing " + UnityEditor.EditorApplication.isPlaying + " / bps " + bps.Length + " / withModel " + withModel + " / heuristic " + heur
    + " / target " + env.TargetDishes + " prep " + env.NeedsPrep;

// Pot-fill diagnostic hook. Run with mcp__unity-mcp__Unity_RunCommand AFTER Play has started
// (an inference trainer must be driving the agents). Replace __OUT__ with the csv path,
// e.g. D:/PCUBE/UnderCooked/results/diag_<run>/fills.csv (the folder must exist).
// Analyze with: python archive/tools/analyze_pot_fills.py <fills.csv>
// One row per pot fill (commit) or dump: first/second ingredient, who brought it (A or over the counter from B),
// the order board at each moment, the cooked recipe, and whether no order wanted it. Used on 2026-10-04.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

internal class CommandScript : IRunCommand
{
    class K
    {
        public KitchenEnv env; public ChefAgent a; public ChefAgent b;
        public int prevTotal; public bool prevCommitted;
        public ItemType prevAHeld = ItemType.None; public string aSrc = "?";
        public ItemType[] prevCounters = new ItemType[4];
        public string first = ""; public string firstSrc = ""; public string firstOrders = ""; public float firstT;
        public float prevT;
    }

    static string Orders(KitchenEnv e)
    {
        var o = e.Orders; var parts = new List<string>();
        for (int i = 0; i < o.ActiveSlots; i++)
        {
            var s = o.GetSlot(i);
            if (s.Active) parts.Add((int)s.Recipe + ":" + s.Remaining.ToString("0.0"));
        }
        return string.Join("|", parts);
    }

    static string Ing(ItemType item)
    {
        return item.TryGetIngredient(out var ing) ? ((int)ing).ToString() : "-";
    }

    public void Execute(ExecutionResult result)
    {
        const string path = "__OUT__";
        System.IO.File.WriteAllText(path, "kitchen,event,t1,first,firstSrc,ordersAtFirst,t2,second,secondSrc,ordersAtSecond,cooked,wrong,counters,bHeld\n");
        var list = new List<K>();
        foreach (var e in Object.FindObjectsByType<KitchenEnv>(FindObjectsSortMode.None))
        {
            var k = new K { env = e };
            foreach (var ag in e.GetComponentsInChildren<ChefAgent>(true)) { if (ag.AgentIndex == 0) k.a = ag; else k.b = ag; }
            list.Add(k);
        }
        var names = new Dictionary<K, int>(); for (int i = 0; i < list.Count; i++) names[list[i]] = i;

        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            var sb = new System.Text.StringBuilder();
            foreach (var k in list)
            {
                var e = k.env; var pot = e.Pot; if (pot == null) continue;
                float t = e.EpisodeElapsed;
                if (t < k.prevT - 0.5f) { k.prevTotal = 0; k.prevCommitted = false; k.first = ""; }
                k.prevT = t;

                var aHeld = k.a.HeldItem;
                if (aHeld != k.prevAHeld && aHeld.IsIngredient())
                {
                    bool fromCounter = false;
                    for (int i = 0; i < e.Counters.Count && i < 4; i++)
                        if (k.prevCounters[i] == aHeld && e.Counters[i].CounterItem != aHeld) fromCounter = true;
                    if (!(k.prevAHeld.IsRawIngredient() && aHeld.IsPreppedIngredient())) k.aSrc = fromCounter ? "B" : "A";
                }

                int total = pot.TotalCount;
                if (total == 1 && k.prevTotal == 0)
                {
                    k.first = Ing(k.prevAHeld); k.firstSrc = k.aSrc; k.firstOrders = Orders(e); k.firstT = t;
                    if (k.first == "-") { for (int i = 0; i < IngredientTypeExtensions.Count; i++) if (pot.Count((IngredientType)i) > 0) k.first = i.ToString(); k.firstSrc = "?"; }
                }
                if (total == 0 && k.prevTotal == 1 && !pot.IsCommitted)
                {
                    sb.Append(names[k]).Append(",dump,").Append(k.firstT.ToString("0.0")).Append(',').Append(k.first).Append(',').Append(k.firstSrc).Append(',').Append(k.firstOrders)
                      .Append(",").Append(t.ToString("0.0")).Append(",,,").Append(Orders(e)).Append(",,,,\n");
                    k.first = "";
                }
                if (pot.IsCommitted && !k.prevCommitted)
                {
                    string first = k.first, firstSrc = k.firstSrc, firstOrders = k.firstOrders;
                    string second; string secondSrc = k.aSrc;
                    var c = pot.Counts;
                    if (k.prevTotal == 0 || first == "")
                    {
                        first = "?"; second = "?"; firstSrc = "?"; secondSrc = "?"; firstOrders = Orders(e);
                    }
                    else
                    {
                        int f = int.Parse(first); second = "?";
                        for (int i = 0; i < IngredientTypeExtensions.Count; i++)
                            if (c[i] - (i == f ? 1 : 0) > 0) second = i.ToString();
                        if (Ing(k.prevAHeld) == "-") secondSrc = "?";
                    }
                    var counters = new List<string>();
                    foreach (var ct in e.Counters) counters.Add(Ing(ct.CounterItem));
                    bool wrong = !e.Orders.HasOrderFor(pot.CookedRecipe.Dish());
                    sb.Append(names[k]).Append(",commit,").Append(k.firstT.ToString("0.0")).Append(',').Append(first).Append(',').Append(firstSrc).Append(',').Append(firstOrders)
                      .Append(',').Append(t.ToString("0.0")).Append(',').Append(second).Append(',').Append(secondSrc).Append(',').Append(Orders(e))
                      .Append(',').Append((int)pot.CookedRecipe).Append(',').Append(wrong ? 1 : 0)
                      .Append(',').Append(string.Join("|", counters)).Append(',').Append(Ing(k.b.HeldItem)).Append('\n');
                    k.first = "";
                }

                k.prevTotal = total; k.prevCommitted = pot.IsCommitted; k.prevAHeld = aHeld;
                for (int i = 0; i < e.Counters.Count && i < 4; i++) k.prevCounters[i] = e.Counters[i].CounterItem;
            }
            if (sb.Length > 0) System.IO.File.AppendAllText(path, sb.ToString());
        };
        EditorApplication.update += cb;
        result.Log("hooked kitchens {0}", list.Count);
    }
}

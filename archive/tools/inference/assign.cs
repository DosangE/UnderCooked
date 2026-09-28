var model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/_InferenceCheck/undercooked.onnx");
if (model == null) return "model not loaded";
int n = 0;
foreach (var bp in UnityEngine.Object.FindObjectsByType<Unity.MLAgents.Policies.BehaviorParameters>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var so = new UnityEditor.SerializedObject(bp);
    so.FindProperty("m_Model").objectReferenceValue = model;
    so.ApplyModifiedProperties();
    n++;
}
// Final lesson values from configs/undercooked.yaml (what undercooked_final trained on).
int k = 0; string missing = "";
foreach (var env in UnityEngine.Object.FindObjectsByType<KitchenEnv>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var so = new UnityEditor.SerializedObject(env);
    System.Action<string, float> setF = (name, v) => { var p = so.FindProperty(name); if (p == null) { missing += name + " "; return; } if (p.propertyType == UnityEditor.SerializedPropertyType.Integer) p.intValue = (int)v; else p.floatValue = v; };
    setF("defaultTargetDishes", 3f);
    setF("defaultRecipePoolSize", 3f);
    setF("defaultCookTime", 5f);
    setF("defaultOrderSlots", 3f);
    setF("defaultOrderDuration", 25f);
    setF("defaultMaxIngredients", 2f);
    var prep = so.FindProperty("defaultNeedsPrep"); if (prep == null) missing += "defaultNeedsPrep "; else prep.boolValue = true;
    so.ApplyModifiedProperties();
    k++;
}
return "model on " + n + " agents / params on " + k + " kitchens / missing: [" + missing + "]";

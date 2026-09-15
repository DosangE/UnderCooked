using UnityEditor;
using UnityEngine;

// Play 모드에서 보상 회귀 검사를 돌리는 메뉴.
// 결과는 콘솔에 남는다. 실패하면 LogError라 콘솔에서 바로 눈에 띈다.
public static class UnderCookedMenu
{
    [MenuItem("UnderCooked/보상 회귀 검사 %#t")]
    public static void RunSelfTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UnderCooked] Play 모드에서 실행할 것.");
            return;
        }

        string report = KitchenSelfTest.RunAll();
        if (report.Contains("실패")) Debug.LogError("[UnderCooked] 보상 회귀 검사\n" + report);
        else Debug.Log("[UnderCooked] 보상 회귀 검사\n" + report);
    }
}

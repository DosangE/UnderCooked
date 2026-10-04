var go = UnityEngine.GameObject.Find("_DemoCam");
if (go == null) { go = new UnityEngine.GameObject("_DemoCam"); go.AddComponent<UnityEngine.Camera>(); }
var cam = go.GetComponent<UnityEngine.Camera>();
var center = new UnityEngine.Vector3(-5.5f, 0f, 5.5f);
go.transform.position = center + new UnityEngine.Vector3(0f, 10.5f, -6.5f);
go.transform.LookAt(center + new UnityEngine.Vector3(0f, 0f, 0.3f));
cam.fieldOfView = 50f;
cam.depth = -10;
var main = UnityEngine.Camera.main;
cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor;
if (cam.targetTexture == null) cam.targetTexture = new UnityEngine.RenderTexture(720, 540, 24);
UnityEditor.EditorApplication.delayCall += () => {
  cam.Render();
  var rt = cam.targetTexture; var prev = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = rt;
  var tex = new UnityEngine.Texture2D(rt.width, rt.height, UnityEngine.TextureFormat.RGB24, false);
  tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
  UnityEngine.RenderTexture.active = prev;
  System.IO.File.WriteAllBytes(@"<FRAMES_DIR>/test.png", tex.EncodeToPNG());
};
return "cam ready";

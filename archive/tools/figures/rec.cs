var cam = UnityEngine.GameObject.Find("_DemoCam").GetComponent<UnityEngine.Camera>();
KitchenEnv env = null;
foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenEnv>(UnityEngine.FindObjectsSortMode.None)) if (e.name == "TrainingArea_09") env = e;
UnityEngine.Time.timeScale = 1f;
string dir = @"<FRAMES_DIR>/";
float last = env.EpisodeElapsed;
int state = 0; int idx = 0; float nextShot = 0f; float startT = 0f;
var tex = new UnityEngine.Texture2D(cam.targetTexture.width, cam.targetTexture.height, UnityEngine.TextureFormat.RGB24, false);
UnityEditor.SessionState.SetString("ucRec", "waiting");
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () => {
  if (!UnityEditor.EditorApplication.isPlaying) { UnityEditor.EditorApplication.update -= cb; return; }
  float t = env.EpisodeElapsed;
  bool reset = t < last - 0.5f; last = t;
  if (state == 0) { if (reset) { state = 1; startT = UnityEngine.Time.time; nextShot = startT; } else return; }
  else if (reset || UnityEngine.Time.time - startT > 90f) {
    var g = env.GetComponent<KitchenGroup>();
    UnityEditor.SessionState.SetString("ucRec", "done frames " + idx + " served " + g.LastEpisodeDishesServed + " len " + (UnityEngine.Time.time - startT).ToString("0.0"));
    UnityEditor.EditorApplication.update -= cb; return;
  }
  if (UnityEngine.Time.time < nextShot) return;
  nextShot += 0.1f;
  cam.Render();  // without this the texture can be black when the Game view is not drawing
  var prev = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = cam.targetTexture;
  tex.ReadPixels(new UnityEngine.Rect(0, 0, tex.width, tex.height), 0, 0); tex.Apply();
  UnityEngine.RenderTexture.active = prev;
  System.IO.File.WriteAllBytes(dir + idx.ToString("0000") + ".png", tex.EncodeToPNG());
  var ob = env.Orders; var line = new System.Text.StringBuilder(); line.Append(idx + "|" + env.DishesServed + "|" + env.EpisodeElapsed.ToString("0.0")); for (int si = 0; si < 3; si++) { var sl = ob.GetSlot(si); line.Append("|" + (sl.Active ? sl.Recipe + ":" + sl.Remaining.ToString("0.0") + ":" + ob.Duration.ToString("0") : "-")); } System.IO.File.AppendAllText(dir + "meta.txt", line.ToString() + System.Environment.NewLine);
  idx++;
  UnityEditor.SessionState.SetString("ucRec", "recording " + idx);
};
UnityEditor.EditorApplication.update += cb;
return "armed, timer " + last;

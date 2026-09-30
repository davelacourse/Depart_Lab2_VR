// Captures d'écran par caméra temporaire rendue dans une RenderTexture (fonctionne sans Game View).
using System.IO;
using UnityEngine;

public static class StationCapture
{
    public const string Dir = "Screenshots";

    public static string Render(string fileName, Vector3 pos, Vector3 lookAt, float fov = 60f, bool ortho = false, float orthoSize = 10f,
        int w = 1600, int h = 900, Color? clear = null)
    {
        Directory.CreateDirectory(Dir);
        var go = new GameObject("__TempCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        try
        {
            go.transform.position = pos;
            var fwd = lookAt - pos;
            var up = Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(fwd, up);
            cam.fieldOfView = fov;
            cam.orthographic = ortho;
            cam.orthographicSize = orthoSize;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            if (clear.HasValue) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = clear.Value; }
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var path = Path.Combine(Dir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return Path.GetFullPath(path);
        }
        finally { Object.DestroyImmediate(go); }
    }
}

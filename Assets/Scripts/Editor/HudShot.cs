using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Play-mode screenshot with the UI (for checks through MCP): the game view's screen capture misses
    /// Screen Space - Overlay canvases while the editor is not focused, so every enabled overlay canvas is switched to
    /// camera space for one render into a texture, saved to Temp/Shots/&lt;name&gt;.png, and switched back.
    /// </summary>
    public static class HudShot
    {
        public static string Take(string name, int width = 1280, int height = 720)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return "no camera";
            }

            var canvases = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases.Add(c);
                }
            }

            var rt = new RenderTexture(width, height, 24);
            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.02f;
            }

            Canvas.ForceUpdateCanvases();
            RenderTexture old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/Shots");
            System.IO.Directory.CreateDirectory(folder);
            string path = System.IO.Path.Combine(folder, name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            rt.Release();
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}

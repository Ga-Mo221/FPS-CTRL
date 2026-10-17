// [UI-INTEGRATION 2026-09-12 - Aemy] FILE MỚI (thêm cho việc ghép UI / hệ skin).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Shop.EditorTools
{
    /// <summary>
    /// EDITOR-ONLY: render 1 prefab vu khi (co the da gan skin) ra PNG nen TRONG SUOT lam anh card shop.
    ///
    /// Vi shader 'Quibli/Stylized Lit' render TOI trong render off-screen (no can setup rieng cua Quibli),
    /// khi RENDER PREVIEW ta thay bang material proxy 'URP/Lit' sao chep _BaseMap/_BaseColor/_BumpMap/_EmissionColor
    /// -> len mau chuan. In-game van dung Quibli that (ApplyTo). Preview render trong 1 scene tam co Directional
    /// Light that (duong render URP that). Nen trong suot: render 2 lan (nen den + nen trang) roi suy ra alpha.
    /// </summary>
    public static class SkinPreviewRenderer
    {
        public static string Render(GameObject weaponPrefab, SkinDefinition skin, string outPath, int size = 512)
        {
            if (weaponPrefab == null) { Debug.LogError("[SkinPreview] weaponPrefab null."); return null; }

            Scene tmp = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            GameObject root = null;
            var proxies = new List<Material>();
            RenderTexture rt = null;
            Texture2D texB = null, texW = null, outTex = null;
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            try
            {
                Camera cam = null; Light sun = null;
                foreach (var go in tmp.GetRootGameObjects())
                {
                    if (cam == null) cam = go.GetComponentInChildren<Camera>();
                    if (sun == null) sun = go.GetComponentInChildren<Light>();
                }
                if (cam == null) { Debug.LogError("[SkinPreview] no camera in temp scene"); return null; }

                root = Object.Instantiate(weaponPrefab);
                SceneManager.MoveGameObjectToScene(root, tmp);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;

                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                    ps.gameObject.SetActive(false);

                // Thay material bang URP/Lit proxy (theo dung thu tu concat, khop skin.materials neu co).
                var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int idx = 0;
                foreach (var smr in smrs)
                {
                    var arr = smr.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        Material srcMat = arr[i];
                        if (skin != null && skin.materials != null && idx < skin.materials.Count && skin.materials[idx] != null)
                            srcMat = skin.materials[idx];
                        idx++;

                        var proxy = new Material(litShader);
                        if (srcMat != null)
                        {
                            if (srcMat.HasProperty("_BaseMap")) proxy.SetTexture("_BaseMap", srcMat.GetTexture("_BaseMap"));
                            if (srcMat.HasProperty("_BaseColor")) proxy.SetColor("_BaseColor", srcMat.GetColor("_BaseColor"));
                            if (srcMat.HasProperty("_BumpMap") && srcMat.GetTexture("_BumpMap") != null)
                            {
                                proxy.SetTexture("_BumpMap", srcMat.GetTexture("_BumpMap"));
                                proxy.EnableKeyword("_NORMALMAP");
                            }
                            if (srcMat.HasProperty("_EmissionColor"))
                            {
                                Color e = srcMat.GetColor("_EmissionColor");
                                if (e.maxColorComponent > 0.02f)
                                {
                                    proxy.SetColor("_EmissionColor", e * 1.5f);
                                    proxy.EnableKeyword("_EMISSION");
                                    proxy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                                }
                            }
                        }
                        proxy.SetFloat("_Smoothness", 0.45f);
                        proxies.Add(proxy);
                        arr[i] = proxy;
                    }
                    smr.sharedMaterials = arr;
                }

                bool has = false;
                Bounds b = new Bounds(Vector3.zero, Vector3.one * 0.1f);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
                float me = Mathf.Max(0.05f, b.extents.x, b.extents.y, b.extents.z);

                cam.orthographic = true;
                cam.orthographicSize = me * 1.12f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = me * 50f;
                Vector3 dir = new Vector3(0.65f, 0.35f, -1f).normalized;
                cam.transform.position = b.center - dir * (me * 8f);
                cam.transform.rotation = Quaternion.LookRotation(dir);

                if (sun != null) { sun.intensity = 1.45f; sun.color = Color.white; sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f); }

                rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.targetTexture = rt;

                // pass 1: nen den
                cam.backgroundColor = Color.black;
                cam.Render();
                RenderTexture.active = rt;
                texB = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texB.ReadPixels(new Rect(0, 0, size, size), 0, 0); texB.Apply();

                // pass 2: nen trang
                cam.backgroundColor = Color.white;
                cam.Render();
                RenderTexture.active = rt;
                texW = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texW.ReadPixels(new Rect(0, 0, size, size), 0, 0); texW.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                var pb = texB.GetPixels32();
                var pw = texW.GetPixels32();
                int n = Mathf.Min(pb.Length, pw.Length);
                var outPx = new Color32[n];
                for (int i = 0; i < n; i++)
                {
                    float d = ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / 3f;
                    float a = Mathf.Clamp01(1f - d / 255f);
                    if (a <= 0.004f) { outPx[i] = new Color32(0, 0, 0, 0); continue; }
                    float r = Mathf.Clamp(pb[i].r / a, 0f, 255f);
                    float g = Mathf.Clamp(pb[i].g / a, 0f, 255f);
                    float bl = Mathf.Clamp(pb[i].b / a, 0f, 255f);
                    outPx[i] = new Color32((byte)r, (byte)g, (byte)bl, (byte)(a * 255f));
                }

                outTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                outTex.SetPixels32(outPx); outTex.Apply();
                byte[] png = outTex.EncodeToPNG();

                string projRoot = Directory.GetParent(Application.dataPath).FullName;
                string full = Path.Combine(projRoot, outPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);

                AssetDatabase.ImportAsset(outPath);
                var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
                Debug.Log("[SkinPreview] Rendered -> " + outPath + " (" + png.Length + " bytes)");
                return outPath;
            }
            finally
            {
                if (rt != null) { RenderTexture.active = null; Object.DestroyImmediate(rt); }
                if (texB != null) Object.DestroyImmediate(texB);
                if (texW != null) Object.DestroyImmediate(texW);
                if (outTex != null) Object.DestroyImmediate(outTex);
                foreach (var m in proxies) if (m != null) Object.DestroyImmediate(m);
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.CloseScene(tmp, true);
            }
        }
    }
}
#endif

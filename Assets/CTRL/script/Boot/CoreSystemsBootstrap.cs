// [Aemy 2026-10-27] FILE MOI. Bo manager song xuyen scene (Core Systems) tu sinh, khong dat rai rac tung scene.
using System.Collections.Generic;
using UnityEngine;

namespace Game.Boot
{
    /// <summary>
    /// Truoc khi scene dau tien Awake: sinh prefab Resources/CoreSystems (BackendBootstrap, Game Manager, TIS Setup,
    /// Setting Manager, AudioManager, SceneFader, Network Runner Handler) -> bam Play tu scene nao cung du manager
    /// (Boot, Login, MenuShell...). Truoc day cac object nay nam trong Login_Register.
    ///
    /// - Prefab goc de INACTIVE: Instantiate chua Awake gi ca; tach tung con ra root THEO THU TU trong prefab
    ///   -> moi manager Awake dung thu tu (vd Game Manager truoc Network Runner Handler) va DontDestroyOnLoad hop le (chi root).
    /// - Scene test tu mang bo manager rieng (vd ShortMap1 co object "Prefab" chua Game Manager) -> KHONG sinh, giu nguyen nhu cu.
    /// - Moi manager van tu chong trung (Instance guard) nhu truoc.
    /// </summary>
    public static class CoreSystemsBootstrap
    {
        public const string ResourcePath = "CoreSystems";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
            if (Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[CoreSystems] Scene dau da co bo manager rieng -> khong sinh Core Systems.");
                return;
            }

            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[CoreSystems] Khong tim thay Resources/" + ResourcePath + ".prefab");
                return;
            }

            var container = Object.Instantiate(prefab); // goc inactive -> cac con chua Awake
            var children = new List<Transform>();
            foreach (Transform c in container.transform) children.Add(c);
            foreach (var c in children)
            {
                c.SetParent(null, false);                 // thanh root + active -> Awake ngay, dung thu tu
                if (c != null) Object.DontDestroyOnLoad(c.gameObject);
            }
            Object.Destroy(container);
        }
    }
}

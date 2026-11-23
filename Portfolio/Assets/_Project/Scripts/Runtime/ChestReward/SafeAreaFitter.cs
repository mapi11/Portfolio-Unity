using UnityEngine;

namespace Portfolio.ChestReward
{
    /// Подгоняет RectTransform под безопасную зону экрана (вырезы, скруглённые углы, системные панели).
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect lastArea;
        Vector2Int lastScreen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != lastArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            lastArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = lastArea.position; var max = lastArea.position + lastArea.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}

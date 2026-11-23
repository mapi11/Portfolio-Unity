using System;
using System.Collections;
using UnityEngine;

namespace Portfolio.ChestReward
{
    /// Изинги и простой твин на корутинах для UI-анимаций экрана.
    public static class Ease
    {
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) => t * t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        public static float OutBack(float t, float s = 1.70158f) { t -= 1f; return t * t * ((s + 1f) * t + s) + 1f; }
        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f; if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        public static IEnumerator Tween(float duration, Action<float> step)
        {
            if (duration <= 0f) { step(1f); yield break; }
            float t = 0f;
            while (t < duration)
            {
                step(t / duration);
                yield return null;
                t += Time.deltaTime;
            }
            step(1f);
        }
    }
}

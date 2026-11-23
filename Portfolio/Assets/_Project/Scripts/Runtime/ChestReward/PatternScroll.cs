using UnityEngine;
using UnityEngine.UI;

namespace Portfolio.ChestReward
{
    /// Тайлит паттерн фона под размер экрана и медленно его прокручивает.
    [RequireComponent(typeof(RawImage))]
    public class PatternScroll : MonoBehaviour
    {
        public float tileSize = 384f;
        public Vector2 speed = new Vector2(0.02f, 0.035f);

        RawImage image;
        Vector2 offset;

        void Awake() => image = GetComponent<RawImage>();

        void Update()
        {
            var size = ((RectTransform)transform).rect.size;
            offset += speed * Time.deltaTime;
            offset.x %= 1f; offset.y %= 1f;
            image.uvRect = new Rect(offset, size / tileSize);
        }
    }
}

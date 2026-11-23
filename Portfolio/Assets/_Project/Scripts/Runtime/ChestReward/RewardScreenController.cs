using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Portfolio.ChestReward
{
    /// Сценарий экрана награды: появление → ожидание тапа → Shake → Open → вылет награды → итог.
    /// Повторные нажатия во время открытия игнорируются. Тап по готовому экрану (или кнопка) перезапускает
    /// всё со сбросом: анимации не накладываются, награда не дублируется, частицы очищаются.
    public class RewardScreenController : MonoBehaviour
    {
        public enum State { Intro, WaitTap, Opening, Done }

        [Header("Сундук")]
        public Animator chestAnimator;
        public ChestAnimEvents chestEvents;
        [Tooltip("Узел для падения сундука при появлении (локальная Y в пикселях канваса)")]
        public Transform chestDrop;
        [Tooltip("Узел для сплющивания при приземлении")]
        public Transform chestSquash;
        [Tooltip("Точка, откуда вылетает награда (щель между крышкой и основанием)")]
        public Transform chestMouth;
        public SpriteRenderer chestShadow;

        [Header("Эффекты у сундука")]
        public SpriteRenderer rays;
        public SpriteRenderer innerBeam;
        public SpriteRenderer innerGlow;
        public SpriteRenderer burstFlash;
        public ParticleSystem burstSparkles;
        public ParticleSystem ambientSparkles;

        [Header("UI")]
        public Camera uiCamera;
        public CanvasGroup background;
        public RectTransform title;
        public RectTransform slot;
        public Image slotGlow;
        public RectTransform shine;
        public RectTransform rewardFly;
        [Tooltip("Слой, в котором награда летит (поверх сундука)")]
        public RectTransform flyLayer;
        [Tooltip("Значок редкости: после приземления награда встаёт под него")]
        public RectTransform slotBadge;
        public ParticleSystem slotSparkles;
        [Tooltip("Тёплые лучи за слотом награды")]
        public SpriteRenderer slotRays;
        [Tooltip("Мерцающие звёздочки вокруг слота, пока награда на экране")]
        public ParticleSystem slotTwinkle;
        public RectTransform continueButton;
        public Text tapText;
        public Image screenFlash;
        public Button tapCatcher;

        [Header("Тайминги")]
        public float shakeTime = 0.85f;
        public float flyTime = 0.55f;

        public State Current { get; private set; }

        static readonly int ShakeId = Animator.StringToHash("Shake");
        static readonly int OpenId = Animator.StringToHash("Open");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        MaterialPropertyBlock mpb;
        Vector2 titlePos, slotPos, flyAnchor;
        bool lidBurst;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            titlePos = title.anchoredPosition;
            slotPos = slot.anchoredPosition;
            flyAnchor = rewardFly.anchorMin;
            tapCatcher.onClick.AddListener(OnTap);
            continueButton.GetComponent<Button>().onClick.AddListener(OnTap);
            chestEvents.LidBurst += () => lidBurst = true;
        }

        void Start() => Restart();

        // ---------- ввод ----------

        public void OnTap()
        {
            switch (Current)
            {
                case State.WaitTap: StartCoroutine(OpenSequence()); break;
                case State.Done: Restart(); break;
                default: break;   // во время появления и открытия нажатия игнорируются
            }
        }

        // ---------- сброс и появление ----------

        public void Restart()
        {
            StopAllCoroutines();
            Current = State.Intro;
            lidBurst = false;

            chestAnimator.Rebind();
            chestAnimator.Update(0f);
            chestDrop.localPosition = Vector3.zero;
            chestSquash.localScale = Vector3.one;

            SetFade(rays, 0f); SetFade(innerBeam, 0f); SetFade(innerGlow, 0f); SetFade(burstFlash, 0f); SetFade(slotRays, 0f);
            foreach (var ps in new[] { burstSparkles, ambientSparkles, slotSparkles, slotTwinkle })
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            rewardFly.SetParent(flyLayer, false);   // награда возвращается из слота: дублей нет
            rewardFly.anchorMin = rewardFly.anchorMax = flyAnchor;
            rewardFly.gameObject.SetActive(false);
            slot.localScale = Vector3.zero;
            SetAlpha(slotGlow, 0f);
            shine.gameObject.SetActive(false);
            continueButton.localScale = Vector3.zero;
            tapText.gameObject.SetActive(false);
            SetAlpha(screenFlash, 0f);
            title.localScale = Vector3.zero;

            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            background.alpha = 0f;
            StartCoroutine(Ease.Tween(0.25f, t => background.alpha = t));

            // заголовок падает сверху с перехлёстом
            StartCoroutine(Delayed(0.1f, Ease.Tween(0.45f, t =>
            {
                float e = Ease.OutBack(t, 2f);
                title.anchoredPosition = titlePos + Vector2.up * 220f * (1f - e);
                title.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
            })));

            // сундук падает и приземляется с пружиной
            float dropFrom = 1400f;
            SetShadow(0f);
            yield return Ease.Tween(0.38f, t =>
            {
                float e = Ease.InCubic(t);
                chestDrop.localPosition = Vector3.up * dropFrom * (1f - e);
                chestSquash.localScale = new Vector3(0.9f, 1.12f, 1f);
                SetShadow(e);
            });
            yield return Ease.Tween(0.45f, t =>
            {
                float sp = Mathf.Exp(-6f * t) * Mathf.Cos(t * Mathf.PI * 2f * 2.2f);
                chestSquash.localScale = new Vector3(1f + 0.22f * sp, 1f - 0.22f * sp, 1f);
            });

            tapText.text = "Tap to Open";
            tapText.gameObject.SetActive(true);
            StartCoroutine(Pulse(tapText.rectTransform));
            Current = State.WaitTap;
        }

        // ---------- открытие ----------

        IEnumerator OpenSequence()
        {
            Current = State.Opening;
            tapText.gameObject.SetActive(false);
            lidBurst = false;

            // тряска: из щели начинает пробиваться свет
            chestAnimator.SetTrigger(ShakeId);
            StartCoroutine(Ease.Tween(shakeTime, t => { SetFade(innerGlow, 0.45f * t * t); SetFade(rays, 0.12f * t); }));
            yield return new WaitForSeconds(shakeTime);
            chestAnimator.SetTrigger(OpenId);

            // ждём события из клипа: крышка распахнулась
            float wait = 0f;
            while (!lidBurst && wait < 1f) { wait += Time.deltaTime; yield return null; }
            Burst();

            yield return new WaitForSeconds(0.12f);
            yield return FlyReward();

            // кнопка и подсказка
            yield return new WaitForSeconds(0.15f);
            StartCoroutine(Ease.Tween(0.35f, t => continueButton.localScale = Vector3.one * Ease.OutBack(t, 2.2f)));
            yield return new WaitForSeconds(0.35f);
            tapText.text = "Tap to Continue";
            tapText.gameObject.SetActive(true);
            StartCoroutine(Pulse(tapText.rectTransform));
            Current = State.Done;
        }

        void Burst()
        {
            // вспышка, лучи, свет изнутри, искры
            StartCoroutine(Ease.Tween(0.3f, t =>
            {
                SetFade(burstFlash, 1f - Ease.OutQuad(t));
                burstFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, Ease.OutCubic(t));
            }));
            StartCoroutine(Ease.Tween(0.22f, t => SetAlpha(screenFlash, 0.28f * (1f - t))));
            StartCoroutine(Ease.Tween(0.5f, t =>
            {
                SetFade(rays, Mathf.Lerp(0.12f, 1f, Ease.OutCubic(t)));
                rays.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, Ease.OutBack(t, 1.4f));
                SetFade(innerBeam, Ease.OutCubic(t));
                SetFade(innerGlow, Mathf.Lerp(0.45f, 1f, t));
            }));
            burstSparkles.Play();
            ambientSparkles.Play();
            StartCoroutine(GlowBreath());
        }

        IEnumerator FlyReward()
        {
            var parent = (RectTransform)rewardFly.parent;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, chestMouth.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var from);
            Vector2 to = slotPos;
            Vector2 ctrl = new Vector2(Mathf.Lerp(from.x, to.x, 0.5f) + 120f, Mathf.Max(from.y, to.y) + 260f);

            rewardFly.gameObject.SetActive(true);
            bool slotShown = false;
            yield return Ease.Tween(flyTime, t =>
            {
                float e = Ease.OutCubic(t);
                Vector2 p = (1 - e) * (1 - e) * from + 2 * (1 - e) * e * ctrl + e * e * to;   // дуга Безье
                rewardFly.anchoredPosition = p;
                rewardFly.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.15f, e);
                rewardFly.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-200f, 0f, e));
                if (!slotShown && t > 0.35f) { slotShown = true; StartCoroutine(PopSlot()); }
            });

            // приземление на подложку: награда встаёт в слот под значок редкости
            rewardFly.SetParent(slot, true);
            rewardFly.SetSiblingIndex(slotBadge.GetSiblingIndex());
            rewardFly.anchorMin = rewardFly.anchorMax = new Vector2(0.5f, 0.5f);
            rewardFly.anchoredPosition = Vector2.zero;
            rewardFly.localRotation = Quaternion.identity;

            // пружина, блик, искры
            slotSparkles.transform.position = slot.position;
            slotSparkles.Play();
            StartCoroutine(Ease.Tween(0.45f, t =>
            {
                float sp = Mathf.Exp(-7f * t) * Mathf.Cos(t * Mathf.PI * 2f * 2f);
                rewardFly.localScale = Vector3.one * (1f + 0.15f * sp);
                slot.localScale = Vector3.one * (1f + 0.1f * sp);
            }));
            StartCoroutine(Shine());
            StartCoroutine(SlotGlowLoop());
        }

        IEnumerator PopSlot()
        {
            yield return Ease.Tween(0.3f, t => slot.localScale = Vector3.one * Ease.OutBack(t, 2f));
        }

        IEnumerator Shine()
        {
            shine.gameObject.SetActive(true);
            float w = ((RectTransform)shine.parent).rect.width;
            yield return Ease.Tween(0.5f, t => shine.anchoredPosition = new Vector2(Mathf.Lerp(-w, w, Ease.InOutSine(t)), 0f));
            shine.gameObject.SetActive(false);
        }

        // ореол вспыхивает при приземлении награды, за слотом разгораются тёплые лучи, вокруг мерцают звёздочки;
        // дальше всё спокойно «дышит»
        IEnumerator SlotGlowLoop()
        {
            var glowRT = slotGlow.rectTransform;
            slotTwinkle.Play();
            yield return Ease.Tween(0.4f, t =>
            {
                SetAlpha(slotGlow, Mathf.Clamp01(t * 3f));
                glowRT.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Ease.OutBack(t, 1.6f));
                SetFade(slotRays, Ease.OutCubic(t));
                slotRays.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, Ease.OutBack(t, 1.3f));
            });
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;
                float s = Mathf.Sin(time * 2.6f);
                SetAlpha(slotGlow, 0.85f + 0.15f * s);
                glowRT.localScale = Vector3.one * (1f + 0.025f * s);
                SetFade(slotRays, 0.85f + 0.15f * Mathf.Sin(time * 2.6f + 1.2f));
                yield return null;
            }
        }

        IEnumerator GlowBreath()
        {
            yield return new WaitForSeconds(0.5f);
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;
                SetFade(innerGlow, 0.85f + 0.15f * Mathf.Sin(time * 2.5f));
                SetFade(innerBeam, 0.85f + 0.15f * Mathf.Sin(time * 2.5f + 1f));
                yield return null;
            }
        }

        IEnumerator Pulse(RectTransform rt)
        {
            float time = 0f;
            var text = rt.GetComponent<Text>();
            while (true)
            {
                time += Time.deltaTime;
                rt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(time * 4f));
                var c = text.color; c.a = Mathf.Clamp01(time * 3f) * (0.75f + 0.25f * Mathf.Sin(time * 4f)); text.color = c;
                yield return null;
            }
        }

        IEnumerator Delayed(float delay, IEnumerator routine)
        {
            yield return new WaitForSeconds(delay);
            yield return routine;
        }

        // ---------- помощники ----------

        void SetFade(SpriteRenderer r, float v)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(FadeId, v);
            r.SetPropertyBlock(mpb);
            r.enabled = v > 0.001f;
        }

        void SetShadow(float a)
        {
            var c = chestShadow.color; c.a = 0.55f * a; chestShadow.color = c;
            chestShadow.transform.localScale = new Vector3(Mathf.Lerp(0.4f, 1f, a), Mathf.Lerp(0.4f, 1f, a), 1f);
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}

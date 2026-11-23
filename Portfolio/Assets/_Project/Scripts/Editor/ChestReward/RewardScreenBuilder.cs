using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Portfolio.ChestReward.EditorTools
{
    /// Собирает сцену экрана награды: Canvas (Screen Space - Camera) с якорями, сундук и эффекты внутри канваса,
    /// передний канвас с UI поверх сундука. Порядок отрисовки (Default layer):
    /// фон-канвас 0 → тень 4 → лучи 5 → крышка 10 → свет изнутри 11 → основание 12 → крышка 13 → кристалл 14 →
    /// вспышка/искры 20–25 → передний UI-канвас 50 → искры у слота 60.
    /// Tools > Portfolio > Chest Reward > Build Reward Screen
    public static class RewardScreenBuilder
    {
        const string R = "Assets/_Project/Art/ChestReward/";
        const string ArtUI = R + "Source/UI/";
        const string ArtRewards = R + "Source/Rewards/";
        const float ChestScale = 112f;     // 1 юнит сундука = 112 px канваса
        const float SeamY = 215f;          // высота щели крышки над низом сундука, px (верх основания ≈ 230)

        [MenuItem("Tools/Portfolio/Chest Reward/Build Reward Screen")]
        public static void Build()
        {
            PrepareImports();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- камера и пост-эффекты ----------
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true; cam.orthographicSize = 9.6f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.1f, 0.6f);
            var acd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            acd.renderPostProcessing = true;   // рендерер проекта по умолчанию

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = VolumeProfile();

            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // ---------- основной канвас: фон ----------
            var canvas = NewCanvas("RewardCanvas", null, 0, cam);
            var bgRoot = Stretch(Child(canvas.transform, "Background"));
            var bgGroup = bgRoot.gameObject.AddComponent<CanvasGroup>();
            var grad = Stretch(Child(bgRoot, "Gradient")).gameObject.AddComponent<Image>();
            grad.sprite = Sprite(ArtUI + "ShopChestPurpleGradient.png"); grad.raycastTarget = false;
            var pattern = Stretch(Child(bgRoot, "Pattern")).gameObject.AddComponent<RawImage>();
            pattern.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtUI + "Pattern_01_x384.psd");
            pattern.color = new Color(0.32f, 0.02f, 0.5f, 0.22f); pattern.raycastTarget = false;
            pattern.gameObject.AddComponent<PatternScroll>();
            var focus = Child(bgRoot, "CenterLight").gameObject.AddComponent<Image>();
            Anchor(focus.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1500, 1500));
            focus.sprite = Sprite(R + "Textures/T_SoftDot.png"); focus.color = new Color(1f, 0.75f, 1f, 0.28f); focus.raycastTarget = false;

            var safe = Stretch(Child(canvas.transform, "SafeArea"));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---------- сундук и эффекты (мировые объекты внутри канваса) ----------
            var chestAnchor = Child(safe, "ChestAnchor");
            Anchor(chestAnchor, new Vector2(0.5f, 0.405f), Vector2.zero);

            var shadowRoot = World(chestAnchor, "ShadowRoot", new Vector3(0, -12, 0), new Vector3(660, 120, 1));
            var shadow = SpriteChild(shadowRoot, "Shadow", R + "Textures/T_SoftDot.png", null, 6);   // поверх лучей (5)
            shadow.color = new Color(0.22f, 0.02f, 0.38f, 0.55f);

            var raysRoot = World(chestAnchor, "RaysRoot", new Vector3(0, 200, 0), new Vector3(1750, 1750, 1));
            var raysMat = Mat("M_Rays", "SG_Rays");
            raysMat.SetColor("_Color", new Color(1.0f, 0.66f, 1.15f, 0.5f));
            var rays = SpriteChild(raysRoot, "Rays", R + "Textures/T_Quad.png", raysMat, 5);

            var drop = World(chestAnchor, "ChestDrop", Vector3.zero, Vector3.one);
            var squash = World(drop, "ChestSquash", Vector3.zero, Vector3.one);
            var scaleT = World(squash, "ChestScale", Vector3.zero, Vector3.one * ChestScale);
            var chest = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(R + "Prefabs/Chest.prefab"), scaleT);
            var events = chest.AddComponent<ChestAnimEvents>();

            // свечение из щели — часть рига (по контуру кромки, на верхней кости тела); здесь только материалы
            var innerGlow = chest.transform.Find("Bones/Base/BaseTop/MouthGlow").GetComponent<SpriteRenderer>();
            innerGlow.sharedMaterial = GlowMat("M_ChestGlow_Inner", new Color(1.35f, 1.0f, 1.45f, 0.9f));
            var innerBeam = chest.transform.Find("Bones/Base/BaseTop/MouthLight").GetComponent<SpriteRenderer>();
            innerBeam.sharedMaterial = GlowMat("M_ChestGlow_Beam", new Color(1.1f, 0.85f, 1.3f, 0.6f));
            var flashRoot = World(squash, "BurstFlashRoot", new Vector3(0, SeamY + 20, 0), new Vector3(760, 760, 1));
            var flash = SpriteChild(flashRoot, "BurstFlash", R + "Textures/T_SoftDot.png", GlowMat("M_ChestGlow_Flash", new Color(1.5f, 1.25f, 1.7f, 0.85f)), 20);
            var mouth = World(squash, "Mouth", new Vector3(0, SeamY + 30, 0), Vector3.one);

            var sparkMat = ParticleMat("M_Sparkle", R + "Textures/T_Sparkle.png", new Color(1.6f, 1.3f, 2.2f, 1f));
            var burst = BurstSparkles(squash, sparkMat);
            var ambient = AmbientSparkles(squash, sparkMat);

            // ---------- лучи и мерцание за слотом награды ----------
            // мировые объекты в основном канвасе с тем же якорем, что слот на переднем: под UI (50), над фоном
            var slotAnchor = Child(safe, "SlotFxAnchor");
            Anchor(slotAnchor, new Vector2(0.5f, 0.755f), Vector2.zero);
            var slotRaysRoot = World(slotAnchor, "SlotRaysRoot", Vector3.zero, new Vector3(640, 640, 1));   // короткие лучи сразу за рамкой
            var slotRaysMat = Mat("M_SlotRays", "SG_Rays");
            slotRaysMat.SetColor("_Color", new Color(1.6f, 1.05f, 0.4f, 1f));
            slotRaysMat.SetFloat("_RayCount", 14f);
            slotRaysMat.SetFloat("_Speed", 0.02f);
            slotRaysMat.SetFloat("_RayWidth", 0.3f);
            slotRaysMat.SetFloat("_InnerRadius", 0.45f);
            EditorUtility.SetDirty(slotRaysMat);
            var slotRays = SpriteChild(slotRaysRoot, "SlotRays", R + "Textures/T_Quad.png", slotRaysMat, 45);
            var warmSparkMat = ParticleMat("M_SparkleWarm", R + "Textures/T_Sparkle.png", new Color(1.9f, 1.75f, 1.4f, 1f));
            var slotTwinkle = SlotTwinkle(slotAnchor, warmSparkMat);

            // ---------- передний канвас: UI поверх сундука ----------
            var front = NewCanvas("FrontUI", canvas.transform, 50, cam);
            var fsafe = Stretch(Child(front.transform, "SafeArea"));
            fsafe.gameObject.AddComponent<SafeAreaFitter>();
            var font = AssetDatabase.LoadAssetAtPath<Font>(R + "Source/Font/LilitaOne.ttf");

            var catcherImg = Stretch(Child(front.transform, "TapCatcher")).gameObject.AddComponent<Image>();
            catcherImg.color = new Color(0, 0, 0, 0);
            var catcher = catcherImg.gameObject.AddComponent<Button>(); catcher.transition = Selectable.Transition.None;
            catcherImg.transform.SetAsFirstSibling();

            var title = Label(fsafe, "Title", "Your Reward", font, 104, new Vector2(0.5f, 0.87f), new Vector2(1000, 160));

            var slot = Child(fsafe, "Slot"); Anchor(slot, new Vector2(0.5f, 0.755f), new Vector2(270, 270));
            var slotGlow = Child(slot, "SlotGlow").gameObject.AddComponent<Image>();
            Anchor(slotGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(450, 450));   // 270 / 0.6 — рамка по контуру подложки
            slotGlow.sprite = Sprite(R + "Textures/T_SlotGlow.png"); slotGlow.color = new Color(1f, 1f, 1f, 0f);   // цвет запечён в текстуре slotGlow.raycastTarget = false;
            var slotBg = Child(slot, "SlotBg").gameObject.AddComponent<Image>();
            Anchor(slotBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(270, 270)); slotBg.sprite = Sprite(ArtUI + "1_Slot_Violet_01.png"); slotBg.raycastTarget = false;
            var mask = Child(slot, "ShineMask"); Anchor(mask, new Vector2(0.5f, 0.5f), new Vector2(236, 236)); mask.gameObject.AddComponent<RectMask2D>();
            var shine = Child(mask, "Shine").gameObject.AddComponent<Image>();
            Anchor(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200, 300)); shine.sprite = Sprite(R + "Textures/T_ShineStripe.png"); shine.raycastTarget = false;
            var badge = Child(slot, "Badge").gameObject.AddComponent<Image>();
            Anchor(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(84, 84)); badge.rectTransform.anchoredPosition = new Vector2(-112, 112);
            badge.sprite = Sprite(ArtUI + "Slot_Type_Violet_01.png"); badge.raycastTarget = false;
            var badgeIcon = Child(badge.transform, "Icon").gameObject.AddComponent<Image>();
            Anchor(badgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(46, 46)); badgeIcon.sprite = Sprite(ArtUI + "GTW_Sword_Icon_01.png"); badgeIcon.raycastTarget = false;

            var flyLayer = Stretch(Child(fsafe, "FlyLayer"));
            var reward = Child(flyLayer, "RewardFly").gameObject.AddComponent<Image>();
            Anchor(reward.rectTransform, new Vector2(0.5f, 0.755f), new Vector2(220, 220));
            reward.sprite = Sprite(ArtRewards + "Weapon_03.png"); reward.preserveAspect = true; reward.raycastTarget = false;

            var btn = Child(fsafe, "ContinueButton").gameObject.AddComponent<Image>();
            Anchor(btn.rectTransform, new Vector2(0.5f, 0.293f), new Vector2(360, 140));
            btn.sprite = Sprite(ArtUI + "Btn_Big_Geen_01.png"); btn.type = Image.Type.Sliced;
            btn.gameObject.AddComponent<Button>();
            var gemIcon = Child(btn.transform, "Gem").gameObject.AddComponent<Image>();
            Anchor(gemIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(88, 88)); gemIcon.rectTransform.anchoredPosition = new Vector2(-70, 4);
            gemIcon.sprite = Sprite(ArtRewards + "Currency_Diamond_01.png"); gemIcon.raycastTarget = false;
            var price = Label(btn.transform, "Price", "500", font, 72, new Vector2(0.5f, 0.5f), new Vector2(200, 120));
            price.rectTransform.anchoredPosition = new Vector2(50, 4); price.alignment = TextAnchor.MiddleCenter;

            var tap = Label(fsafe, "TapText", "Tap to Open", font, 60, new Vector2(0.5f, 0.18f), new Vector2(900, 100));

            var screenFlash = Stretch(Child(front.transform, "ScreenFlash")).gameObject.AddComponent<Image>();
            screenFlash.color = new Color(1f, 0.92f, 1f, 0f); screenFlash.raycastTarget = false;

            var slotSparkles = SlotSparkles(front.transform, sparkMat);

            // ---------- контроллер ----------
            var ctrl = canvas.gameObject.AddComponent<RewardScreenController>();
            ctrl.chestAnimator = chest.GetComponent<Animator>();
            ctrl.chestEvents = events;
            ctrl.chestDrop = drop; ctrl.chestSquash = squash; ctrl.chestMouth = mouth; ctrl.chestShadow = shadow;
            ctrl.rays = rays; ctrl.innerBeam = innerBeam; ctrl.innerGlow = innerGlow; ctrl.burstFlash = flash;
            ctrl.burstSparkles = burst; ctrl.ambientSparkles = ambient;
            ctrl.uiCamera = cam; ctrl.background = bgGroup; ctrl.title = title.rectTransform;
            ctrl.slot = slot; ctrl.slotGlow = slotGlow; ctrl.shine = shine.rectTransform;
            ctrl.rewardFly = reward.rectTransform; ctrl.flyLayer = flyLayer; ctrl.slotBadge = badge.rectTransform;
            ctrl.slotSparkles = slotSparkles; ctrl.continueButton = btn.rectTransform; ctrl.tapText = tap;
            ctrl.slotRays = slotRays; ctrl.slotTwinkle = slotTwinkle;
            ctrl.screenFlash = screenFlash; ctrl.tapCatcher = catcher;

            EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/ChestReward.unity");
        }

        // ---------- импорт ----------

        static void PrepareImports()
        {
            foreach (var dir in new[] { ArtUI, ArtRewards })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".psd")) continue;
                    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                    ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.npotScale = TextureImporterNPOTScale.None;
                    if (path.EndsWith("Btn_Big_Geen_01.png")) ti.spriteBorder = new Vector4(32, 32, 32, 32);
                    if (path.EndsWith("ShopChestPurpleGradient.png")) ti.wrapMode = TextureWrapMode.Clamp;
                    ti.SaveAndReimport();
                }
            var pi = (TextureImporter)AssetImporter.GetAtPath(ArtUI + "Pattern_01_x384.psd");
            pi.textureType = TextureImporterType.Default; pi.wrapMode = TextureWrapMode.Repeat; pi.alphaIsTransparency = true; pi.SaveAndReimport();
        }

        // ---------- помощники ----------

        static Canvas NewCanvas(string name, Transform parent, int order, Camera cam)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            if (parent) { go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform); }
            var c = go.GetComponent<Canvas>();
            if (parent == null)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 5f;
                var s = go.AddComponent<CanvasScaler>();
                s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1080, 1920);
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            else { c.overrideSorting = true; }
            c.sortingOrder = order;
            return c;
        }

        static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt; }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size) { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero; }

        static Transform World(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = localPos; t.localScale = localScale;
            return t;
        }

        static SpriteRenderer SpriteChild(Transform parent, string name, string spritePath, Material mat, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = Sprite(spritePath);
            if (mat) sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }

        static Text Label(Transform parent, string name, string text, Font font, int size, Vector2 anchor, Vector2 box)
        {
            var t = Child(parent, name).gameObject.AddComponent<Text>();
            Anchor(t.rectTransform, anchor, box);
            t.font = font; t.text = text; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.22f, 0.02f, 0.32f, 1f); o.effectDistance = new Vector2(4, -4);
            var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0.15f, 0f, 0.25f, 0.8f); s.effectDistance = new Vector2(0, -8);
            return t;
        }

        static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static Material Mat(string name, string shaderGraph)
        {
            string p = R + "Materials/" + name + ".mat";
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(R + "Shaders/" + shaderGraph + ".shadergraph");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
            return m;
        }

        // отдельный материал свечения на каждую роль: своя яркость, а _Fade анимируется через MaterialPropertyBlock
        static Material GlowMat(string name, Color tint)
        {
            var m = Mat(name, "SG_ChestGlow");
            m.SetColor("_Tint", tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ParticleMat(string name, string texPath, Color hdr)
        {
            string p = R + "Materials/" + name + ".mat";
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem NewPS(Transform parent, string name, Vector3 pos, Material mat, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 120;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.sortingOrder = order;
            return ps;
        }

        static AnimationCurve Curve(params float[] tv)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < tv.Length; i += 2) c.AddKey(tv[i], tv[i + 1]);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0);
            return c;
        }

        static Gradient Fade(Color a, Color b, params float[] alphaTV)
        {
            var g = new Gradient();
            var ak = new GradientAlphaKey[alphaTV.Length / 2];
            for (int i = 0; i < ak.Length; i++) ak[i] = new GradientAlphaKey(alphaTV[i * 2 + 1], alphaTV[i * 2]);
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, ak);
            return g;
        }

        // Плоский веер вверх для 2D: Circle лежит в плоскости XY, дуга 120° повёрнута так, что её середина смотрит вверх.
        // (Cone и Box объёмные: одна ось уходит в глубину экрана, и в 2D разлёт сплющивается в линию.)
        static void FanUp(ParticleSystem ps, float radius, float arc)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius; sh.radiusThickness = 1f;
            sh.arc = arc;
            sh.rotation = new Vector3(0f, 0f, 90f - arc * 0.5f);
            sh.randomDirectionAmount = 0.12f;
        }

        // искры при открытии: вылетают веером из щели сундука и падают по дуге.
        // Порядок 11 — между открытой крышкой и передней стенкой: искры рождаются внутри сундука.
        static ParticleSystem BurstSparkles(Transform parent, Material mat)
        {
            var ps = NewPS(parent, "BurstSparkles", new Vector3(0, SeamY, 0), mat, 11);
            var m = ps.main; m.loop = false; m.duration = 0.5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(650f, 1350f);
            m.startSize = new ParticleSystem.MinMaxCurve(30f, 74f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 1f), new Color(1f, 0.6f, 1f));
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30), new ParticleSystem.Burst(0.06f, 18) });
            FanUp(ps, 90f, 120f);
            var f = ps.forceOverLifetime; f.enabled = true; f.space = ParticleSystemSimulationSpace.Local; f.y = new ParticleSystem.MinMaxCurve(-1500f);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 1.2f;
            lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;   // размеры в px канваса — иначе drag растёт в десятки раз
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.3f, 0.12f, 1f, 1f, 0f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, new Color(0.9f, 0.6f, 1f), 0f, 1f, 0.7f, 1f, 1f, 0f);
            return ps;
        }

        // пока сундук открыт, из него бьёт мягкий фонтанчик искр: вылетают вверх и опадают по дуге
        static ParticleSystem AmbientSparkles(Transform parent, Material mat)
        {
            var ps = NewPS(parent, "AmbientSparkles", new Vector3(0, SeamY, 0), mat, 11);
            var m = ps.main; m.loop = true; m.duration = 2f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(380f, 760f);
            m.startSize = new ParticleSystem.MinMaxCurve(24f, 54f);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.7f, 1f));
            var em = ps.emission; em.rateOverTime = 22f;
            FanUp(ps, 110f, 70f);
            var f = ps.forceOverLifetime; f.enabled = true; f.space = ParticleSystemSimulationSpace.Local; f.y = new ParticleSystem.MinMaxCurve(-650f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0f, 0.2f, 1f, 0.5f, 0.6f, 0.75f, 1f, 1f, 0f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 0f, 0f, 0.15f, 1f, 0.75f, 1f, 1f, 0f);
            return ps;
        }

        // искры при приземлении награды на слот
        // редкие звёздочки вылетают от краёв слота наружу, пока награда на экране
        static ParticleSystem SlotTwinkle(Transform parent, Material mat)
        {
            var ps = NewPS(parent, "SlotTwinkle", Vector3.zero, mat, 61);
            var m = ps.main; m.loop = true; m.duration = 2f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(55f, 120f);   // от края наружу
            m.startSize = new ParticleSystem.MinMaxCurve(18f, 36f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 0.6f);
            var em = ps.emission; em.rateOverTime = 3.5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 150f; sh.radiusThickness = 0f;   // рождаются у края слота, не поверх иконки
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0f, 0.35f, 1f, 1f, 0f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 0f, 0f, 0.3f, 1f, 1f, 0f);
            return ps;
        }

        static ParticleSystem SlotSparkles(Transform parent, Material mat)
        {
            var ps = NewPS(parent, "SlotSparkles", Vector3.zero, mat, 60);
            var m = ps.main; m.loop = false; m.duration = 0.3f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(320f, 760f);
            m.startSize = new ParticleSystem.MinMaxCurve(26f, 58f);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.65f, 1f));
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 110f;
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 2.6f;
            lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;   // размеры в px канваса — иначе drag растёт в десятки раз
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.4f, 0.15f, 1f, 1f, 0f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white, 0f, 1f, 0.7f, 1f, 1f, 0f);
            return ps;
        }

        static VolumeProfile VolumeProfile()
        {
            string p = R + "Settings/ChestVolume.asset";
            System.IO.Directory.CreateDirectory(R + "Settings");
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(p);
            if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, p); }
            if (!prof.TryGet(out Bloom bloom)) { bloom = prof.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, prof); }
            bloom.threshold.Override(1.05f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.6f); bloom.highQualityFiltering.Override(false);
            if (!prof.TryGet(out Tonemapping tm)) { tm = prof.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tm, prof); }
            tm.mode.Override(TonemappingMode.None);
            EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets();
            return prof;
        }
    }
}

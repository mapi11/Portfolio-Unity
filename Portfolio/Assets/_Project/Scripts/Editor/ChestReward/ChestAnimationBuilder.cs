using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Portfolio.ChestReward.EditorTools
{
    /// Клипы костной анимации сундука и Animator Controller: Idle → Shake → Open → Opened, сброс через Reset.
    /// Ключи задаются как смещения от позы префаба, поэтому риг можно перестроить, а анимации — пересобрать.
    /// Tools > Portfolio > Chest Reward > Build Chest Animations
    public static class ChestAnimationBuilder
    {
        const string Dir = "Assets/_Project/Art/ChestReward/Animations/";
        const string PrefabPath = "Assets/_Project/Art/ChestReward/Prefabs/Chest.prefab";

        const string Base = "Bones/Base";
        const string BaseTop = "Bones/Base/BaseTop";
        const string LidC = "Bones/Base/BaseTop/LidClosed";
        const string LidCTop = "Bones/Base/BaseTop/LidClosed/LidClosedTop";
        const string LidO = "Bones/Base/BaseTop/LidOpen";
        const string LidOMid = "Bones/Base/BaseTop/LidOpen/LidOpenMid";
        const string LidOTop = "Bones/Base/BaseTop/LidOpen/LidOpenMid/LidOpenTop";

        static GameObject prefab;

        [MenuItem("Tools/Portfolio/Chest Reward/Build Chest Animations")]
        public static void Build()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            System.IO.Directory.CreateDirectory(Dir);

            var idle = Clip("Chest_Idle", true, 1.6f);
            // дыхание: тело тянется вверх и чуть сужается (крышка едет вместе с телом)
            Key(idle, BaseTop, "m_LocalPosition.x", new[] { 0f, 0.8f, 1.6f }, new[] { 0f, 0.035f, 0f });
            Key(idle, Base, "m_LocalScale.y", new[] { 0f, 0.8f, 1.6f }, new[] { 0f, -0.018f, 0f });
            LidState(idle, closed: true);

            var shake = Clip("Chest_Shake", false, 0.9f);
            // нарастающее покачивание: наклоны всё сильнее; закрытая крышка не отрывается от основания, чтобы не было видно шва
            var ts = new[] { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f };
            Key(shake, BaseTop, "localEulerAnglesRaw.z", ts, new[] { 0f, 5f, -5f, 7f, -7f, 9f, -10f, 11f, -6f, 0f });
            Key(shake, "", "m_LocalPosition.x", ts, new[] { 0f, 0.05f, -0.05f, 0.06f, -0.06f, 0.08f, -0.08f, 0.09f, -0.04f, 0f });
            Key(shake, Base, "m_LocalScale.y", ts, new[] { 0f, 0.03f, -0.02f, 0.04f, -0.02f, 0.05f, -0.03f, 0.06f, 0.02f, 0.04f });
            Key(shake, BaseTop, "m_LocalPosition.x", ts, new[] { 0f, -0.02f, 0.01f, -0.03f, 0.01f, -0.04f, 0.01f, -0.05f, -0.08f, -0.1f });
            LidState(shake, closed: true);

            var open = Clip("Chest_Open", false, 1.0f);
            // 0–0.12 приседание, 0.12–0.2 рывок вверх и крышка вытягивается,
            // 0.2 крышка распахивается (смена внешней стороны на внутреннюю на самом быстром кадре),
            // дальше открытая крышка раскрывается с перехлёстом, тело пружинит при приземлении
            Key(open, BaseTop, "m_LocalPosition.x", new[] { 0f, 0.12f, 0.2f, 0.34f, 0.46f, 0.6f }, new[] { -0.1f, -0.2f, 0.12f, -0.16f, 0.06f, 0f });
            Key(open, Base, "m_LocalScale.y", new[] { 0f, 0.12f, 0.2f, 0.34f, 0.46f, 0.6f }, new[] { 0.04f, 0.12f, -0.08f, 0.14f, -0.04f, 0f });
            Key(open, "", "m_LocalPosition.y", new[] { 0f, 0.12f, 0.2f, 0.32f }, new[] { 0f, 0f, 0.28f, 0f });
            Key(open, LidCTop, "m_LocalPosition.x", new[] { 0f, 0.12f, 0.2f }, new[] { 0f, -0.05f, 0.55f });
            // крышка откидывается от петли: кость LidOpen стоит на линии стыка с основанием и масштабирует крышку
            // только по вертикали (её ось X смотрит вверх), без поворотов вбок; нижний край всё время лежит на основании
            Key(open, LidO, "m_LocalScale.x", new[] { 0f, 0.2f, 0.31f, 0.41f, 0.52f, 0.62f }, new[] { -0.85f, -0.85f, 0.14f, -0.07f, 0.03f, 0f });
            Step(open, "LidClosed", "m_Color.a", 0.2f, 1f, 0f);
            Step(open, "LidOpen", "m_Color.a", 0.2f, 0f, 1f);
            AnimationUtility.SetAnimationEvents(open, new[]
            {
                new AnimationEvent { time = 0.2f, functionName = "OnLidBurst" },
                new AnimationEvent { time = 0.34f, functionName = "OnLanded" },
            });

            var opened = Clip("Chest_Opened", true, 2f);
            // открытый сундук «дышит», крышка слегка покачивается
            Key(opened, LidO, "m_LocalScale.x", new[] { 0f, 1f, 2f }, new[] { 0f, 0.02f, 0f });
            Key(opened, BaseTop, "m_LocalPosition.x", new[] { 0f, 1f, 2f }, new[] { 0f, 0.025f, 0f });
            LidState(opened, closed: false);

            BuildController(idle, shake, open, opened);
            AssetDatabase.SaveAssets();
        }

        static AnimationClip Clip(string name, bool loop, float length)
        {
            string path = Dir + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop; s.stopTime = length;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            return clip;
        }

        static float DefaultValue(string path, string prop)
        {
            var t = string.IsNullOrEmpty(path) ? prefab.transform : prefab.transform.Find(path);
            if (prop.StartsWith("m_LocalPosition")) return Axis(t.localPosition, prop);
            if (prop.StartsWith("m_LocalScale")) return Axis(t.localScale, prop);
            if (prop.StartsWith("localEulerAnglesRaw")) { float z = t.localEulerAngles.z; return z > 180f ? z - 360f : z; }
            return 0f;
        }

        static float Axis(Vector3 v, string prop) => prop.EndsWith(".x") ? v.x : prop.EndsWith(".y") ? v.y : v.z;

        // ключи как смещения от позы префаба, плавные касательные
        static void Key(AnimationClip clip, string path, string prop, float[] times, float[] offsets)
        {
            float d = DefaultValue(path, prop);
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) curve.AddKey(new Keyframe(times[i], d + offsets[i]));
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            clip.SetCurve(path, typeof(Transform), prop, curve);
        }

        static void Step(AnimationClip clip, string path, string prop, float t, float from, float to)
        {
            var curve = new AnimationCurve(new Keyframe(0, from), new Keyframe(t - 0.001f, from), new Keyframe(t, to));
            for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant); }
            clip.SetCurve(path, typeof(SpriteRenderer), prop, curve);
        }

        // какая крышка видна: явные ключи в каждом клипе, чтобы сброс и переходы всегда давали верное состояние
        static void LidState(AnimationClip clip, bool closed)
        {
            clip.SetCurve("LidClosed", typeof(SpriteRenderer), "m_Color.a", AnimationCurve.Constant(0, 0.1f, closed ? 1f : 0f));
            clip.SetCurve("LidOpen", typeof(SpriteRenderer), "m_Color.a", AnimationCurve.Constant(0, 0.1f, closed ? 0f : 1f));
        }

        static void BuildController(AnimationClip idle, AnimationClip shake, AnimationClip open, AnimationClip opened)
        {
            string path = Dir + "Chest.controller";
            // контроллер не удаляем, а очищаем на месте: GUID сохраняется, ссылки из префаба и сцены не рвутся
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            var oldSm = ctrl.layers[0].stateMachine;
            foreach (var t in oldSm.anyStateTransitions) oldSm.RemoveAnyStateTransition(t);
            foreach (var s in oldSm.states) oldSm.RemoveState(s.state);
            ctrl.AddParameter("Shake", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Open", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Reset", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sShake = sm.AddState("Shake"); sShake.motion = shake;
            var sOpen = sm.AddState("Open"); sOpen.motion = open;
            var sOpened = sm.AddState("Opened"); sOpened.motion = opened;
            sm.defaultState = sIdle;

            var t1 = sIdle.AddTransition(sShake); t1.hasExitTime = false; t1.duration = 0.08f; t1.AddCondition(AnimatorConditionMode.If, 0, "Shake");
            var t2 = sShake.AddTransition(sOpen); t2.hasExitTime = false; t2.duration = 0.06f; t2.AddCondition(AnimatorConditionMode.If, 0, "Open");
            var t2b = sShake.AddTransition(sOpen); t2b.hasExitTime = true; t2b.exitTime = 1f; t2b.duration = 0.06f;   // страховка: после тряски всегда открываемся
            var t3 = sOpen.AddTransition(sOpened); t3.hasExitTime = true; t3.exitTime = 1f; t3.duration = 0.2f;
            var tr = sm.AddAnyStateTransition(sIdle); tr.hasExitTime = false; tr.duration = 0f; tr.canTransitionToSelf = true; tr.AddCondition(AnimatorConditionMode.If, 0, "Reset");
            EditorUtility.SetDirty(ctrl);

            // Animator на корне префаба
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();   // ?? не работает с «фальшивым null» Unity
            anim.runtimeAnimatorController = ctrl;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);   // иначе редактор держит старую копию без контроллера
        }
    }
}

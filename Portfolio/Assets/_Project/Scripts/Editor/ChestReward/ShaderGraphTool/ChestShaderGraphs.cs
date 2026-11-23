using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace Portfolio.ShaderGraphTool
{
    /// Shader Graph для экрана награды.
    /// Tools > Portfolio > Chest Reward > Build Chest Shader Graphs
    internal static class ChestShaderGraphs
    {
        const string ShaderDir = "Assets/_Project/Art/ChestReward/Shaders/";

        [MenuItem("Tools/Portfolio/Chest Reward/Build Chest Shader Graphs")]
        public static void BuildAll()
        {
            Rays();
            Glow();
            AssetDatabase.SaveAssets();
        }

        // Свечение для спрайтов (луч из сундука, ореол, вспышка): альфа спрайта × HDR-цвет × Fade.
        public static void Glow()
        {
            var b = SGBuilder.NewSpriteUnlit();
            b.Column(0);
            var mainTex = b.Tex("Main Tex", "_MainTex", null);
            var tint = b.Color("Tint", "_Tint", new Color(1.4f, 1.1f, 1.8f, 1f), true);
            var fade = b.Float("Fade", "_Fade", 1f, new Vector2(0, 1));
            b.Column(1);
            var vc = b.VertexColor();
            var a = b.Mul(b.Mul(b.Mul(b.Sample(mainTex, b.UV(), "A"), b.Comp(tint, "A")), b.Comp(vc, "A")), fade);
            b.Output(b.Mul(tint, vc), a);
            b.Save(ShaderDir + "SG_ChestGlow.shadergraph");
        }

        // Лучи за сундуком: два слоя лучей вращаются в разные стороны, мягкий спад к краю и пустой центр.
        public static void Rays()
        {
            var b = SGBuilder.NewSpriteUnlit();
            b.Column(0);
            var color = b.Color("Color", "_Color", new Color(1f, 0.85f, 1.25f, 1f), true);
            var count = b.Float("Ray Count", "_RayCount", 10f);
            var speed = b.Float("Speed", "_Speed", 0.035f);
            var width = b.Float("Ray Width", "_RayWidth", 0.35f, new Vector2(0.05f, 0.9f));
            var inner = b.Float("Inner Radius", "_InnerRadius", 0.12f, new Vector2(0f, 0.6f));
            var fade = b.Float("Fade", "_Fade", 1f, new Vector2(0, 1));

            b.Column(1);
            var uv = b.UV();
            var cx = b.Sub(b.Mul(b.Comp(uv, "R"), 2f), 1f);
            var cy = b.Sub(b.Mul(b.Comp(uv, "G"), 2f), 1f);
            var r = b.Un<LengthNode>(b.Vec2(cx, cy));
            var at = b.Node<Arctangent2Node>(); b.Link(cy, at, "A"); b.Link(cx, at, "B");
            var ang = b.Mul(SGBuilder.O(at), 0.159155f);

            // слой 1: широкие лучи
            b.Column(2);
            var a1 = b.Add(b.Mul(ang, count), b.Mul(b.Time(), speed));
            var f1 = b.Un<FractionNode>(a1);
            var tri1 = b.Mul(b.Abs(b.Sub(f1, 0.5f)), 2f);                    // 0 в центре луча, 1 между лучами
            var ray1 = b.Smooth(0f, 1f, b.Sat(b.Div(b.Sub(1f, tri1), width)));

            // слой 2: узкие лучи в обратную сторону, слабее
            b.Column(3);
            var a2 = b.Sub(b.Mul(ang, b.Mul(count, 1.6f)), b.Mul(b.Time(), b.Mul(speed, 1.7f)));
            var tri2 = b.Mul(b.Abs(b.Sub(b.Un<FractionNode>(a2), 0.5f)), 2f);
            var ray2 = b.Mul(b.Smooth(0f, 1f, b.Sat(b.Div(b.Sub(1f, tri2), b.Mul(width, 0.5f)))), 0.45f);

            b.Column(4);
            var rays = b.Sat(b.Add(ray1, ray2));
            var radial = b.Mul(b.OneMinus(b.Smooth(0.25f, 1f, r)), b.Smooth(0f, 1f, b.Div(r, b.Max(inner, b.K(0.001f)))));
            var centerGlow = b.Mul(b.Pow(b.OneMinus(b.Sat(b.Mul(r, 1.4f))), 2f), 0.6f);
            var alpha = b.Mul(b.Mul(b.Sat(b.Add(b.Mul(rays, radial), centerGlow)), b.Comp(color, "A")), fade);
            b.Output(b.Mul(color, b.VertexColor()), b.Mul(alpha, b.Comp(b.VertexColor(), "A")));
            b.Save(ShaderDir + "SG_Rays.shadergraph");
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Portfolio.ChestReward.EditorTools
{
    /// Процедурные текстуры экрана награды: искра-звёздочка, мягкая точка, блик для слота.
    /// Tools > Portfolio > Chest Reward > Generate Chest Textures
    public static class ChestTextureGenerator
    {
        const string Dir = "Assets/_Project/Art/ChestReward/Textures/";

        [MenuItem("Tools/Portfolio/Chest Reward/Generate Chest Textures")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Save("T_Sparkle.png", 128, 128, Sparkle, 128);
            Save("T_SoftDot.png", 64, 64, SoftDot, 64);
            Save("T_ShineStripe.png", 128, 128, ShineStripe, 100);
            Save("T_SlotGlow.png", 256, 256, SlotGlow, 100);
            Save("T_Quad.png", 16, 16, (u, v) => Color.white, 16);
            AssetDatabase.Refresh();
        }

        // четырёхлучевая звёздочка с ярким центром
        static Color Sparkle(float u, float v)
        {
            float x = Mathf.Abs(u * 2 - 1), y = Mathf.Abs(v * 2 - 1);
            float cross = Mathf.Max(Mathf.Clamp01(1 - x / 0.09f * 1f) * Mathf.Clamp01(1 - y), Mathf.Clamp01(1 - y / 0.09f) * Mathf.Clamp01(1 - x));
            float core = Mathf.Clamp01(1 - new Vector2(x, y).magnitude / 0.32f);
            float a = Mathf.Clamp01(Mathf.Pow(cross, 1.6f) + core * core);
            return new Color(1, 1, 1, a);
        }

        static Color SoftDot(float u, float v)
        {
            float d = new Vector2(u * 2 - 1, v * 2 - 1).magnitude;
            float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
            return new Color(1, 1, 1, a);
        }


        // узкая рамка-ореол вокруг слота награды: повторяет скруглённый квадрат подложки (слот занимает ~60% текстуры).
        // Цвет запечён градиентом: у края слота светло-жёлтый, наружу переходит в оранжево-красный и гаснет
        static Color SlotGlow(float u, float v)
        {
            const float half = 0.3f, corner = 0.06f;
            var q = new Vector2(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) - new Vector2(half - corner, half - corner);
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
            float a = d <= 0f ? 1f : Mathf.Clamp01(Mathf.Exp(-d / 0.05f) * 0.95f + Mathf.Exp(-d / 0.11f) * 0.35f);
            float edge = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f);   // ноль на краю текстуры
            float k = Mathf.Clamp01(d / 0.16f);
            var col = Color.Lerp(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.55f, 0.2f), Mathf.SmoothStep(0f, 1f, k));
            col.a = a * Mathf.SmoothStep(0f, 1f, edge / 0.06f);
            return col;
        }

        // диагональная полоса блика (пробегает по слоту награды)
        static Color ShineStripe(float u, float v)
        {
            float d = Mathf.Abs((u - 0.5f) - (v - 0.5f) * 0.6f);
            float a = Mathf.Clamp01(1 - d / 0.16f); a = a * a * (3 - 2 * a);
            return new Color(1, 1, 1, a * 0.85f);
        }

        static void Save(string name, int w, int h, System.Func<float, float, Color> f, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(Dir + name, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Dir + name);
            var ti = (TextureImporter)AssetImporter.GetAtPath(Dir + name);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 512;   // бюджет проекта: не больше 1024
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
        }
    }
}

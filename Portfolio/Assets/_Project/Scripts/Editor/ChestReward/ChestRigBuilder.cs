using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Portfolio.ChestReward.EditorTools
{
    /// Риг сундука: кости, сетка и веса для каждой части + сборка префаба с SpriteSkin.
    /// Данные пишутся штатным API 2D Animation, поэтому их можно открыть и поправить в Skinning Editor.
    /// Tools > Portfolio > Chest Reward > Build Chest Rig
    public static class ChestRigBuilder
    {
        const string PartsDir = "Assets/_Project/Art/ChestReward/Source/Chest/Parts/";
        const string PrefabPath = "Assets/_Project/Art/ChestReward/Prefabs/Chest.prefab";
        const float PPU = 100f;

        // Кость в пространстве спрайта (пиксели от левого нижнего угла). Направление кости = её ось X.
        struct BoneDef { public string name; public int parent; public Vector2 pos; public float angle; public float length; }

        // ---------- нарезка ----------
        // Открытый референс совпадает с закрытым сундуком по основанию со сдвигом dy = 107 px (найден попиксельно, ошибка 0).
        // Режем по настоящим границам рисунка, а не по середине полосы:
        //  • закрытая крышка — строки 0..151 закрытого сундука (заканчивается над полосой-шовом);
        //  • основание — из открытого референса вместе с верхним бортиком и объёмной рамкой кристалла (см. BaseTopRow);
        //    справа от рамки кромка на строке 242, чтобы ничего не перекрывало свечение;
        //    в закрытом состоянии бортик спрятан под крышкой, а когда крышка приподнимается, в щели виден бортик и свет, а не срез;
        //  • открытая крышка — строки 0..250 открытого референса, низ уходит за основание;
        //  • кристалл — отдельный спрайт поверх, чтобы не гнулся вместе с крышкой.
        const string ChestDir = "Assets/_Project/Art/ChestReward/Source/Chest/";
        const int OpenDy = 107;

        // Положение левого нижнего угла каждой части в пространстве сундука (пиксели, 0 = низ-центр основания)
        static readonly Dictionary<string, Vector2> PartOrigin = new Dictionary<string, Vector2>
        {
            { "Chest_Base", new Vector2(-209.5f, 0f) },
            { "Chest_LidClosed", new Vector2(-209.5f, 327f - 151f) },
            { "Chest_LidOpen", new Vector2(-209.5f, 327f + OpenDy - 250f) },
        };

        static readonly Dictionary<string, BoneDef[]> Bones = new Dictionary<string, BoneDef[]>
        {
            // основание: низ и верх, чтобы сундук сжимался и пружинил
            { "Chest_Base", new[] {
                new BoneDef { name = "Base", parent = -1, pos = new Vector2(209.5f, 0f), angle = 90f, length = 100f },
                new BoneDef { name = "BaseTop", parent = 0, pos = new Vector2(100f, 0f), angle = 0f, length = 100f },
            } },
            // закрытая крышка: петля у шва и верх
            { "Chest_LidClosed", new[] {
                new BoneDef { name = "LidClosed", parent = -1, pos = new Vector2(209.5f, 4f), angle = 90f, length = 70f },
                new BoneDef { name = "LidClosedTop", parent = 0, pos = new Vector2(70f, 0f), angle = 0f, length = 70f },
            } },
            // открытая крышка: петля, середина, верх — откидывается и пружинит
            { "Chest_LidOpen", new[] {
                new BoneDef { name = "LidOpen", parent = -1, pos = new Vector2(209.5f, 8f), angle = 90f, length = 85f },
                new BoneDef { name = "LidOpenMid", parent = 0, pos = new Vector2(85f, 0f), angle = -8f, length = 85f },
                new BoneDef { name = "LidOpenTop", parent = 1, pos = new Vector2(85f, 0f), angle = -6f, length = 60f },
            } },
        };

        [MenuItem("Tools/Portfolio/Chest Reward/Build Chest Rig")]
        public static void Build()
        {
            CutParts();
            foreach (var part in Bones.Keys) RigSprite(part, Bones[part]);
            BuildPrefab();
        }

        static void CutParts()
        {
            System.IO.Directory.CreateDirectory(PartsDir);
            var closed = Readable(ChestDir + "Chest.png");
            var open = Readable(ChestDir + "Chest_Open_Reference.png");
            Cut(closed, 0, 0, closed.width - 1, 151, null, "Chest_LidClosed.png");
            // бортик над полосой (строки < 258) оставляем только под силуэтом закрытой крышки:
            // справа контур открытого рисунка на 1–4 px шире, и иначе он торчит из-под закрытой крышки
            var cpx = closed.GetPixels32();
            float ClosedAlpha(int x, int yClosed) => x < 0 || x >= closed.width || yClosed < 0 || yClosed > 151 ? 0f : cpx[(closed.height - 1 - yClosed) * closed.width + x].a / 255f;
            // правый скат рамки наращиваем наружу на 4 px её собственным жёлтым — грань ровная и плотная
            var opx = open.GetPixels32();
            var frameYellow = opx[(open.height - 1 - 225) * open.width + 184];
            frameYellow.a = 255;
            bool RightBand(int x, int y) { if (y < 192 || y > 246) return false; float s = 162f + (y - 192) * 0.9f; return x > s - 1f && x <= s + 4f; }
            Cut(open, 0, 188, 418, open.height - 1, (x, y) =>
            {
                if (y >= 258) return 1f;
                if (y < BaseTopRow(x) && !RightBand(x, y)) return 0f;
                return ClosedAlpha(x, y - OpenDy);
            }, "Chest_Base.png", (x, y, c) => RightBand(x, y) && (y < BaseTopRow(x) || x > 162f + (y - 192) * 0.9f - 1f) ? frameYellow : c);
            Cut(open, 0, 0, open.width - 1, 250, null, "Chest_LidOpen.png");
            MouthGlow(open);
            Cut(open, 104, 206, 210, 314, (x, y) => (1f - (Mathf.Abs(x - 157f) / 50f + Mathf.Abs(y - 260f) / 52f)) * 40f, "Chest_Gem.png");
            AssetDatabase.Refresh();
            foreach (var n in new[] { "Chest_Base", "Chest_LidClosed", "Chest_LidOpen", "Chest_Gem" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(PartsDir + n + ".png");
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = false; ti.maxTextureSize = 512; ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = PPU;
                var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
                st.spriteAlignment = (int)SpriteAlignment.BottomCenter; st.spriteMeshType = SpriteMeshType.Tight;
                ti.SetTextureSettings(st); ti.SaveAndReimport();
            }
        }

        // ---------- свечение из щели ----------
        // Левый нижний угол текстур свечения в пространстве сундука и их размер (пиксели).
        // По X — от левого края основания до правого края сундука (x 100..409 исходника).
        static readonly Vector2 GlowOrigin = new Vector2(100f - 209.5f, 150f);
        const int GlowW = 310, GlowH = 250;

        // Свечение строится по реальному контуру верхней кромки основания и кристалла (как он виден на открытом сундуке):
        // ярко у кромки и быстро гаснет вверх. Часть ниже кромки спрятана за основанием.
        // Контур кромки на открытом сундуке (x, строка исходника сверху вниз), замерен по пикселям:
        // верхняя кромка основания слева → внешний край ромбовидной рамки кристалла (вершина на строке 192) →
        // плоский верх рамки → правый скат с фаской до строки 242 → по кромке основания до правого края сундука.
        static readonly Vector2[] MouthContour =
        {
            new Vector2(100f, 230f), new Vector2(121f, 230f), new Vector2(159f, 192f),
            new Vector2(166f, 192f), new Vector2(211f, 242f), new Vector2(403f, 242f), new Vector2(420f, 242f),
        };

        static float ContourRow(float x)
        {
            for (int k = 0; k < MouthContour.Length - 1; k++)
                if (x <= MouthContour[k + 1].x)
                    return Mathf.Lerp(MouthContour[k].y, MouthContour[k + 1].y, Mathf.InverseLerp(MouthContour[k].x, MouthContour[k + 1].x, x));
            return MouthContour[MouthContour.Length - 1].y;
        }

        static void MouthGlow(Texture2D open)
        {
            var opx = open.GetPixels32();
            // маска «краски»: прозрачный фон и чёрный контур не светятся — свет не выходит за границу сундука
            float PaintMask(int x, int r)
            {
                if (x < 0 || x >= open.width || r < 0 || r >= open.height) return 0f;
                var c = opx[(open.height - 1 - r) * open.width + x];
                float lum = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                return c.a / 255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.3f, lum));
            }
            var edge = new float[GlowW];   // высота кромки в пространстве сундука для каждой колонки
            for (int i = 0; i < GlowW; i++) edge[i] = (327f + OpenDy) - ContourRow(100 + i + 0.5f);
            // лёгкое сглаживание: вершина рамки остаётся острой, но без пиксельных ступенек
            var smooth = new float[GlowW];
            for (int i = 0; i < GlowW; i++) { float s = 0; int c = 0; for (int k = -2; k <= 2; k++) { int j = Mathf.Clamp(i + k, 0, GlowW - 1); s += edge[j]; c++; } smooth[i] = s / c; }

            var tight = new Color[GlowW * GlowH]; var soft = new Color[GlowW * GlowH];
            for (int i = 0; i < GlowW; i++)
            {
                int x = 100 + i;
                float ends = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(100, 112, x)) * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(396, 410, x)));
                for (int j = 0; j < GlowH; j++)
                {
                    float y = GlowOrigin.y + j + 0.5f;
                    float h = y - smooth[i];
                    float below = Mathf.Clamp01(1f + h / 4f);    // заходим под кромку на ~4 px, чтобы свет плотно прилегал к рамке
                    float mask = PaintMask(x, Mathf.RoundToInt((327f + OpenDy) - y));   // свет только поверх цветных пикселей сундука
                    float a1 = (h < 0 ? below : Mathf.Exp(-h / 9f)) * ends * mask;
                    float a2 = (h < 0 ? below : Mathf.Exp(-h / 55f)) * 0.85f * ends * mask;
                    tight[j * GlowW + i] = new Color(1, 1, 1, a1);
                    soft[j * GlowW + i] = new Color(1, 1, 1, a2);
                }
            }
            SaveGlow(tight, "Chest_MouthGlow.png");
            SaveGlow(soft, "Chest_MouthLight.png");
        }

        static void SaveGlow(Color[] c, string name)
        {
            var t = new Texture2D(GlowW, GlowH, TextureFormat.RGBA32, false);
            t.SetPixels(c); t.Apply();
            WriteIfChanged(PartsDir + name, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(PartsDir + name);
            var ti = (TextureImporter)AssetImporter.GetAtPath(PartsDir + name);
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU; ti.mipmapEnabled = false; ti.maxTextureSize = 512; ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None; ti.wrapMode = TextureWrapMode.Clamp;
            var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.BottomLeft; st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st); ti.SaveAndReimport();
        }


        // Верх основания. Объёмная рамка вокруг кристалла целиком принадлежит основанию (пиксели берутся из открытого сундука):
        // левый скат под 45° от кромки (121, 230) к плоскому верху (159..162, 192), правый скат с фаской к подножию (207, 242), наращён на 4 px.
        // Левее рамки кромка на строке 230, правее — на строке 242.
        static int BaseTopRow(int x)
        {
            if (x < 159) return Mathf.Min(230, 192 + (159 - x));   // левый скат под 45°, без светлой фаски
            if (x <= 162) return 192;   // плоский верх
            if (x < 207) return Mathf.RoundToInt(192f + (x - 162) * (50f / 45f));
            return 242;
        }

        // Пишем PNG, только если он изменился: неизменённые текстуры, открытые в превью редактора, не блокируют пересборку
        static void WriteIfChanged(string path, byte[] bytes)
        {
            if (System.IO.File.Exists(path) && System.Linq.Enumerable.SequenceEqual(System.IO.File.ReadAllBytes(path), bytes)) return;
            System.IO.File.WriteAllBytes(path, bytes);
        }

        static Texture2D Readable(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!ti.isReadable || ti.npotScale != TextureImporterNPOTScale.None || ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.isReadable = true; ti.npotScale = TextureImporterNPOTScale.None; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // строки y0..y1 считаются сверху вниз, как в редакторе картинок
        static void Cut(Texture2D src, int x0, int y0, int x1, int y1, System.Func<int, int, float> mask, string name, System.Func<int, int, Color32, Color32> recolor = null)
        {
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var sp = src.GetPixels32(); var o = new Color32[w * h];
            for (int yi = y0; yi <= y1; yi++)
                for (int x = x0; x <= x1; x++)
                {
                    var c = sp[(src.height - 1 - yi) * src.width + x];
                    if (recolor != null) c = recolor(x, yi, c);
                    if (mask != null) c.a = (byte)(c.a * Mathf.Clamp01(mask(x, yi)));
                    o[(y1 - yi) * w + (x - x0)] = c;
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(o); t.Apply();
            WriteIfChanged(PartsDir + name, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        // ---------- кости, сетка, веса ----------

        static void RigSprite(string part, BoneDef[] defs)
        {
            string path = PartsDir + part + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var rect = dp.GetSpriteRects()[0];
            var guid = rect.spriteID;

            // кости
            var bones = new List<SpriteBone>();
            for (int i = 0; i < defs.Length; i++)
            {
                bones.Add(new SpriteBone
                {
                    name = defs[i].name,
                    guid = GUID.Generate().ToString(),
                    parentId = defs[i].parent,
                    position = defs[i].pos,
                    rotation = Quaternion.Euler(0, 0, defs[i].angle),
                    length = defs[i].length,
                    color = Color.HSVToRGB(i / (float)defs.Length, 0.7f, 1f)
                });
            }
            dp.GetDataProvider<ISpriteBoneDataProvider>().SetBones(guid, bones);

            // мировые (в пространстве спрайта) начала и концы костей — для весов
            var start = new Vector2[defs.Length]; var end = new Vector2[defs.Length]; var rot = new float[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].parent < 0) { start[i] = defs[i].pos; rot[i] = defs[i].angle; }
                else
                {
                    int p = defs[i].parent;
                    start[i] = start[p] + (Vector2)(Quaternion.Euler(0, 0, rot[p]) * defs[i].pos);
                    rot[i] = rot[p] + defs[i].angle;
                }
                end[i] = start[i] + (Vector2)(Quaternion.Euler(0, 0, rot[i]) * Vector2.right) * defs[i].length;
            }

            // сетка: регулярная решётка по непрозрачным клеткам
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int cols = Mathf.Max(4, Mathf.RoundToInt(w / 34f)), rows = Mathf.Max(4, Mathf.RoundToInt(h / 26f));
            float cw = w / (float)cols, ch = h / (float)rows;
            var used = new bool[cols, rows];
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    int x0 = (int)(cx * cw), x1 = Mathf.Min(w - 1, (int)((cx + 1) * cw));
                    int y0 = (int)(cy * ch), y1 = Mathf.Min(h - 1, (int)((cy + 1) * ch));
                    bool any = false;
                    for (int y = y0; y <= y1 && !any; y += 2)
                        for (int x = x0; x <= x1; x += 2)
                            if (px[y * w + x].a > 8) { any = true; break; }
                    used[cx, cy] = any;
                }

            var vIndex = new Dictionary<Vector2Int, int>();
            var verts = new List<Vertex2DMetaData>();
            var indices = new List<int>();
            int V(int gx, int gy)
            {
                var key = new Vector2Int(gx, gy);
                if (vIndex.TryGetValue(key, out int id)) return id;
                var pos = new Vector2(Mathf.Min(gx * cw, w), Mathf.Min(gy * ch, h));
                verts.Add(new Vertex2DMetaData { position = pos, boneWeight = Weights(pos, start, end) });
                vIndex[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    if (!used[cx, cy]) continue;
                    int a = V(cx, cy), b = V(cx + 1, cy), c = V(cx + 1, cy + 1), d = V(cx, cy + 1);
                    indices.AddRange(new[] { a, c, b, a, d, c });
                }

            // внешний контур (рёбра) — нужен Skinning Editor для отображения сетки
            var edges = new List<Vector2Int>();
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    if (!used[cx, cy]) continue;
                    if (cy == 0 || !used[cx, cy - 1]) edges.Add(new Vector2Int(V(cx, cy), V(cx + 1, cy)));
                    if (cy == rows - 1 || !used[cx, cy + 1]) edges.Add(new Vector2Int(V(cx + 1, cy + 1), V(cx, cy + 1)));
                    if (cx == 0 || !used[cx - 1, cy]) edges.Add(new Vector2Int(V(cx, cy + 1), V(cx, cy)));
                    if (cx == cols - 1 || !used[cx + 1, cy]) edges.Add(new Vector2Int(V(cx + 1, cy), V(cx + 1, cy + 1)));
                }

            var mesh = dp.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(guid, verts.ToArray());
            mesh.SetIndices(guid, indices.ToArray());
            mesh.SetEdges(guid, edges.ToArray());
            dp.Apply();

            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        // Веса: расстояние до кости считается ВДОЛЬ цепочки (по высоте), поперёк почти не влияет.
        // Кости стоят по центру, и при обычном расстоянии углы сундука получали ~50/50 и отставали от крышки —
        // на краях был виден шов. Так вся горизонтальная полоса на одной высоте движется одинаково.
        static BoneWeight Weights(Vector2 p, Vector2[] s, Vector2[] e)
        {
            int n = s.Length;
            var w = new float[n];
            for (int i = 0; i < n; i++)
            {
                var ab = e[i] - s[i];
                var dir = ab / Mathf.Max(ab.magnitude, 1e-4f);
                var perp = new Vector2(-dir.y, dir.x);
                float t = Mathf.Clamp01(Vector2.Dot(p - s[i], ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                var off = p - (s[i] + ab * t);
                float along = Vector2.Dot(off, dir), across = Vector2.Dot(off, perp) * 0.04f;
                float d = Mathf.Sqrt(along * along + across * across);
                w[i] = 1f / Mathf.Pow(d + 10f, 4f);
            }
            var order = Enumerable.Range(0, n).OrderByDescending(i => w[i]).ToArray();
            int i0 = order[0], i1 = n > 1 ? order[1] : order[0];
            float w0 = w[i0], w1 = n > 1 ? w[i1] : 0f, sum = w0 + w1;
            return new BoneWeight { boneIndex0 = i0, weight0 = w0 / sum, boneIndex1 = i1, weight1 = w1 / sum };
        }

        // ---------- префаб ----------

        static void BuildPrefab()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("Chest");
            var bonesRoot = new GameObject("Bones").transform; bonesRoot.SetParent(root.transform, false);

            // кости в пространстве сундука
            var t = new Dictionary<string, Transform>();
            Transform MakeBones(string part, Transform parentForRoot)
            {
                var defs = Bones[part];
                var origin = PartOrigin[part];
                var created = new Transform[defs.Length];
                for (int i = 0; i < defs.Length; i++)
                {
                    var go = new GameObject(defs[i].name).transform;
                    if (defs[i].parent < 0)
                    {
                        go.SetParent(parentForRoot, true);
                        go.position = root.transform.TransformPoint((origin + defs[i].pos) / PPU);
                        go.rotation = Quaternion.Euler(0, 0, defs[i].angle);
                    }
                    else
                    {
                        go.SetParent(created[defs[i].parent], false);
                        go.localPosition = defs[i].pos / PPU;
                        go.localRotation = Quaternion.Euler(0, 0, defs[i].angle);
                    }
                    created[i] = go; t[defs[i].name] = go;
                }
                return created[0];
            }
            MakeBones("Chest_Base", bonesRoot);
            MakeBones("Chest_LidClosed", t["BaseTop"]);
            MakeBones("Chest_LidOpen", t["BaseTop"]);

            // кристалл — жёстко на верхней кости основания
            var gemAnchor = new GameObject("GemAnchor").transform;
            gemAnchor.SetParent(t["BaseTop"], true);
            gemAnchor.position = root.transform.TransformPoint(new Vector2(-52.5f, 120f) / PPU);
            gemAnchor.rotation = Quaternion.identity;

            // части со SpriteSkin (порядок: открытая крышка позади, кристалл впереди)
            Skin("Chest_LidOpen", root.transform, t, 10);
            Skin("Chest_Base", root.transform, t, 12);
            Skin("Chest_LidClosed", root.transform, t, 13);

            var gem = new GameObject("Gem").AddComponent<SpriteRenderer>();
            gem.transform.SetParent(gemAnchor, false);
            gem.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartsDir + "Chest_Gem.png");
            gem.sortingOrder = 14;

            // свечение из щели: спрайты по контуру верхней кромки основания и кристалла, на верхней кости тела.
            // Порядок 11 — позади основания (12) и поверх открытой крышки (10): видно только то, что выше кромки.
            foreach (var name in new[] { "MouthGlow", "MouthLight" })
            {
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(t["BaseTop"], true);
                sr.transform.position = root.transform.TransformPoint(GlowOrigin / PPU);
                sr.transform.rotation = Quaternion.identity;
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartsDir + "Chest_" + name + ".png");
                sr.sortingOrder = 11;
                sr.enabled = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        static void Skin(string part, Transform root, Dictionary<string, Transform> t, int order)
        {
            var go = new GameObject(part.Replace("Chest_", ""));
            go.transform.SetParent(root, false);
            go.transform.localPosition = (PartOrigin[part] + new Vector2(GetSprite(part).rect.width * 0.5f, 0f)) / PPU; // pivot = низ-центр
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(part);
            sr.sortingOrder = order;
            var skin = go.AddComponent<SpriteSkin>();
            var defs = Bones[part];
            var so = new SerializedObject(skin);
            so.FindProperty("m_RootBone").objectReferenceValue = t[defs[0].name];
            var arr = so.FindProperty("m_BoneTransforms");
            arr.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = t[defs[i].name];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite GetSprite(string part) => AssetDatabase.LoadAssetAtPath<Sprite>(PartsDir + part + ".png");
    }
}

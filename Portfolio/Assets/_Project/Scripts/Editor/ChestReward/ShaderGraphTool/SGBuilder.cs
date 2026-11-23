using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace Portfolio.ShaderGraphTool
{
    /// Порт узла: узел + имя слота.
    internal struct Port
    {
        public AbstractMaterialNode node; public string slot;
        public Port(AbstractMaterialNode n, string s) { node = n; slot = s; }
    }

    /// Небольшая обёртка над внутренним API Shader Graph: собирает граф узлами и сохраняет обычный .shadergraph.
    /// Нужна только для генерации графов скриптом; сами графы потом редактируются в Shader Graph как обычно.
    internal class SGBuilder
    {
        public readonly GraphData graph;
        int col, row;

        SGBuilder(GraphData g) { graph = g; }

        public static SGBuilder NewSpriteUnlit()
        {
            var ut = Type.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget, Unity.RenderPipelines.Universal.Editor");
            var st = Type.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalSpriteUnlitSubTarget, Unity.RenderPipelines.Universal.Editor");
            var target = (Target)Activator.CreateInstance(ut, true);
            ut.GetMethod("TrySetActiveSubTarget").Invoke(target, new object[] { st });

            var g = new GraphData();
            g.AddContexts();
            g.InitializeOutputs(new[] { target }, new[]
            {
                BlockFields.VertexDescription.Position, BlockFields.VertexDescription.Normal, BlockFields.VertexDescription.Tangent,
                BlockFields.SurfaceDescription.BaseColor, BlockFields.SurfaceDescription.Alpha
            });
            g.path = "Portfolio/ChestReward";
            return new SGBuilder(g);
        }

        public void Save(string path)
        {
            graph.ValidateGraph();
            FileUtilities.WriteShaderGraphToDisk(path, graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // ---------- узлы ----------
        public void Column(int c) { col = c; row = 0; }

        public T Node<T>() where T : AbstractMaterialNode, new()
        {
            var n = new T();
            n.drawState = new DrawState { position = new Rect(-1400 + col * 260, -400 + row * 140, 0, 0), expanded = true };
            row++;
            graph.AddNode(n);
            return n;
        }

        static MaterialSlot FindSlot(AbstractMaterialNode n, string name, bool input)
        {
            var slots = input ? n.GetInputSlots<MaterialSlot>().ToList() : n.GetOutputSlots<MaterialSlot>().ToList();
            var s = slots.FirstOrDefault(x => x.RawDisplayName() == name) ?? slots.FirstOrDefault(x => x.shaderOutputName == name);
            if (s == null && name == null && slots.Count > 0) s = slots[0];
            if (s == null) throw new Exception($"{n.GetType().Name}: нет слота '{name}'. Есть: {string.Join(", ", slots.Select(x => x.RawDisplayName()))}");
            return s;
        }

        public void Link(Port from, AbstractMaterialNode to, string inSlot)
        {
            var o = FindSlot(from.node, from.slot, false);
            var i = FindSlot(to, inSlot, true);
            graph.Connect(from.node.GetSlotReference(o.id), to.GetSlotReference(i.id));
        }

        public void Set(AbstractMaterialNode n, string inSlot, float v)
        {
            var s = FindSlot(n, inSlot, true);
            switch (s)
            {
                case Vector1MaterialSlot a: a.value = v; break;
                case DynamicVectorMaterialSlot d: d.value = new Vector4(v, v, v, v); break;
                case Vector2MaterialSlot b: b.value = new Vector2(v, v); break;
                default: throw new Exception("Set float: неподдержанный слот " + s.GetType().Name);
            }
        }

        public void Set(AbstractMaterialNode n, string inSlot, Vector4 v)
        {
            var s = FindSlot(n, inSlot, true);
            switch (s)
            {
                case DynamicVectorMaterialSlot d: d.value = v; break;
                case Vector2MaterialSlot b: b.value = v; break;
                case Vector3MaterialSlot c: c.value = v; break;
                case Vector4MaterialSlot e: e.value = v; break;
                default: throw new Exception("Set vec: неподдержанный слот " + s.GetType().Name);
            }
        }

        public static Port O(AbstractMaterialNode n, string slot = "Out") => new Port(n, slot);

        // ---------- свойства материала ----------
        public Port Float(string display, string reference, float value, Vector2? range = null)
        {
            var p = new Vector1ShaderProperty { displayName = display, value = value };
            if (range.HasValue) { p.floatType = FloatType.Slider; p.rangeValues = range.Value; }
            return AddProp(p, reference);
        }

        public Port Color(string display, string reference, Color value, bool hdr)
        {
            var p = new ColorShaderProperty { displayName = display, value = value, colorMode = hdr ? ColorMode.HDR : ColorMode.Default };
            return AddProp(p, reference);
        }

        public Port Tex(string display, string reference, Texture2D tex, bool exposed = true)
        {
            var p = new Texture2DShaderProperty { displayName = display };
            p.value.texture = tex;
            p.generatePropertyBlock = exposed;
            return AddProp(p, reference);
        }

        Port AddProp(AbstractShaderProperty p, string reference)
        {
            graph.AddGraphInput(p);
            p.SetReferenceNameAndSanitizeForGraph(graph, reference);
            var pn = Node<PropertyNode>();
            pn.property = p;
            return new Port(pn, null);
        }

        // ---------- выходы ----------
        public void Output(Port baseColor, Port alpha)
        {
            foreach (var b in graph.GetNodes<BlockNode>())
            {
                if (b.descriptor == BlockFields.SurfaceDescription.BaseColor) Link(baseColor, b, b.GetInputSlots<MaterialSlot>().First().RawDisplayName());
                if (b.descriptor == BlockFields.SurfaceDescription.Alpha) Link(alpha, b, b.GetInputSlots<MaterialSlot>().First().RawDisplayName());
            }
        }

        // ---------- короткие математические помощники ----------
        public Port Bin<T>(Port a, Port b) where T : AbstractMaterialNode, new() { var n = Node<T>(); Link(a, n, "A"); Link(b, n, "B"); return O(n); }
        public Port Bin<T>(Port a, float b) where T : AbstractMaterialNode, new() => Bin<T>(a, K(b));
        public Port Bin<T>(float a, Port b) where T : AbstractMaterialNode, new() => Bin<T>(K(a), b);
        public Port K(float v) { var n = Node<Vector1Node>(); Set(n, "X", v); return O(n); }
        public Port Un<T>(Port a) where T : AbstractMaterialNode, new() { var n = Node<T>(); Link(a, n, "In"); return O(n); }

        public Port Mul(Port a, Port b) => Bin<MultiplyNode>(a, b);
        public Port Mul(Port a, float b) => Bin<MultiplyNode>(a, b);
        public Port Add(Port a, Port b) => Bin<AddNode>(a, b);
        public Port Add(Port a, float b) => Bin<AddNode>(a, b);
        public Port Sub(Port a, Port b) => Bin<SubtractNode>(a, b);
        public Port Sub(float a, Port b) => Bin<SubtractNode>(a, b);
        public Port Sub(Port a, float b) => Bin<SubtractNode>(a, b);
        public Port Div(Port a, Port b) => Bin<DivideNode>(a, b);
        public Port Max(Port a, Port b) => Bin<MaximumNode>(a, b);
        public Port Min(Port a, Port b) => Bin<MinimumNode>(a, b);
        public Port Pow(Port a, float b) => Bin<PowerNode>(a, b);
        public Port Sat(Port a) => Un<SaturateNode>(a);
        public Port OneMinus(Port a) => Un<OneMinusNode>(a);
        public Port Abs(Port a) => Un<AbsoluteNode>(a);

        public Port Lerp(Port a, Port b, Port t) { var n = Node<LerpNode>(); Link(a, n, "A"); Link(b, n, "B"); Link(t, n, "T"); return O(n); }

        public Port Smooth(float e1, float e2, Port x) { var n = Node<SmoothstepNode>(); Set(n, "Edge1", e1); Set(n, "Edge2", e2); Link(x, n, "In"); return O(n); }

        public Port Comp(Port v, string c) { var n = Node<SplitNode>(); Link(v, n, "In"); return O(n, c); }

        public Port Vec2(Port x, Port y) { var n = Node<Vector2Node>(); Link(x, n, "X"); Link(y, n, "Y"); return O(n); }

        public Port Sample(Port tex, Port uv, string channel = "RGBA") { var n = Node<SampleTexture2DNode>(); Link(tex, n, "Texture"); Link(uv, n, "UV"); return O(n, channel); }

        public Port UV() => O(Node<UVNode>());
        public Port Time() => O(Node<TimeNode>(), "Time");
        public Port ScreenUV() => O(Node<ScreenPositionNode>());
        public Port VertexColor() => O(Node<VertexColorNode>());
    }
}

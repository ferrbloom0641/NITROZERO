using UnityEngine;
using UnityEngine.UIElements;
namespace RacingUI
{
    // Rectifies the original painted panel without replacing its border decoration.
    [UxmlElement]
    public partial class HudRectifiedPanel : VisualElement
    {
        Texture2D texture;
        bool map;
        [UxmlAttribute] public Texture2D panelTexture { get => texture; set { texture = value; MarkDirtyRepaint(); } }
        [UxmlAttribute] public bool mapPanel { get => map; set { map = value; MarkDirtyRepaint(); } }
        public HudRectifiedPanel() { generateVisualContent += Draw; }
        void Draw(MeshGenerationContext context)
        {
            if (texture == null || contentRect.width <= 0 || contentRect.height <= 0) return;
            // Source quadrilateral includes the bevels and all layered red trim.
            Vector2[] source = map
                ? new[] { new Vector2(1438,110), new Vector2(1785,89), new Vector2(1795,308), new Vector2(1448,329) }
                : new[] { new Vector2(128,98), new Vector2(477,116), new Vector2(469,310), new Vector2(120,292) };
            var mesh = context.Allocate(4, 6, texture);
            // Unity 6 remaps normalized UVs to the texture's atlas region automatically.
            Rect uv = new Rect(0, 0, 1, 1);
            var r = contentRect;
            Vector2[] points = { new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax) };
            for (int i=0;i<4;i++)
                mesh.SetNextVertex(new Vertex { position = new Vector3(points[i].x,points[i].y,Vertex.nearZ), tint = Color.white,
                    uv = new Vector2(uv.xMin + source[i].x / 1920f * uv.width, uv.yMin + (1-source[i].y/1080f)*uv.height) });
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);
            mesh.SetNextIndex(2);mesh.SetNextIndex(3);mesh.SetNextIndex(0);
            if (map)
            {
                // The fill shares the artwork transform and follows its inner bevel.
                var painter = context.painter2D;
                Vector2[] edge = {
                    new Vector2(.13f,.13f), new Vector2(.88f,.13f),
                    new Vector2(.94f,.23f), new Vector2(.94f,.82f),
                    new Vector2(.88f,.92f), new Vector2(.12f,.92f),
                    new Vector2(.06f,.82f), new Vector2(.06f,.23f)
                };
                painter.fillColor = Color.black;
                painter.BeginPath();
                for(int i=0;i<edge.Length;i++)
                {
                    Vector2 point = new Vector2(r.xMin+edge[i].x*r.width,r.yMin+edge[i].y*r.height);
                    if(i==0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.ClosePath(); painter.Fill();
            }
        }
    }
}

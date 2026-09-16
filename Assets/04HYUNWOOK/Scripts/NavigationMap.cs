using UnityEngine;
using UnityEngine.UIElements;

namespace RacingUI
{
    /// <summary>Editable perspective view of the current straight road, with live vehicle motion.</summary>
    [UxmlElement]
    public partial class NavigationMap : VisualElement
    {
        float roadWidthValue = .55f, horizonValue = .22f, hazeValue = 1f;
        float lateral, heading, raceProgress, progressOffset = .075f;
        [UxmlAttribute] public bool amberRoute { get; set; }
        [UxmlAttribute] public bool referenceMarker { get; set; }
        Color mapTintValue = Color.clear;
        [UxmlAttribute]
        public Color mapTint { get => mapTintValue; set { mapTintValue = value; MarkDirtyRepaint(); } }

        [UxmlAttribute]
        public float roadWidth { get => roadWidthValue; set { roadWidthValue = Mathf.Clamp(value, .3f, 1.2f); MarkDirtyRepaint(); } }
        [UxmlAttribute]
        public float horizon { get => horizonValue; set { horizonValue = Mathf.Clamp(value, .1f, .45f); MarkDirtyRepaint(); } }
        [UxmlAttribute]
        public float haze { get => hazeValue; set { hazeValue = Mathf.Clamp01(value); MarkDirtyRepaint(); } }

        public NavigationMap()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }
        public void SetVehicle(float lane, float angle, float distance)
        {
            lateral = Mathf.Clamp(lane, -.65f, .65f);
            heading = Mathf.Clamp(angle, -45f, 45f);
            MarkDirtyRepaint();
        }
        public void SetProgress(float progress, float forwardOffset)
        {
            raceProgress = Mathf.Clamp01(progress);
            progressOffset = Mathf.Clamp(forwardOffset, .05f, .10f);
            MarkDirtyRepaint();
        }
        Vector2 Project(float side, float depth)
        {
            Rect r = contentRect;
            // Both boundaries and the road center meet at the same vanishing point.
            float near = Mathf.Clamp01(depth);
            float center = Mathf.Lerp(.5f - heading * .002f, .5f - lateral * .6f, near);
            float width = roadWidthValue * near;
            return new Vector2((center + side * width) * r.width,
                Mathf.Lerp(horizonValue, 1.1f, depth) * r.height);
        }
        static void Quad(Painter2D p, Color color, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            p.fillColor = color; p.BeginPath(); p.MoveTo(a); p.LineTo(b);
            p.LineTo(c); p.LineTo(d); p.ClosePath(); p.Fill();
        }
        void DrawRoad(MeshGenerationContext context)
        {
            // Shared vertices avoid the antialiasing seams between separately painted strips.
            const int steps = 80, columns = 4;
            var mesh = context.Allocate((steps + 1) * columns, steps * 18);
            for (int i = 0; i <= steps; i++)
            {
                float depth = i / (float)steps;
                float visibility = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0f, Mathf.Lerp(.18f, .46f, hazeValue), depth));
                float shade = Mathf.Lerp(.44f, .56f, depth);
                for (int column = 0; column < columns; column++)
                {
                    float side = column == 0 ? -.516f : column == 1 ? -.5f : column == 2 ? .5f : .516f;
                    Vector2 point = Project(side, depth);
                    mesh.SetNextVertex(new Vertex {
                        position = new Vector3(point.x, point.y, Vertex.nearZ),
                        tint = new Color(shade, shade * 1.01f, shade * .98f,
                            column == 0 || column == 3 ? 0f : visibility),
                        uv = Vector2.zero
                    });
                }
            }
            for (int row = 0; row < steps; row++)
                for (int column = 0; column < columns - 1; column++)
                {
                    ushort a = (ushort)(row * columns + column), b = (ushort)(a + 1);
                    ushort d = (ushort)(a + columns), c = (ushort)(d + 1);
                    mesh.SetNextIndex(a); mesh.SetNextIndex(b); mesh.SetNextIndex(c);
                    mesh.SetNextIndex(c); mesh.SetNextIndex(d); mesh.SetNextIndex(a);
                }
        }
        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            if (amberRoute) {
                float cx=r.width*(.5f-lateral*.4f);
                for(int j=0;j<3;j++){
                    p.lineWidth=j==0?8:j==1?4:2;
                    p.strokeColor=new Color(1,.66f,.18f,j==0?.06f:j==1?.22f:1);
                    p.BeginPath();p.MoveTo(new Vector2(cx,r.height*.82f));
                    p.LineTo(new Vector2(cx,r.height*.08f));p.Stroke();
                }
                float markerTravel = raceProgress * progressOffset * r.height;
                var markerBase = new Vector2(r.width*.5f,r.height*.8f);
                var markerAt = markerBase - new Vector2(0f, markerTravel);
                if (markerTravel > .5f) {
                    p.lineWidth = 3f;
                    p.strokeColor = new Color(1f,.55f,.13f,.75f);
                    p.BeginPath(); p.MoveTo(markerBase); p.LineTo(markerAt); p.Stroke();
                }
                float sz=referenceMarker?15:11;
                var rot=Quaternion.Euler(0,0,-heading);
                Vector2 Offset(float x,float y)=>markerAt+(Vector2)(rot*new Vector3(x,y,0));
                Quad(p,new Color(1,.5f,.1f,1),Offset(-sz,sz*.7f),Offset(0,-sz),Offset(sz,sz*.7f),Offset(0,sz*.25f));
                if(referenceMarker){
                    Quad(p,new Color(1,.70f,.30f,1),Offset(-sz*.64f,sz*.42f),Offset(0,-sz*.70f),Offset(sz*.64f,sz*.42f),Offset(0,0));
                    Quad(p,new Color(.55f,.21f,.04f,1),Offset(-sz*.22f,sz*.12f),Offset(0,-sz*.29f),Offset(sz*.22f,sz*.12f),Offset(0,0));
                }
                return;
            }
            // Layered dark haze has no world imagery: only the road remains distinct.
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f, b = (i + 1) / 40f;
                float mist = .025f + .055f * Mathf.Exp(-Mathf.Pow((a - horizonValue) / .23f, 2));
                Quad(p, mapTintValue.a > 0 ? mapTintValue : new Color(mist, mist * 1.08f, mist * 1.15f, Mathf.Lerp(.75f, 1f, hazeValue)),
                    new Vector2(0, a*r.height), new Vector2(r.width, a*r.height),
                    new Vector2(r.width, b*r.height+.5f), new Vector2(0, b*r.height+.5f));
            }
            DrawRoad(context);
            Vector2 at=new Vector2(r.width*.5f,r.height*.78f);
            float size=Mathf.Min(r.width,r.height)*.075f;
            Quad(p,new Color(0,0,0,.6f),at+new Vector2(-size*1.25f,size*.9f),
                at+new Vector2(0,-size*1.4f),at+new Vector2(size*1.25f,size*.9f),at+new Vector2(0,size*.45f));
            Quad(p,new Color(1f,.2f,.15f,1),at+new Vector2(-size,size*.65f),
                at+new Vector2(0,-size),at+new Vector2(size,size*.65f),at+new Vector2(0,size*.25f));
        }
    }
}

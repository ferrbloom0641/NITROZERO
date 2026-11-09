using UnityEngine;
using UnityEngine.UIElements;

namespace RacingUI
{
    /// <summary>Editable perspective view of the current straight road, with live vehicle motion.</summary>
    [UxmlElement]
    public partial class NavigationMap : VisualElement
    {
        float roadWidthValue = .55f, horizonValue = .22f, hazeValue = 1f;
        float routeCurveAmountValue = .15f;
        float routeFadeStartValue = .84f, routeFarOpacityValue = .42f;
        bool straightRouteValue;
        float lateral, heading, raceProgress, progressOffset = .075f;
        float markerHorizontalOffset, markerHeading;
        float markerVibrationPixels, routeTravelMetres;
        bool revealUpcomingTurn;
        bool animateStraightTravel;
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
        [UxmlAttribute]
        public float routeCurveAmount { get => routeCurveAmountValue; set { routeCurveAmountValue = Mathf.Clamp(value, .05f, .28f); MarkDirtyRepaint(); } }
        [UxmlAttribute]
        public float routeFadeStart { get => routeFadeStartValue; set { routeFadeStartValue = Mathf.Clamp(value, .35f, .9f); MarkDirtyRepaint(); } }
        [UxmlAttribute]
        public float routeFarOpacity { get => routeFarOpacityValue; set { routeFarOpacityValue = Mathf.Clamp(value, .08f, .75f); MarkDirtyRepaint(); } }
        [UxmlAttribute]
        public bool straightRoute { get => straightRouteValue; set { straightRouteValue = value; MarkDirtyRepaint(); } }

        public NavigationMap()
        {
            pickingMode = PickingMode.Position;
            style.overflow = Overflow.Hidden;
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
        public void SetMarkerMotion(float horizontalOffset, float angle, float vibrationPixels = 0f)
        {
            // Lateral movement and vibration affect only the arrow, never the trail.
            markerHorizontalOffset = Mathf.Clamp(horizontalOffset, -.025f, .025f);
            markerHeading = Mathf.Clamp(angle, -18f, 18f);
            markerVibrationPixels = Mathf.Clamp(vibrationPixels, 0f, 1.5f);
            MarkDirtyRepaint();
        }
        public void SetRouteTravel(float metres, bool revealTurn, bool straightTravel = false)
        {
            routeTravelMetres = Mathf.Max(0f, metres);
            revealUpcomingTurn = revealTurn;
            animateStraightTravel = straightTravel;
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
        Vector2 AmberRoutePoint(Rect r, float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (straightRouteValue)
                // Keep the straight route inside the map artwork. The old -4%~104%
                // range crossed the header/footer and made the line look as if it
                // pierced the minimap frame.
                return new Vector2(r.width * .5f, Mathf.Lerp(.86f, .12f, t) * r.height);

            if (revealUpcomingTurn)
            {
                // Forward travel brings an initially off-screen bend down into view.
                // The visible straight remains centred and its trail remains straight.
                float y = Mathf.Lerp(1.04f, -.04f, t);
                float bendY = Mathf.Min(.30f, -.10f + routeTravelMetres / 450f);
                float turn = Mathf.Clamp01((bendY - y) / .30f);
                float x = .5f - routeCurveAmountValue * 1.15f * turn * turn;
                return new Vector2(x * r.width, y * r.height);
            }

            const float turnStart = .56f;
            float verticalX = .5f;
            Vector2 point;
            if (t <= turnStart)
            {
                float straight = Mathf.InverseLerp(0f, turnStart, t);
                point = new Vector2(verticalX, Mathf.Lerp(1.04f, .43f, straight));
            }
            else
            {
                float turn = Mathf.InverseLerp(turnStart, 1f, t);
                float inverse = 1f - turn;
                Vector2 start = new Vector2(verticalX, .43f);
                Vector2 lowerControl = new Vector2(verticalX, .25f);
                Vector2 upperControl = new Vector2(.50f, .08f);
                Vector2 end = new Vector2(
                    .50f - routeCurveAmountValue * 1.15f, -.04f);
                point = inverse * inverse * inverse * start +
                    3f * inverse * inverse * turn * lowerControl +
                    3f * inverse * turn * turn * upperControl +
                    turn * turn * turn * end;
            }
            return new Vector2(point.x * r.width, point.y * r.height);
        }
        float AmberRouteOpacity(float progress)
        {
            float fade = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(routeFadeStartValue, 1f, progress));
            return Mathf.Lerp(1f, routeFarOpacityValue, fade);
        }
        void DrawAmberRoute(Painter2D p, Rect r, float width, float opacity)
        {
            const int segments = 36;
            p.lineWidth = width;
            for (int i = 0; i < segments; i++)
            {
                float from = i / (float)segments;
                float to = (i + 1) / (float)segments;
                float segmentOpacity = AmberRouteOpacity((from + to) * .5f);
                p.strokeColor = new Color(1f, .66f, .18f, opacity * segmentOpacity);
                p.BeginPath();
                p.MoveTo(AmberRoutePoint(r, from));
                p.LineTo(AmberRoutePoint(r, to));
                p.Stroke();
            }
        }
        void DrawStraightTravel(Painter2D p, Rect r)
        {
            if (!animateStraightTravel || !straightRouteValue) return;
            // Distance-driven highlights flow towards the driver. When the car stops,
            // the highlights stop too; the road and arrow remain straight and centred.
            p.lineWidth = 2.2f;
            for (int i = 0; i < 4; i++)
            {
                float travel = Mathf.Repeat(routeTravelMetres / 180f + i * .25f, 1f);
                float opacity = Mathf.Sin(travel * Mathf.PI) * .65f;
                p.strokeColor = new Color(1f, .90f, .60f, opacity);
                p.BeginPath();
                p.MoveTo(AmberRoutePoint(r, Mathf.Clamp01(1f - travel - .02f)));
                p.LineTo(AmberRoutePoint(r, Mathf.Clamp01(1f - travel + .02f)));
                p.Stroke();
            }
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
                DrawAmberRoute(p, r, 9f, .06f);
                DrawAmberRoute(p, r, 4.5f, .22f);
                DrawAmberRoute(p, r, 2.6f, 1f);
                DrawStraightTravel(p, r);

                const float routeVerticalSpan = .78f;
                bool fixedMarker = revealUpcomingTurn || animateStraightTravel;
                float markerBaseProgress = revealUpcomingTurn ? .18f : .14f;
                float markerProgress = fixedMarker ? markerBaseProgress : Mathf.Clamp(
                    markerBaseProgress + raceProgress * progressOffset / routeVerticalSpan,
                    markerBaseProgress, .82f);
                // Keep the navigation arrow anchored while the route scrolls ahead.
                // Its straight trail grows behind it instead of dragging the arrow.
                float trailStartProgress = fixedMarker
                    ? Mathf.Max(.10f, markerBaseProgress - raceProgress * progressOffset / routeVerticalSpan)
                    : markerBaseProgress;
                var markerBase = AmberRoutePoint(r, trailStartProgress);
                var markerAt = AmberRoutePoint(r, markerProgress);
                float sz=referenceMarker?15:11;
                Vector2 routeMarkerAt = markerAt;
                const float tangentSample = .012f;
                Vector2 routeBefore = AmberRoutePoint(r,
                    Mathf.Max(.001f, markerProgress - tangentSample));
                Vector2 routeAfter = AmberRoutePoint(r,
                    Mathf.Min(.999f, markerProgress + tangentSample));
                Vector2 routeDirection = (routeAfter - routeBefore).normalized;
                float vibration = Application.isPlaying
                    ? Mathf.Sin(Time.time * Mathf.PI * 13f) * markerVibrationPixels * r.height / 175f : 0f;
                markerAt += routeDirection * vibration;
                float margin = Mathf.Min(sz * 1.5f, Mathf.Min(r.width, r.height) * .5f);
                markerAt.x = Mathf.Clamp(markerAt.x + markerHorizontalOffset * r.width, margin, r.width - margin);
                markerAt.y = Mathf.Clamp(markerAt.y, margin, r.height - margin);
                if (Vector2.Distance(markerBase, routeMarkerAt) > .5f) {
                    p.lineWidth = 3f;
                    p.strokeColor = new Color(1f,.55f,.13f,.75f);
                    const int trailSegments = 12;
                    p.BeginPath();
                    p.MoveTo(markerBase);
                    for (int i = 1; i <= trailSegments; i++)
                    {
                        float trailProgress = i / (float)trailSegments;
                        p.LineTo(AmberRoutePoint(r, Mathf.Lerp(trailStartProgress, markerProgress, trailProgress)));
                    }
                    p.Stroke();
                }
                float routeAngle = Mathf.Atan2(routeDirection.y, routeDirection.x) *
                    Mathf.Rad2Deg + 90f;
                var rot=Quaternion.Euler(0,0,routeAngle);
                // Keep all forward tips and the centre fixed. Only the rear wings
                // flex with the car's lateral maneuver; the road trail never bends.
                float rearShear = -Mathf.Tan(markerHeading * Mathf.Deg2Rad);
                Vector2 Offset(float x,float y)=>markerAt+(Vector2)(rot*new Vector3(
                    x + rearShear * Mathf.Max(0f, y), y, 0));
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

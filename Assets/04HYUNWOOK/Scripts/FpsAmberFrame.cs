using UnityEngine;
using UnityEngine.UIElements;
namespace RacingUI
{
    /// <summary>Open helmet rim: side curves stop at the instrument instead of crossing it.</summary>
    [UxmlElement]
    public partial class FpsAmberFrame : VisualElement
    {
        bool lightReflectionValue = true;
        float lightReflectionSpeedValue = .18f;
        float lightReflectionIntensityValue = .9f;
        float lightReflectionStartTime;
        bool lightReflectionWasPlaying;

        [UxmlAttribute]
        public bool lightReflection
        {
            get => lightReflectionValue;
            set { if (lightReflectionValue == value) return; lightReflectionValue = value; MarkDirtyRepaint(); }
        }
        [UxmlAttribute]
        public float lightReflectionSpeed
        {
            get => lightReflectionSpeedValue;
            set
            {
                float clamped = Mathf.Clamp(value, .02f, 1.5f);
                if (Mathf.Approximately(lightReflectionSpeedValue, clamped)) return;
                lightReflectionSpeedValue = clamped;
                MarkDirtyRepaint();
            }
        }
        [UxmlAttribute]
        public float lightReflectionIntensity
        {
            get => lightReflectionIntensityValue;
            set
            {
                float clamped = Mathf.Clamp(value, 0f, 3f);
                if (Mathf.Approximately(lightReflectionIntensityValue, clamped)) return;
                lightReflectionIntensityValue = clamped;
                MarkDirtyRepaint();
            }
        }

        public FpsAmberFrame()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ =>
                lightReflectionStartTime = Time.unscaledTime);
            schedule.Execute(() =>
            {
                if (Application.isPlaying && lightReflectionValue)
                    MarkDirtyRepaint();
            }).Every(16);
        }
        void Draw(MeshGenerationContext ctx)
        {
            if (contentRect.width <= 0 || contentRect.height <= 0) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Butt;
            float scale = Mathf.Min(contentRect.width / 1672f, contentRect.height / 941f);
            if (Application.isPlaying && !lightReflectionWasPlaying)
            {
                lightReflectionStartTime = Time.unscaledTime;
                lightReflectionWasPlaying = true;
            }
            else if (!Application.isPlaying)
            {
                lightReflectionWasPlaying = false;
            }
            Vector2 V(float x, float y) => new Vector2(x * contentRect.width / 1672f, y * contentRect.height / 941f);
            void Stroke(Vector2[] points, float width, Color color)
            {
                p.BeginPath(); p.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
                p.lineWidth = width * scale; p.strokeColor = color; p.Stroke();
            }
            float reflectionSweep = Application.isPlaying && lightReflectionValue
                ? Mathf.Repeat((Time.unscaledTime - lightReflectionStartTime) *
                    lightReflectionSpeedValue, 1f)
                : -1f;
            float FrameTileReflection(float tileProgress)
            {
                if (reflectionSweep < 0f || lightReflectionIntensityValue <= 0f) return 0f;

                // Progress is assigned from the actual thick tile order. Empty thin
                // rail sections therefore no longer consume most of the animation.
                float distance = Mathf.Abs(reflectionSweep - Mathf.Clamp01(tileProgress));
                float strength = 1f - Mathf.Clamp01(distance / .11f);
                return strength * strength * lightReflectionIntensityValue;
            }
            // Dense smooth samples keep the long visor edges curved at every resolution.
            // Same endpoint as instrument-panel: (338,706) + (8%,14%) of (997,210).
            var left = new System.Collections.Generic.List<Vector2> { V(417.76f,735.4f), V(319,822), V(113,822), V(76,756), V(119,711) };
            // A single cubic joins the lower shoulder to the upper visor corner.
            // Its tangents avoid the pinched joins of the previous sine/straight sections.
            for(int i=1;i<=128;i++) {
                float t=i/128f, u=1-t;
                left.Add(V(u*u*u*119+3*u*u*t*24+3*u*t*t*17+t*t*t*87,
                    u*u*u*711+3*u*u*t*554+3*u*t*t*420+t*t*t*269));
            }
            left.Add(V(20,202));left.Add(V(31,71));left.Add(V(91,28));
            var right = new Vector2[left.Count];
            for(int i=0;i<right.Length;i++) right[i]=new Vector2(contentRect.width-left[i].x,left[i].y);
            // The authored 997px instrument is centered at 836.5, not 836.
            right[0]=V(1255.24f,735.4f);
            foreach(var line in new[]{left.ToArray(),right}) {
                // A subtle full-length rail closes the small gaps between the
                // visor curve, lower tiles and instrument shoulder.
                Stroke(line,.8f,new Color(1,.55f,.13f,.55f));
                // The lower tiles own the bright rim. A bright continuous line beneath
                // them used to bridge every gap and produce a heavy double border.
                var upper = new Vector2[line.Length - 4];
                System.Array.Copy(line, 4, upper, 0, upper.Length);
                // No continuous stroke beneath the lower tiles.
                Stroke(upper,9,new Color(1,.46f,.06f,.035f));
                Stroke(upper,4,new Color(1,.48f,.09f,.10f));
                Stroke(upper,1.15f,new Color(1,.55f,.13f,.75f));
                var inset=new Vector2[line.Length];
                for(int i=0;i<inset.Length;i++) {
                    Vector2 tangent = (line[Mathf.Min(i+1,line.Length-1)]-line[Mathf.Max(i-1,0)]).normalized;
                    Vector2 normal = new Vector2(-tangent.y,tangent.x);
                    if(Vector2.Dot(normal,contentRect.center-line[i])<0) normal=-normal;
                    inset[i]=line[i]+normal*(9f*contentRect.width/1672f);
                }
                // Lower secondary rails sit outside the tiles, as in the reference.
                // Keeping them inside made a solid-looking channel around every tile.
                Stroke(new System.ArraySegment<Vector2>(inset,4,inset.Length-4).ToArray(),.6f,new Color(1,.57f,.16f,.18f));
            }
            foreach(bool mirror in new[]{false,true}) {
                Vector2 Side(float x,float y)=>V(mirror?1672f-x:x,y);
                // Follow the wide shoulder below the tiles. The instrument now owns
                // the stepped lower contour, so a second diagonal must not cross it.
                Vector2 sideJoin = V(mirror ? 1360.922f : 312.078f,840.4f);
                Stroke(new[]{Side(119,711),Side(63,756),Side(103,836),Side(300,836),sideJoin},.65f,new Color(1,.49f,.10f,.32f));
                // Only the final tile's end continues to the instrument's upper rail.
                Vector2 topJoin=V(mirror?1255.24f:417.76f,735.4f);
                Stroke(new[]{Side(388.5f,758),topJoin},.85f,new Color(1,.55f,.13f,.75f));
            }
            Stroke(new[]{V(91,28),V(429,46),V(451,39),V(1221,39),V(1243,46),V(1581,28)},.7f,new Color(1,.51f,.12f,.25f));
            void Lit(float ax,float ay,float bx,float by,int count, bool mirror,
                bool lower = false, float[] divisions = null, bool marker = false,
                float reflectionStart = .82f, float reflectionEnd = .98f) {
                // Build in design space, then mirror the entire polygon (including its
                // slanted ends). Reversing only a stroke made left/right cuts disagree.
                Vector2 Map(Vector2 point) => V(mirror ? 1672f-point.x : point.x, point.y);
                for(int i=0;i<count;i++) {
                    float start = divisions == null ? i/(float)count : divisions[i];
                    float end = divisions == null ? (i+1)/(float)count : divisions[i+1];
                    float from = start+(end-start)*(marker ? .04f : .05f);
                    float to = end-(end-start)*(marker ? .15f : .10f);
                    var a=new Vector2(Mathf.Lerp(ax,bx,from),Mathf.Lerp(ay,by,from));
                    var b=new Vector2(Mathf.Lerp(ax,bx,to),Mathf.Lerp(ay,by,to));
                    float light = lower && count > 2 ? (i==0 ? .85f : Mathf.Lerp(.42f,1f,(i-1f)/(count-2f))) : 1f;
                    float reflection = FrameTileReflection(Mathf.Lerp(
                        reflectionStart, reflectionEnd, (i + .5f) / count));
                    // Angled ends match the original visor's luminous tiles.
                    Vector2 d=(b-a).normalized, n=new Vector2(-d.y,d.x);
                    float half = marker ? 2.4f : lower ? 3.6f : 3.4f;
                    // Glow follows the same slanted polygon instead of a square-ended
                    // thick stroke, which previously looked like a second row of blocks.
                    void Tile(float h,float spread,Color color) {
                        p.BeginPath();p.MoveTo(Map(a-n*h+d*(2-spread)));p.LineTo(Map(b-n*h+d*(2+spread)));
                        p.LineTo(Map(b+n*h-d*(2-spread)));p.LineTo(Map(a+n*h-d*(2+spread)));p.ClosePath();
                        p.fillColor=color;p.Fill();
                    }
                    if(!marker) {
                        Tile(half+7,5,new Color(1,.52f,.10f,(.025f+.10f*reflection)*light));
                        Tile(half+3,2,new Color(1,.60f,.12f,(.065f+.20f*reflection)*light));
                    }
                    p.BeginPath();p.MoveTo(Map(a-n*half+d*2));p.LineTo(Map(b-n*half+d*2));
                    p.LineTo(Map(b+n*half-d*2));p.LineTo(Map(a+n*half-d*2));p.ClosePath();
                    Color baseColor = new Color(1,.59f,.15f,.97f*light);
                    p.fillColor=Color.Lerp(baseColor,new Color(1f,.96f,.72f,1f),
                        Mathf.Clamp01(reflection));p.Fill();
                }
            }
            void LowerTiles(bool mirror) {
                // These are individual flat luminous plates, not equal subdivisions
                // of a stroked rail. Corner plates turn with the frame as one piece.
                int plateIndex=0;
                const int plateCount=11;
                void Plate(float brightness, params Vector2[] points) {
                    Vector2 Map(Vector2 a)=>V(mirror?1672f-a.x:a.x,a.y);
                    Vector2 center=Vector2.zero;
                    for(int i=0;i<points.Length;i++)center+=points[i];
                    center/=points.Length;
                    float reflection=FrameTileReflection(Mathf.Lerp(.02f,.72f,
                        plateIndex/(float)(plateCount-1)));
                    plateIndex++;
                    void FillPlate(float expansion,Color color) {
                        p.BeginPath();
                        for(int i=0;i<points.Length;i++) {
                            Vector2 expanded=center+(points[i]-center)*expansion;
                            if(i==0)p.MoveTo(Map(expanded));else p.LineTo(Map(expanded));
                        }
                        p.ClosePath();p.fillColor=color;p.Fill();
                    }
                    FillPlate(1f,new Color(1,.57f,.12f,brightness));
                    if(reflection>0f) {
                        FillPlate(1.42f,new Color(1f,.55f,.12f,
                            .13f*Mathf.Clamp01(reflection)));
                        FillPlate(1f,new Color(1f,.97f,.76f,
                            .88f*Mathf.Clamp01(reflection)));
                    }
                }
                Plate(.97f,new Vector2(72,757),new Vector2(79,754),new Vector2(88,770),new Vector2(81,774));
                Plate(.97f,new Vector2(83,777),new Vector2(90,773),new Vector2(99,789),new Vector2(92,793));
                // Small outer elbow, four subdued straight plates, one broad inner elbow.
                Plate(.90f,new Vector2(107,812),new Vector2(115,819),new Vector2(130,819),new Vector2(133,825),new Vector2(112,825),new Vector2(104,815));
                Plate(.46f,new Vector2(136,819),new Vector2(163,819),new Vector2(166,825),new Vector2(139,825));
                Plate(.51f,new Vector2(169,819),new Vector2(196,819),new Vector2(199,825),new Vector2(172,825));
                Plate(.58f,new Vector2(202,819),new Vector2(229,819),new Vector2(232,825),new Vector2(205,825));
                Plate(.66f,new Vector2(235,819),new Vector2(262,819),new Vector2(265,825),new Vector2(238,825));
                Plate(.98f,new Vector2(269,818),new Vector2(316,818),new Vector2(329,806),new Vector2(334,812),new Vector2(320,826),new Vector2(272,826));
                // Separated slanted plates continue from the broad elbow toward the instrument.
                Plate(.98f,new Vector2(332,803),new Vector2(348,789),new Vector2(353,795),new Vector2(337,809));
                Plate(.98f,new Vector2(351,786),new Vector2(367,772),new Vector2(372,778),new Vector2(356,792));
                Plate(.96f,new Vector2(370,769),new Vector2(386,755),new Vector2(391,761),new Vector2(375,775));
                // Leave the final approach to the instrument as the thin connected
                // rail drawn above, rather than placing another luminous plate here.
            }
            foreach(bool mirror in new[]{false,true}) {
                Lit(38,221,79,257,2,mirror);
                LowerTiles(mirror);
                // A thin illuminated tail leads into the short elbow tile.
                Vector2 Side(float x,float y)=>V(mirror?1672f-x:x,y);
                Stroke(new[]{Side(97,795),Side(113,822)},1.8f,new Color(1,.57f,.13f,.85f));
            }
            // Thin mirrored shoulder ticks sit just above the instrument rail.
            Lit(496,728,540,728,4,false,marker:true,
                reflectionStart:.74f,reflectionEnd:.82f);
            Lit(496,728,540,728,4,true,marker:true,
                reflectionStart:.74f,reflectionEnd:.82f);
            // The instrument owns the shoulder rails; do not draw a second floating outline here.

        }
    }
}

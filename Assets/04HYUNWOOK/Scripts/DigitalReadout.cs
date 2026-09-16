using UnityEngine;
using UnityEngine.UIElements;

namespace RacingUI
{
    /// <summary>
    /// Digital numeral element used by the FPS/TPS racing HUD.
    /// Kept in its own file so deleting or replacing the HUD controller does not
    /// unregister every custom element referenced by the UXML documents.
    /// </summary>
    [UxmlElement]
    public partial class DigitalReadout : VisualElement
    {
        string displayText = "000";
        bool opticalCenterValue;
        bool fitToBoundsValue;
        float offSegmentOpacityValue = 0.055f;
        float segmentThicknessValue = 0.11f;
        float digitSpacingValue = 0.17f;

        [UxmlAttribute]
        public string text
        {
            get => displayText;
            set
            {
                string next = value ?? string.Empty;
                if (displayText == next) return;
                displayText = next;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public bool opticalCenter
        {
            get => opticalCenterValue;
            set
            {
                if (opticalCenterValue == value) return;
                opticalCenterValue = value;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public bool fitToBounds
        {
            get => fitToBoundsValue;
            set
            {
                if (fitToBoundsValue == value) return;
                fitToBoundsValue = value;
                if (value) style.overflow = Overflow.Hidden;
                else style.overflow = StyleKeyword.Null;
                MarkDirtyRepaint();
            }
        }

        // Retain the original UXML attributes for compatibility with older HUD files.
        [UxmlAttribute]
        public float offSegmentOpacity
        {
            get => offSegmentOpacityValue;
            set
            {
                offSegmentOpacityValue = Mathf.Clamp01(value);
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public float segmentThickness
        {
            get => segmentThicknessValue;
            set
            {
                segmentThicknessValue = Mathf.Clamp(value, 0.04f, 0.18f);
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public float digitSpacing
        {
            get => digitSpacingValue;
            set
            {
                digitSpacingValue = Mathf.Clamp(value, 0.04f, 0.3f);
                MarkDirtyRepaint();
            }
        }

        public DigitalReadout()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext context)
        {
            Rect area = contentRect;
            if (area.width <= 0f || area.height <= 0f) return;

            // The bundled DS-Digital outline renderer is shared by the game and
            // UI Builder, preserving the authored numeral shape without a font atlas.
            HudGlyphRenderer.Draw(context, area, displayText,
                resolvedStyle.fontSize, resolvedStyle.color, fitToBoundsValue);
        }
    }
}

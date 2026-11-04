using UnityEngine;

public sealed partial class AutonomousRaceController
{
    Vector3[] centreTangents, sideVectors, sideTangents;
    float smoothRoadInset;
    public float SmoothRoadInset => smoothRoadInset;

    void BuildSmoothRoad()
    {
        int count = centres.Length;
        centreTangents = new Vector3[count]; sideVectors = new Vector3[count]; sideTangents = new Vector3[count];
        for (int i = 0; i < count; i++) sideVectors[i] = (right[i] - left[i]).normalized;
        for (int i = 0; i < count; i++)
        {
            centreTangents[i] = RoadTangent(centres, i);
            sideTangents[i] = RoadTangent(sideVectors, i);
        }
        // Reserve a constant envelope for the curve's deviation from the visible polygonal road.
        // A constant reserve avoids introducing new lateral snaps through per-frame clamping.
        smoothRoadInset = 0;
        for (int i = 0; i < count - 1; i++)
        {
            float length = cumulative[i + 1] - cumulative[i];
            int samples = Mathf.Max(16, Mathf.CeilToInt(length));
            for (int j = 0; j <= samples; j++)
            {
                float t = j / (float)samples;
                float d = Mathf.Lerp(cumulative[i], cumulative[i + 1], t);
                Vector3 straightCentre = Vector3.Lerp(centres[i], centres[i + 1], t);
                Vector3 straightSide = Vector3.Lerp(sideVectors[i], sideVectors[i + 1], t).normalized;
                Vector3 curvedCentre = SmoothRoadSample(i, d, 0);
                Vector3 curvedSide = (SmoothRoadSample(i, d, 1) - curvedCentre).normalized;
                float halfWidth = Mathf.Max(Vector3.Distance(left[i], right[i]), Vector3.Distance(left[i + 1], right[i + 1])) * .5f;
                float reserve = Vector3.Distance(curvedCentre, straightCentre) + halfWidth * Vector3.Distance(curvedSide, straightSide);
                smoothRoadInset = Mathf.Max(smoothRoadInset, reserve);
            }
        }
        smoothRoadInset += .05f;
    }

    Vector3 RoadTangent(Vector3[] values, int i)
    {
        if (i == 0) return (values[1] - values[0]) / Mathf.Max(.001f, cumulative[1] - cumulative[0]);
        if (i == values.Length - 1) return (values[i] - values[i - 1]) / Mathf.Max(.001f, cumulative[i] - cumulative[i - 1]);
        float before = Mathf.Max(.001f, cumulative[i] - cumulative[i - 1]);
        float after = Mathf.Max(.001f, cumulative[i + 1] - cumulative[i]);
        Vector3 incoming = (values[i] - values[i - 1]) / before;
        Vector3 outgoing = (values[i + 1] - values[i]) / after;
        return (incoming * after + outgoing * before) / (before + after);
    }

    Vector3 SmoothRoadSample(int i, float distance, float lateral)
    {
        float length = cumulative[i + 1] - cumulative[i];
        float t = Mathf.Clamp01((distance - cumulative[i]) / Mathf.Max(.001f, length));
        Vector3 centre = Hermite(centres[i], centres[i + 1], centreTangents[i] * length, centreTangents[i + 1] * length, t);
        Vector3 side = Hermite(sideVectors[i], sideVectors[i + 1], sideTangents[i] * length, sideTangents[i + 1] * length, t).normalized;
        return centre + side * lateral;
    }

    static Vector3 Hermite(Vector3 a, Vector3 b, Vector3 da, Vector3 db, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return (2*t3-3*t2+1)*a + (t3-2*t2+t)*da + (-2*t3+3*t2)*b + (t3-t2)*db;
    }
}

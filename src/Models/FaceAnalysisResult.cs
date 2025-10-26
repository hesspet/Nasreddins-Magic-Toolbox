using System.Collections.Generic;

namespace Toolbox.Models;

public sealed record FaceAnalysisResult(
    bool HasFace,
    bool IsLikelyHuman,
    BoundingBox? FaceBounds,
    FaceFeature? LeftEye,
    FaceFeature? RightEye,
    FaceFeature? Nose,
    int ImageWidth,
    int ImageHeight,
    float SkinCoverage,
    float ConfidenceScore)
{
    public IReadOnlyList<FaceFeature> EnumerateFeatures()
    {
        var features = new List<FaceFeature>(3);

        if (LeftEye is not null)
        {
            features.Add(LeftEye);
        }

        if (RightEye is not null)
        {
            features.Add(RightEye);
        }

        if (Nose is not null)
        {
            features.Add(Nose);
        }

        return features;
    }
}

public sealed record FaceFeature(FaceFeatureKind Kind, BoundingBox Bounds, float Confidence);

public enum FaceFeatureKind
{
    LeftEye,
    RightEye,
    Nose
}

public readonly record struct BoundingBox(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
}

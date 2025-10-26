using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Toolbox.Models;

namespace Toolbox.Helpers;

/// <summary>
///     Analysiert Bilddaten heuristisch, um Hautbereiche und einfache Gesichtsmerkmale zu erkennen
///     und daraus eine Aussage darüber abzuleiten, ob es sich wahrscheinlich um ein menschliches
///     Gesicht handelt. Die Klasse verwendet ausschließlich Bildverarbeitung auf Pixelebene und
///     kommt ohne externe KI- oder ML-Modelle aus.
/// </summary>
public sealed class FaceAnalysisService
{
    /// <summary>
    ///     Führt die komplette Analyse eines Bildes durch. Dazu werden zunächst Mindestgrößen geprüft,
    ///     anschließend Hautpixel markiert und daraus ein Gesichtsrahmen abgeleitet. Im Bereich des
    ///     Rahmens werden wiederum Augen und Nase anhand von Helligkeits- und Farbverteilungen gesucht,
    ///     um eine Vertrauensbewertung und die Wahrscheinlichkeit eines menschlichen Gesichts zu
    ///     bestimmen.
    /// </summary>
    /// <param name="imageData">Die zu prüfenden Bilddaten im RGBA-Format.</param>
    /// <returns>
    ///     Ein <see cref="FaceAnalysisResult" />, das sowohl die gefundenen Regionen als auch
    ///     Bewertungen zur Bedeckung und Plausibilität enthält.
    /// </returns>
    public FaceAnalysisResult Analyze(ReadOnlySpan<byte> imageData)
    {
        if (imageData.IsEmpty)
        {
            return new FaceAnalysisResult(false, false, null, null, null, null, 0, 0, 0f, 0f);
        }

        using var image = Image.Load<Rgba32>(imageData);

        var width = image.Width;
        var height = image.Height;

        if (width < 32 || height < 32)
        {
            return new FaceAnalysisResult(false, false, null, null, null, null, width, height, 0f, 0f);
        }

        var skinMask = new bool[width * height];
        var totalSkin = 0;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                if (!IsSkinPixel(pixel))
                {
                    continue;
                }

                var index = (y * width) + x;
                skinMask[index] = true;
                totalSkin++;

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (totalSkin < Math.Max(200, (int)(width * height * MinimumSkinFraction)) ||
            maxX <= minX ||
            maxY <= minY)
        {
            return new FaceAnalysisResult(false, false, null, null, null, null, width, height, 0f, 0f);
        }

        var faceBox = new BoundingBox(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        var faceArea = Math.Max(1, (int)(faceBox.Width * faceBox.Height));
        var faceSkinCount = 0;

        for (var y = (int)faceBox.Y; y < faceBox.Bottom && y < height; y++)
        {
            for (var x = (int)faceBox.X; x < faceBox.Right && x < width; x++)
            {
                if (skinMask[(y * width) + x])
                {
                    faceSkinCount++;
                }
            }
        }

        var skinCoverage = MathF.Min(1f, faceSkinCount / (float)faceArea);
        if (skinCoverage < MinimumFaceCoverage)
        {
            return new FaceAnalysisResult(true, false, faceBox, null, null, null, width, height, skinCoverage, 0f);
        }

        var features = new List<FaceFeature>(3);
        var leftEye = DetectEye(image, faceBox, width, height, isLeft: true);
        var rightEye = DetectEye(image, faceBox, width, height, isLeft: false);
        var nose = DetectNose(image, faceBox, width, height);

        if (leftEye is not null)
        {
            features.Add(leftEye);
        }

        if (rightEye is not null)
        {
            features.Add(rightEye);
        }

        if (nose is not null)
        {
            features.Add(nose);
        }

        var hasEyePair = leftEye is not null && rightEye is not null;
        var hasAnyEye = leftEye is not null || rightEye is not null;
        var hasNose = nose is not null;

        var featureConfidence = features.Count > 0
            ? Clamp((float)AverageConfidence(features) / 0.05f, 0f, 1f)
            : 0f;

        var coverageScore = Clamp((skinCoverage - MinimumFaceCoverage) / (1f - MinimumFaceCoverage), 0f, 1f);
        var combinedConfidence = Clamp((coverageScore * 0.6f) + (featureConfidence * 0.4f), 0f, 1f);
        var likelyHuman = features.Count > 0 && (hasEyePair || (hasAnyEye && hasNose));

        return new FaceAnalysisResult(
            true,
            likelyHuman,
            faceBox,
            leftEye,
            rightEye,
            nose,
            width,
            height,
            skinCoverage,
            combinedConfidence);
    }

    private const float EyeDarknessThreshold = 0.38f;
    private const float MinimumFaceCoverage = 0.28f;
    private const float MinimumSkinFraction = 0.0125f;
    private const float NoseLowerLumaThreshold = 0.35f;
    private const float NoseUpperLumaThreshold = 0.75f;

    /// <summary>
    ///     Ermittelt die durchschnittliche Vertrauensbewertung aller gefundenen Merkmale, um daraus
    ///     eine Gesamtbewertung ableiten zu können.
    /// </summary>
    private static double AverageConfidence(IEnumerable<FaceFeature> features)
    {
        double sum = 0d;
        var count = 0;
        foreach (var feature in features)
        {
            sum += feature.Confidence;
            count++;
        }

        return count == 0 ? 0d : sum / count;
    }

    /// <summary>
    ///     Stellt sicher, dass ein Wert innerhalb eines definierten Bereichs bleibt.
    /// </summary>
    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    /// <summary>
    ///     Sucht innerhalb des Gesichtsrahmens nach einem dunklen Bereich, der die typische Position
    ///     eines linken oder rechten Auges einnimmt. Grundlage sind Helligkeitsschwellen und eine
    ///     Mindestbedeckung des Untersuchungsbereichs.
    /// </summary>
    private static FaceFeature? DetectEye(Image<Rgba32> image, BoundingBox faceBox, int width, int height, bool isLeft)
    {
        var xStart = (int)(faceBox.X + (faceBox.Width * (isLeft ? 0.1f : 0.55f)));
        var xEnd = (int)(faceBox.X + (faceBox.Width * (isLeft ? 0.45f : 0.9f)));
        var yStart = (int)(faceBox.Y + (faceBox.Height * 0.2f));
        var yEnd = (int)(faceBox.Y + (faceBox.Height * 0.55f));

        xStart = Math.Clamp(xStart, 0, width - 1);
        xEnd = Math.Clamp(xEnd, 0, width);
        yStart = Math.Clamp(yStart, 0, height - 1);
        yEnd = Math.Clamp(yEnd, 0, height);

        if (xEnd <= xStart + 2 || yEnd <= yStart + 2)
        {
            return null;
        }

        var regionArea = Math.Max(1, (xEnd - xStart) * (yEnd - yStart));
        var darkPixels = 0;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = yStart; y < yEnd; y++)
        {
            for (var x = xStart; x < xEnd; x++)
            {
                var luma = GetLuminance(image[x, y]);
                if (luma > EyeDarknessThreshold)
                {
                    continue;
                }

                darkPixels++;

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        var coverage = darkPixels / (float)regionArea;
        if (coverage < 0.002f || maxX <= minX || maxY <= minY)
        {
            return null;
        }

        var bounds = new BoundingBox(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        return new FaceFeature(isLeft ? FaceFeatureKind.LeftEye : FaceFeatureKind.RightEye, bounds, coverage);
    }

    /// <summary>
    ///     Identifiziert im zentralen Bereich des Gesichts eine potenzielle Nase, indem Pixel mit
    ///     passenden Helligkeits- und Farbkontrasten gezählt und zu einem Bereich zusammengefasst
    ///     werden.
    /// </summary>
    private static FaceFeature? DetectNose(Image<Rgba32> image, BoundingBox faceBox, int width, int height)
    {
        var xStart = (int)(faceBox.X + (faceBox.Width * 0.3f));
        var xEnd = (int)(faceBox.X + (faceBox.Width * 0.7f));
        var yStart = (int)(faceBox.Y + (faceBox.Height * 0.35f));
        var yEnd = (int)(faceBox.Y + (faceBox.Height * 0.85f));

        xStart = Math.Clamp(xStart, 0, width - 1);
        xEnd = Math.Clamp(xEnd, 0, width);
        yStart = Math.Clamp(yStart, 0, height - 1);
        yEnd = Math.Clamp(yEnd, 0, height);

        if (xEnd <= xStart + 2 || yEnd <= yStart + 2)
        {
            return null;
        }

        var regionArea = Math.Max(1, (xEnd - xStart) * (yEnd - yStart));
        var candidatePixels = 0;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = yStart; y < yEnd; y++)
        {
            for (var x = xStart; x < xEnd; x++)
            {
                var pixel = image[x, y];
                var luma = GetLuminance(pixel);
                if (luma < NoseLowerLumaThreshold || luma > NoseUpperLumaThreshold)
                {
                    continue;
                }

                var chromaDifference = Math.Abs(pixel.R - pixel.G) + Math.Abs(pixel.G - pixel.B);
                if (chromaDifference < 12)
                {
                    continue;
                }

                candidatePixels++;

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        var coverage = candidatePixels / (float)regionArea;
        if (coverage < 0.01f || maxX <= minX || maxY <= minY)
        {
            return null;
        }

        var bounds = new BoundingBox(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        return new FaceFeature(FaceFeatureKind.Nose, bounds, coverage);
    }

    /// <summary>
    ///     Berechnet die wahrgenommene Helligkeit eines Pixels nach der ITU-R-BT.601-Formel.
    /// </summary>
    private static float GetLuminance(Rgba32 pixel)
    {
        return ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;
    }

    /// <summary>
    ///     Prüft anhand mehrerer Farbräume (HSV und YCbCr), ob ein Pixel plausibel als Haut eingeordnet
    ///     werden kann. Nur wenn beide Kriterien zutreffen, wird der Pixel als Hautpixel gewertet.
    /// </summary>
    private static bool IsSkinPixel(Rgba32 pixel)
    {
        var r = pixel.R / 255f;
        var g = pixel.G / 255f;
        var b = pixel.B / 255f;

        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        var hue = 0f;
        if (delta > 0f)
        {
            if (MathF.Abs(max - r) < 0.0001f)
            {
                hue = ((g - b) / delta) % 6f;
            }
            else if (MathF.Abs(max - g) < 0.0001f)
            {
                hue = ((b - r) / delta) + 2f;
            }
            else
            {
                hue = ((r - g) / delta) + 4f;
            }

            hue *= 60f;
            if (hue < 0f)
            {
                hue += 360f;
            }
        }

        var saturation = max <= 0f ? 0f : delta / max;
        var value = max;

        var isHsvSkin = hue >= 0f && hue <= 50f && saturation >= 0.23f && saturation <= 0.68f && value >= 0.35f && value <= 1f;

        var rByte = pixel.R;
        var gByte = pixel.G;
        var bByte = pixel.B;
        var cb = 128f - (0.168736f * rByte) - (0.331264f * gByte) + (0.5f * bByte);
        var cr = 128f + (0.5f * rByte) - (0.418688f * gByte) - (0.081312f * bByte);
        var isYcbcrSkin = cb >= 77f && cb <= 127f && cr >= 133f && cr <= 173f;

        return isHsvSkin && isYcbcrSkin;
    }
}

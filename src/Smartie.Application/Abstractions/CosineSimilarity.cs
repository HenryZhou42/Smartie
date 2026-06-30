namespace Smartie.Application.Abstractions;

public static class CosineSimilarity
{
    public static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Embedding vectors must have the same dimension.");
        }

        if (a.Length == 0)
        {
            return 0f;
        }

        var dot = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA <= 0f || magnitudeB <= 0f)
        {
            return 0f;
        }

        return dot / (MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB));
    }
}

using System.Runtime.InteropServices;

namespace Smartie.Application.Abstractions;

public static class EmbeddingVectorConverter
{
    public static byte[] ToBytes(float[] vector) =>
        MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    public static float[] FromBytes(byte[] bytes) =>
        MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}

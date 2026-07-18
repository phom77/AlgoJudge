using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AlgoJudge.ContentTool.Generation;

internal static class DeterministicSeed
{
    public static int Derive(string group, int baseSeed, int zeroBasedIndex)
    {
        var material = Encoding.UTF8.GetBytes(
            $"algojudge-generator-v1\n{group}\n{baseSeed}\n{zeroBasedIndex}");
        var hash = SHA256.HashData(material);
        return BinaryPrimitives.ReadInt32BigEndian(hash);
    }
}

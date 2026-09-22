using System.Security.Cryptography;
using System.Text;

namespace Aegis.Sanitizer;

public sealed class SourceHasher
{
    public string Compute(string source)
    {
        byte[] bytes =
            Encoding.UTF8.GetBytes(source);

        byte[] hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }
}
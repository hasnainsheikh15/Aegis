using Aegis.Sanitizer;

namespace Sanitizer.Tests;

public class SourceHasherTests
{
    [Fact]
    public void SameSource_ProducesSameHash()
    {
        SourceHasher hasher = new();

        string first =
            hasher.Compute("hello world");

        string second =
            hasher.Compute("hello world");

        Assert.Equal(
            first,
            second
        );
    }

    [Fact]
    public void DifferentSource_ProducesDifferentHash()
    {
        SourceHasher hasher = new();

        string first =
            hasher.Compute("hello world");

        string second =
            hasher.Compute("hello World");

        Assert.NotEqual(
            first,
            second
        );
    }

    [Fact]
    public void Hash_IsSha256Hex()
    {
        SourceHasher hasher = new();

        string hash =
            hasher.Compute("hello world");

        Assert.Equal(
            64,
            hash.Length
        );

        Assert.Matches(
            "^[0-9a-f]{64}$",
            hash
        );
    }
}
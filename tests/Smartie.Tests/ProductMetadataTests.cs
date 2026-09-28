using System.Reflection;
using Smartie.Contracts;

namespace Smartie.Tests;

public class ProductMetadataTests
{
    [Fact]
    public void AssemblyAndAbout_AgreeOnBetaVersionAndBuildDate()
    {
        var version = typeof(ProductMetadata).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.StartsWith(ProductMetadata.Version + "-beta+", version);
        Assert.Matches(@"^\d{4}\.\d{2}\.\d{2}$", ProductMetadata.BuildNumber);
        Assert.Equal("Beta", ProductMetadata.ReleaseLabel);
    }
}

using Xunit;

namespace InfraHarbor.UnitTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void DomainAssembly_IsLoadable()
    {
        Assert.NotNull(typeof(Domain.AssemblyMarker).Assembly);
    }
}

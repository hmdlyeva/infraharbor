using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.UnitTests;

public sealed class IdentityModelTests
{
    [Fact]
    public void StableRoleNames_AreUniqueAndExpected()
    {
        Assert.Equal(4, RoleNames.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(RoleNames.Owner, RoleNames.All);
        Assert.Contains(RoleNames.Admin, RoleNames.All);
        Assert.Contains(RoleNames.Operator, RoleNames.All);
        Assert.Contains(RoleNames.Viewer, RoleNames.All);
    }

    [Fact]
    public void IdentityModel_ContainsUniqueNormalizedEmailIndex()
    {
        using var context = CreateContext();
        var userType = context.Model.FindEntityType(typeof(ApplicationUser));

        Assert.NotNull(userType);
        var emailIndex = Assert.Single(userType!.GetIndexes().Where(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ApplicationUser.NormalizedEmail)])));
        Assert.True(emailIndex.IsUnique);
    }

    private static InfraHarborDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InfraHarborDbContext>()
            .UseNpgsql("Host=localhost;Database=infraharbor_model;Username=infraharbor;Password=model-only")
            .Options;

        return new InfraHarborDbContext(options);
    }
}

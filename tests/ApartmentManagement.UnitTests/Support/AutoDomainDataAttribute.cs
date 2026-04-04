using AutoFixture;
using AutoFixture.Xunit2;

namespace ApartmentManagement.UnitTests.Support;

/// <summary>xUnit theory data from AutoFixture with recursion omitted (safe for EF-style entities).</summary>
public sealed class AutoDomainDataAttribute : AutoDataAttribute
{
    public AutoDomainDataAttribute()
        : base(() => OmitRecursionCustomization.CreateFixture())
    {
    }
}

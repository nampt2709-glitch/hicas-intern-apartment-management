using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.UnitTests.Support;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Domain;

/// <summary>Demonstrates AutoFixture generating EF-style graphs without infinite recursion.</summary>
public sealed class ApartmentEntityAutoFixtureTests
{
    [Theory]
    [AutoDomainData]
    public void Apartment_FromAutoFixture_Is_Not_Null(Apartment apartment)
    {
        apartment.Should().NotBeNull();
        apartment.Id.Should().NotBeEmpty();
        apartment.ApartmentNumber.Should().NotBeNull();
    }

}

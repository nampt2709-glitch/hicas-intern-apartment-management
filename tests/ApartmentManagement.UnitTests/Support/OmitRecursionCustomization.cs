using System.Linq;
using AutoFixture;

namespace ApartmentManagement.UnitTests.Support;

/// <summary>AutoFixture setup that avoids infinite recursion on entity graphs (e.g. navigation properties).</summary>
public static class OmitRecursionCustomization
{
    public static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        foreach (var b in fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList())
            fixture.Behaviors.Remove(b);
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        // Default AutoFixture picks arbitrary ctor args for DateOnly and can produce invalid (e.g. month 31).
        fixture.Register(() =>
        {
            var y = Random.Shared.Next(2000, 2031);
            var m = Random.Shared.Next(1, 13);
            var d = Random.Shared.Next(1, DateTime.DaysInMonth(y, m) + 1);
            return new DateOnly(y, m, d);
        });
        return fixture;
    }
}

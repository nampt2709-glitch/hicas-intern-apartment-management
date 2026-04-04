using ApartmentManagement.Utilities;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Utilities;

public sealed class SqlLikePrefixTests
{
    [Fact]
    public void ForStartsWith_Empty_Returns_Percent_Only()
    {
        SqlLikePrefix.ForStartsWith("").Should().Be("%");
    }

    [Fact]
    public void ForStartsWith_Null_Returns_Percent_Only()
    {
        SqlLikePrefix.ForStartsWith(null!).Should().Be("%");
    }

    [Fact]
    public void Escape_Escapes_Like_Special_Chars()
    {
        SqlLikePrefix.Escape("a[b]%_").Should().Be("a[[]b][%][_]");
    }

    [Fact]
    public void ForStartsWith_Appends_Percent()
    {
        SqlLikePrefix.ForStartsWith("A101").Should().Be("A101%");
    }
}

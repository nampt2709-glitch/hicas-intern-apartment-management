using ApartmentManagement.Utilities;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Utilities;

public sealed class QueryableExtensionsTests
{
    [Fact]
    public void ToPagedResult_Computes_TotalPages_And_Preserves_Items()
    {
        var items = new List<int> { 1, 2, 3 };
        var result = items.ToPagedResult(pageNumber: 2, pageSize: 3, totalCount: 10);

        result.Items.Should().BeEquivalentTo(items, opts => opts.WithStrictOrdering());
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.TotalCount.Should().Be(10);
        result.TotalPages.Should().Be(4);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    public void ToPagedResult_TotalPages_Ceiling(int total, int pageSize, int expectedPages)
    {
        var result = Array.Empty<string>().ToPagedResult(1, pageSize, total);
        result.TotalPages.Should().Be(expectedPages);
    }
}

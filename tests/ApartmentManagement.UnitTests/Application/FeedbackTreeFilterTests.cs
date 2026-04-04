using ApartmentManagement.API.V1.Interfaces.Repositories;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Application;

public sealed class FeedbackTreeFilterTests
{
    [Fact]
    public void FeedbackTreeFilter_Struct_Holds_Scope_Flags()
    {
        var acting = Guid.NewGuid();
        var root = Guid.NewGuid();
        var f = new FeedbackTreeFilter(AllRootThreads: false, ScopeRootId: root, RestrictToActingUserOnly: true, ActingUserId: acting);

        f.AllRootThreads.Should().BeFalse();
        f.ScopeRootId.Should().Be(root);
        f.RestrictToActingUserOnly.Should().BeTrue();
        f.ActingUserId.Should().Be(acting);
    }
}

using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Enums;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using ApartmentManagement.DataSeed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.UnitTests.Persistence;

public sealed class SeedDataCleanupTests
{
    [Fact]
    public async Task CleanupTaggedBusinessDataAsync_removes_rows_with_sql_test_seed_tag()
    {
        var options = new DbContextOptionsBuilder<ApartmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApartmentDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var aptId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        db.Apartments.Add(new Apartment
        {
            Id = aptId,
            ApartmentNumber = "X",
            Floor = 1,
            Area = 1,
            Status = ApartmentStatus.Available,
            CreatedBy = SeedData.SeedDataTag,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await SeedData.CleanupTaggedDomainOnlyAsync(db, CancellationToken.None);

        (await db.Apartments.IgnoreQueryFilters().AnyAsync(a => a.Id == aptId)).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupTaggedBusinessDataAsync_removes_feedback_tree_with_tag()
    {
        var options = new DbContextOptionsBuilder<ApartmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApartmentDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var rootId = Guid.Parse("44444444-4444-4444-4444-444444444401");
        var childId = Guid.Parse("44444444-4444-4444-4444-444444444402");
        var userId = Guid.NewGuid();

        db.Set<ApplicationUser>().Add(
            new ApplicationUser
            {
                Id = userId,
                UserName = "t@t.local",
                NormalizedUserName = "T@T.LOCAL",
                Email = "t@t.local",
                NormalizedEmail = "T@T.LOCAL",
                EmailConfirmed = true,
                FullName = "T",
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });

        db.Feedbacks.Add(new Feedback
        {
            Id = rootId,
            UserId = userId,
            Content = "root",
            Path = "/r",
            CreatedBy = SeedData.SeedDataTag,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.Feedbacks.Add(new Feedback
        {
            Id = childId,
            UserId = userId,
            Content = "child",
            Path = "/r/c",
            ParentFeedbackId = rootId,
            CreatedBy = SeedData.SeedDataTag,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await SeedData.CleanupTaggedDomainOnlyAsync(db, CancellationToken.None);

        (await db.Feedbacks.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }
}

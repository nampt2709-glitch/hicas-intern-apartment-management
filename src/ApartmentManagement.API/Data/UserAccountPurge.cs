using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data;

/// <summary>
/// Hard-removes dependent rows so <see cref="UserManager{TUser}.DeleteAsync"/> can succeed (FK Restrict on feedbacks / refresh tokens).
/// </summary>
public static class UserAccountPurge
{
    public static async Task PurgeUserDependenciesAsync(ApartmentDbContext db, Guid userId, CancellationToken cancellationToken = default)
    {
        var refresh = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        if (refresh.Count > 0)
        {
            db.RefreshTokens.RemoveRange(refresh);
            await db.SaveChangesAsync(cancellationToken);
        }

        var myFeedbackIds = await db.Feedbacks.IgnoreQueryFilters()
            .Where(f => f.UserId == userId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        if (myFeedbackIds.Count > 0)
        {
            var tagSet = myFeedbackIds.ToHashSet();
            var candidates = await db.Feedbacks.IgnoreQueryFilters()
                .Where(x => x.ParentFeedbackId != null)
                .ToListAsync(cancellationToken);

            var changed = false;
            foreach (var row in candidates)
            {
                if (row.ParentFeedbackId is { } pid && tagSet.Contains(pid))
                {
                    row.ParentFeedbackId = null;
                    changed = true;
                }
            }

            if (changed)
                await db.SaveChangesAsync(cancellationToken);

            while (true)
            {
                var mine = await db.Feedbacks.IgnoreQueryFilters()
                    .Where(f => f.UserId == userId)
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken);
                if (mine.Count == 0)
                    break;

                var parentIdsPointedAt = (await db.Feedbacks.IgnoreQueryFilters()
                    .Where(f => f.ParentFeedbackId != null)
                    .Select(f => f.ParentFeedbackId!.Value)
                    .ToListAsync(cancellationToken)).ToHashSet();

                var leaves = mine.Where(id => !parentIdsPointedAt.Contains(id)).ToList();
                if (leaves.Count == 0)
                    break;

                var rows = await db.Feedbacks.IgnoreQueryFilters()
                    .Where(f => leaves.Contains(f.Id))
                    .ToListAsync(cancellationToken);
                db.Feedbacks.RemoveRange(rows);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var residents = await db.Residents.IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var r in residents)
            r.UserId = null;

        if (residents.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task<IdentityResult> DeleteUserHardAsync(
        UserManager<ApplicationUser> userManager,
        ApartmentDbContext db,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await PurgeUserDependenciesAsync(db, userId, cancellationToken);

        var user = await db.Set<ApplicationUser>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return IdentityResult.Success;

        return await userManager.DeleteAsync(user);
    }
}

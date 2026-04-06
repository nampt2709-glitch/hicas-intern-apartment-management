using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data;

// Xóa cứng các bản ghi phụ thuộc trước khi gọi <see cref="UserManager{TUser}.DeleteAsync"/> (FK Restrict trên phản hồi / refresh token).
public static class UserAccountPurge
{
    // Gỡ refresh token, xử lý cây phản hồi, gỡ liên kết cư dân theo thứ tự an toàn với FK.
    public static async Task PurgeUserDependenciesAsync(ApartmentDbContext db, Guid userId, CancellationToken cancellationToken = default)
    {
        // Bước 1: xóa toàn bộ refresh token của user (bỏ query filter soft-delete nếu cần).
        var refresh = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        if (refresh.Count > 0)
        {
            db.RefreshTokens.RemoveRange(refresh);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Bước 2: lấy Id phản hồi do user tạo; xử lý reply trỏ tới phản hồi sắp xóa (set ParentFeedbackId null).
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

            // Bước 3: xóa phản hồi của user từ lá lên gốc (tránh vi phạm FK self-reference).
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

        // Bước 4: gỡ UserId khỏi bản ghi cư dân (giữ cư dân nhưng không còn liên kết tài khoản).
        var residents = await db.Residents.IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var r in residents)
            r.UserId = null;

        if (residents.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    // gọi PurgeUserDependenciesAsync rồi xóa cứng ApplicationUser qua UserManager.
    public static async Task<IdentityResult> DeleteUserHardAsync(
        UserManager<ApplicationUser> userManager,
        ApartmentDbContext db,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await PurgeUserDependenciesAsync(db, userId, cancellationToken);

        // Tải user kể cả soft-deleted; không có thì coi như thành công.
        var user = await db.Set<ApplicationUser>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return IdentityResult.Success;

        return await userManager.DeleteAsync(user);
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Enums;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.DataSeed;

/// <summary>Inserts ~1000 realistic rows across all domain tables + Identity, and removes them in FK-safe order.</summary>
public static class SeedData
{
    /// <summary>Tag stored in domain <c>CreatedBy</c> for seeded rows. String value kept stable for existing databases.</summary>
    public const string SeedDataTag = "bulk-demo-seed";

    // Row budget (sum = 1000): users 40 + userRoles 40 + utilities 5 + apartments 150 + images 300 + residents 150 + invoices 100 + details 200 + feedbacks 10 + refresh 5
    private const int UserCount = 40;
    private const int AdminCount = 5;
    private const int UtilityCount = 5;
    private const int ApartmentCount = 150;
    private const int ImagesTotal = 300;
    private const int ResidentCount = 150;
    private const int InvoiceCount = 100;
    private const int InvoiceDetailsTotal = 200;
    private const int FeedbackCount = 10;
    private const int RefreshTokenCount = 5;

    private const int CanonicalAdminSlots = 2;
    private const int CanonicalUserSlots = 6;

    /// <summary>Postman / local dev defaults; override with SEED_CANONICAL_ADMIN_PASSWORD / SEED_CANONICAL_USER_PASSWORD.</summary>
    private static string CanonicalAdminPassword =>
        Environment.GetEnvironmentVariable("SEED_CANONICAL_ADMIN_PASSWORD")?.Trim() is { Length: > 0 } p
            ? p
            : "Admin@12345";

    private static string CanonicalUserPassword =>
        Environment.GetEnvironmentVariable("SEED_CANONICAL_USER_PASSWORD")?.Trim() is { Length: > 0 } p
            ? p
            : "User@12345";

    private sealed record CanonicalSeedAccount(string Label, string Email, string Password, string RoleName, string FullName);

    private sealed record K6CredentialRow(string Label, string Email, string Password);

    /// <summary>Builds admin1..admin2 and user1..user6 emails/roles/passwords from patterns (not a flat hardcoded list of 8 objects).</summary>
    private static IReadOnlyList<CanonicalSeedAccount> BuildCanonicalSeedAccounts()
    {
        static string UserOrdinal(int n) => n switch
        {
            1 => "One",
            2 => "Two",
            3 => "Three",
            4 => "Four",
            5 => "Five",
            6 => "Six",
            _ => n.ToString(CultureInfo.InvariantCulture)
        };

        var list = new List<CanonicalSeedAccount>(CanonicalAdminSlots + CanonicalUserSlots);
        for (var i = 1; i <= CanonicalAdminSlots; i++)
        {
            var email = string.Format(CultureInfo.InvariantCulture, "admin{0}@apartment.local", i);
            var full = i == 1 ? "Administrator One" : "Administrator Two";
            list.Add(new CanonicalSeedAccount(
                string.Format(CultureInfo.InvariantCulture, "admin{0}", i),
                email,
                CanonicalAdminPassword,
                "Admin",
                full));
        }

        for (var i = 1; i <= CanonicalUserSlots; i++)
        {
            var email = string.Format(CultureInfo.InvariantCulture, "user{0}@apartment.local", i);
            list.Add(new CanonicalSeedAccount(
                string.Format(CultureInfo.InvariantCulture, "user{0}", i),
                email,
                CanonicalUserPassword,
                "User",
                string.Format(CultureInfo.InvariantCulture, "User {0}", UserOrdinal(i))));
        }

        return list;
    }

    private static async Task<IReadOnlyList<K6CredentialRow>> EnsureCanonicalDemoAccountsAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var rows = new List<K6CredentialRow>(CanonicalAdminSlots + CanonicalUserSlots);
        foreach (var spec in BuildCanonicalSeedAccounts())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await userManager.FindByEmailAsync(spec.Email);
            if (existing is not null)
            {
                logger.LogInformation("Canonical seed user already exists ({Email}), skipping create.", spec.Email);
                rows.Add(new K6CredentialRow(spec.Label, spec.Email, spec.Password));
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = spec.Email,
                Email = spec.Email,
                EmailConfirmed = true,
                FullName = spec.FullName,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedDataTag
            };

            var result = await userManager.CreateAsync(user, spec.Password);
            if (!result.Succeeded)
            {
                logger.LogError("Canonical user {Email} create failed: {Err}", spec.Email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            var roleAdd = await userManager.AddToRoleAsync(user, spec.RoleName);
            if (!roleAdd.Succeeded)
            {
                logger.LogError("Canonical user {Email} role {Role} failed: {Err}", spec.Email, spec.RoleName,
                    string.Join("; ", roleAdd.Errors.Select(e => e.Description)));
            }

            rows.Add(new K6CredentialRow(spec.Label, spec.Email, spec.Password));
        }

        return rows;
    }

    public static async Task InsertAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApartmentDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedData));

        if (await db.Apartments.IgnoreQueryFilters().AnyAsync(a => a.CreatedBy == SeedDataTag, cancellationToken))
        {
            logger.LogInformation("Seed data already present (tag={Tag}). Skipping insert.", SeedDataTag);
            return;
        }

        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var canonicalK6Rows = await EnsureCanonicalDemoAccountsAsync(userManager, logger, cancellationToken);
        if (canonicalK6Rows.Count < CanonicalAdminSlots + CanonicalUserSlots)
            logger.LogWarning(
                "Expected {Expected} canonical @apartment.local accounts; {Actual} ready for k6 credentials file.",
                CanonicalAdminSlots + CanonicalUserSlots, canonicalK6Rows.Count);

        var sharedPassword = GenerateSeedPassword();
        var createdUsers = new List<ApplicationUser>(UserCount);
        for (var i = 0; i < UserCount; i++)
        {
            var email = $"demoblk{i:D2}@seed.local";
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = i < AdminCount ? $"Admin Demo {i + 1}" : $"Resident Demo {i + 1}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedDataTag
            };
            var result = await userManager.CreateAsync(user, sharedPassword);
            if (!result.Succeeded)
            {
                logger.LogError("Create user {Email} failed: {Err}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await userManager.AddToRoleAsync(user, i < AdminCount ? "Admin" : "User");
            createdUsers.Add(user);
        }

        if (createdUsers.Count == 0)
            throw new InvalidOperationException("No users were created; aborting seed.");

        WriteCredentialsFile(canonicalK6Rows, createdUsers, sharedPassword, logger);

        var utc = DateTime.UtcNow;
        var utilityIds = new Guid[UtilityCount];
        var utilityNames = new[] { "Electricity", "Cold water", "Internet fiber", "Parking slot", "Building management" };
        for (var i = 0; i < UtilityCount; i++)
        {
            utilityIds[i] = Guid.NewGuid();
            db.UtilityServices.Add(new UtilityService
            {
                Id = utilityIds[i],
                ServiceName = utilityNames[i],
                Unit = i == 0 ? "kWh" : i == 1 ? "m3" : i == 2 ? "month" : i == 3 ? "slot" : "m2",
                UnitPrice = 1000m * (i + 1),
                IsActive = true,
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var apartmentIds = new Guid[ApartmentCount];
        for (var i = 0; i < ApartmentCount; i++)
        {
            apartmentIds[i] = Guid.NewGuid();
            var floor = i % 20 + 1;
            db.Apartments.Add(new Apartment
            {
                Id = apartmentIds[i],
                ApartmentNumber = $"BLK-{i + 1:D4}",
                Floor = floor,
                Area = Math.Round(42m + i * 0.37m, 2),
                Status = (ApartmentStatus)(i % 3),
                Description = $"Demo apartment unit {i + 1}",
                CoverImagePath = $"/uploads/demo/{apartmentIds[i]:N}/cover.jpg",
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var imgIx = 0;
        while (imgIx < ImagesTotal)
        {
            var aptIndex = imgIx / 2;
            if (aptIndex >= ApartmentCount)
                break;
            db.ApartmentImages.Add(new ApartmentImage
            {
                Id = Guid.NewGuid(),
                ApartmentId = apartmentIds[aptIndex],
                FilePath = $"/uploads/demo/{apartmentIds[aptIndex]:N}/img-{imgIx}.jpg",
                OriginalFileName = $"img-{imgIx}.jpg",
                MimeType = "image/jpeg",
                SortOrder = imgIx % 2 + 1,
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
            imgIx++;
        }

        await db.SaveChangesAsync(cancellationToken);

        for (var r = 0; r < ResidentCount; r++)
        {
            Guid? uid = r < createdUsers.Count ? createdUsers[r].Id : null;
            var phone = $"+84901{(200000 + r):D6}";
            db.Residents.Add(new Resident
            {
                Id = Guid.NewGuid(),
                ApartmentId = apartmentIds[r % ApartmentCount],
                UserId = uid,
                FullName = uid.HasValue ? createdUsers[r].FullName : $"Guest resident {r}",
                PhoneNumber = phone,
                Email = uid.HasValue ? createdUsers[r].Email : $"guest{r}@seed.local",
                IsOwner = r % 3 == 0,
                MoveInDate = utc.AddMonths(-(r % 36) - 1),
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var invoiceIds = new Guid[InvoiceCount];
        for (var inv = 0; inv < InvoiceCount; inv++)
        {
            invoiceIds[inv] = Guid.NewGuid();
            var bm = DateOnly.FromDateTime(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(inv));
            var issue = new DateTime(bm.Year, bm.Month, 5, 0, 0, 0, DateTimeKind.Utc);
            db.Invoices.Add(new Invoice
            {
                Id = invoiceIds[inv],
                ApartmentId = apartmentIds[inv % ApartmentCount],
                Title = $"Invoice {bm:yyyy-MM} — unit {(inv % ApartmentCount) + 1}",
                BillingMonth = bm,
                IssueDate = issue,
                DueDate = issue.AddDays(14),
                TotalAmount = 0,
                IsPaid = inv % 5 == 0,
                PaidAt = inv % 5 == 0 ? issue.AddDays(2) : null,
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var detailIx = 0;
        for (var inv = 0; inv < InvoiceCount; inv++)
        {
            for (var line = 0; line < 2 && detailIx < InvoiceDetailsTotal; line++)
            {
                var svcId = utilityIds[detailIx % UtilityCount];
                var qty = 10m + detailIx % 50;
                var unitP = 2000m + detailIx * 10m;
                var sub = qty * unitP;
                db.InvoiceDetails.Add(new InvoiceDetail
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceIds[inv],
                    UtilityServiceId = svcId,
                    Quantity = qty,
                    UnitPrice = unitP,
                    SubTotal = sub,
                    Note = $"Line {detailIx}",
                    CreatedAt = utc,
                    CreatedBy = SeedDataTag
                });
                detailIx++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var invEntity in await db.Invoices.IgnoreQueryFilters().Where(i => i.CreatedBy == SeedDataTag).ToListAsync(cancellationToken))
        {
            invEntity.TotalAmount = await db.InvoiceDetails.IgnoreQueryFilters()
                .Where(d => d.InvoiceId == invEntity.Id)
                .SumAsync(d => d.SubTotal, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var feedbackUserIds = createdUsers.Take(Math.Min(4, createdUsers.Count)).Select(u => u.Id).ToArray();
        if (feedbackUserIds.Length == 0)
            feedbackUserIds = createdUsers.Select(u => u.Id).Take(1).ToArray();
        var rootFeedbackIds = new Guid[4];
        for (var f = 0; f < FeedbackCount; f++)
        {
            var id = Guid.NewGuid();
            Guid? parentId = null;
            string path;
            if (f < 4)
            {
                rootFeedbackIds[f] = id;
                path = $"/{id:N}";
            }
            else
            {
                var root = rootFeedbackIds[(f - 4) % 4];
                parentId = root;
                path = $"/{root:N}/{id:N}";
            }

            db.Feedbacks.Add(new Feedback
            {
                Id = id,
                UserId = feedbackUserIds[f % feedbackUserIds.Length],
                Content = f < 4
                    ? $"Building notice thread {f + 1}"
                    : $"Reply regarding notice {(f - 4) % 4 + 1}",
                ReferenceApartmentId = f % 2 == 0 ? apartmentIds[f % ApartmentCount] : null,
                ReferenceInvoiceId = f % 3 == 0 ? invoiceIds[f % InvoiceCount] : null,
                ParentFeedbackId = parentId,
                Path = path,
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        for (var t = 0; t < RefreshTokenCount && t < createdUsers.Count; t++)
        {
            var raw = RandomNumberGenerator.GetBytes(32);
            var tokenKey = Convert.ToHexString(raw).ToLowerInvariant();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = createdUsers[t].Id,
                TokenHash = Sha256Hex(tokenKey),
                JwtId = Guid.NewGuid().ToString("N"),
                ExpiresAt = utc.AddDays(30),
                CreatedAt = utc,
                CreatedBy = SeedDataTag
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed data completed: {BulkUsers} bulk users + canonical @apartment.local accounts, {Apt} apartments, {Img} images, {Res} residents, {Inv} invoices, {Det} details, {Fb} feedbacks, {Rt} refresh tokens (tag={Tag}).",
            createdUsers.Count, ApartmentCount, ImagesTotal, ResidentCount, InvoiceCount, InvoiceDetailsTotal, FeedbackCount, RefreshTokenCount, SeedDataTag);
    }

    private static void WriteCredentialsFile(
        IReadOnlyList<K6CredentialRow> canonicalK6Users,
        IReadOnlyList<ApplicationUser> bulkUsers,
        string bulkSharedPassword,
        ILogger logger)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "seed-credentials.generated.json");

        var k6Users = canonicalK6Users.Count > 0
            ? canonicalK6Users.Select(k => new { label = k.Label, email = k.Email, password = k.Password }).ToArray()
            :
            [
                new { label = "admin1", email = bulkUsers[0].Email!, password = bulkSharedPassword },
                new { label = "admin2", email = bulkUsers[1].Email!, password = bulkSharedPassword },
                new { label = "user1", email = bulkUsers[5].Email!, password = bulkSharedPassword },
                new { label = "user2", email = bulkUsers[6].Email!, password = bulkSharedPassword },
                new { label = "user3", email = bulkUsers[7].Email!, password = bulkSharedPassword },
                new { label = "user4", email = bulkUsers[8].Email!, password = bulkSharedPassword },
                new { label = "user5", email = bulkUsers[9].Email!, password = bulkSharedPassword },
                new { label = "user6", email = bulkUsers[10].Email!, password = bulkSharedPassword }
            ];

        var sampleEmail = canonicalK6Users.Count > 0 ? canonicalK6Users[0].Email : bulkUsers[0].Email!;
        var samplePassword = canonicalK6Users.Count > 0 ? canonicalK6Users[0].Password : bulkSharedPassword;

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            note =
                "k6Users: Postman-style admin1..admin2, user1..user6 @ apartment.local (passwords Admin@12345 / User@12345 unless overridden by env). " +
                "Bulk demoblk*@seed.local users share bulkSharedPassword.",
            sampleLogin = new { email = sampleEmail, password = samplePassword },
            k6Users,
            bulkSharedPassword,
            bulkSampleLogin = new { email = bulkUsers[0].Email!, password = bulkSharedPassword }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        logger.LogWarning("Wrote {Path} (do not commit).", path);
    }

    private static string GenerateSeedPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digit = "23456789";
        const string special = "@#$%&*!";
        var all = upper + lower + digit + special;
        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digit), Pick(special) };
        while (chars.Count < 16)
            chars.Add(Pick(all));
        Shuffle(chars);
        return new string(chars.ToArray());

        char Pick(string set)
        {
            var b = new byte[4];
            RandomNumberGenerator.Fill(b);
            return set[(int)(BitConverter.ToUInt32(b, 0) % (uint)set.Length)];
        }

        void Shuffle(List<char> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var b = new byte[4];
                RandomNumberGenerator.Fill(b);
                var j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    private static string Sha256Hex(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Removes tagged domain rows only (no Identity). For unit tests.</summary>
    public static async Task CleanupTaggedDomainOnlyAsync(ApartmentDbContext db, CancellationToken cancellationToken = default)
    {
        await UnlinkParentLinksToTaggedFeedbacksAsync(db, cancellationToken);

        while (true)
        {
            var taggedRows = await db.Feedbacks.IgnoreQueryFilters()
                .Where(x => x.CreatedBy == SeedDataTag)
                .ToListAsync(cancellationToken);
            if (taggedRows.Count == 0)
                break;

            var parentIdsReferenced = (await db.Feedbacks.IgnoreQueryFilters()
                    .Where(x => x.ParentFeedbackId != null)
                    .Select(x => x.ParentFeedbackId!.Value)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var leaves = taggedRows.Where(r => !parentIdsReferenced.Contains(r.Id)).ToList();
            if (leaves.Count == 0)
                break;

            db.Feedbacks.RemoveRange(leaves);
            await db.SaveChangesAsync(cancellationToken);
        }

        await DeleteTaggedAsync<InvoiceDetail>(db, cancellationToken);
        await DeleteTaggedAsync<RefreshToken>(db, cancellationToken);
        await DeleteTaggedAsync<ApartmentImage>(db, cancellationToken);
        await DeleteTaggedAsync<Resident>(db, cancellationToken);
        await DeleteTaggedAsync<Invoice>(db, cancellationToken);
        await DeleteTaggedAsync<UtilityService>(db, cancellationToken);
        await DeleteTaggedAsync<Apartment>(db, cancellationToken);
    }

    /// <summary>Removes all rows created with <see cref="SeedDataTag"/> (FK-safe). Does not touch unrelated rows.</summary>
    public static async Task DeleteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApartmentDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedData));

        await CleanupTaggedDomainOnlyAsync(db, cancellationToken);

        var seedUserIds = await userManager.Users.IgnoreQueryFilters()
            .Where(u => u.CreatedBy == SeedDataTag)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var uid in seedUserIds)
        {
            var del = await UserAccountPurge.DeleteUserHardAsync(userManager, db, uid, cancellationToken);
            if (!del.Succeeded)
                logger.LogError("Failed to delete seed user {Id}: {Err}", uid, string.Join("; ", del.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("Seed data removed (tag={Tag}).", SeedDataTag);
    }

    private static async Task DeleteTaggedAsync<TEntity>(ApartmentDbContext db, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var rows = await db.Set<TEntity>().IgnoreQueryFilters()
            .Where(x => x.CreatedBy == SeedDataTag)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return;
        db.Set<TEntity>().RemoveRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UnlinkParentLinksToTaggedFeedbacksAsync(ApartmentDbContext db, CancellationToken cancellationToken)
    {
        var taggedParentIds = await db.Feedbacks.IgnoreQueryFilters()
            .Where(p => p.CreatedBy == SeedDataTag)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (taggedParentIds.Count == 0)
            return;

        var tagSet = taggedParentIds.ToHashSet();
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
    }
}

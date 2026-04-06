using ApartmentManagement.API.V1.Entities.Abstractions;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.API.V1.Entities.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data;

// DbContext trung tâm: kế thừa Identity (user/role) + các bảng nghiệp vụ (căn hộ, hóa đơn, phản hồi...).
// Luồng: OnModelCreating → cấu hình Identity → miền nghiệp vụ → bộ lọc soft-delete toàn cục.
public class ApartmentDbContext : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>
{
    public ApartmentDbContext(DbContextOptions<ApartmentDbContext> options) : base(options)
    {
    }

    // DbSet: truy cập tập thực thể được map sang bảng SQL Server.
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<ApartmentImage> ApartmentImages => Set<ApartmentImage>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<UtilityService> UtilityServices => Set<UtilityService>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // cấu hình bảng Identity (ApplicationUser, RefreshToken) và quan hệ FK.
        ConfigureIdentity(builder);
        // cấu hình bảng miền (Apartment, Invoice, Feedback...) — chỉ mục, độ dài, precision.
        ConfigureDomain(builder);
        // áp query filter IsDeleted=false cho mọi entity ISoftDeletable.
        ApplySoftDeleteFilters(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        // mapping ApplicationUser — mở rộng thuộc tính, quan hệ 1-n với RefreshToken và Feedback.
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AvatarPath).HasMaxLength(500);
            entity.HasIndex(x => x.Email).IsUnique(false);
            entity.HasMany(x => x.RefreshTokens)
                  .WithOne(x => x.User)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AuthoredFeedbacks)
                  .WithOne(x => x.User)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // mapping RefreshToken — hash token, unique index, FK tới User.
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.JwtId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.Property(x => x.UpdatedBy).HasMaxLength(200);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
        });
    }

    private static void ConfigureDomain(ModelBuilder builder)
    {
        // Căn hộ — số căn unique, quan hệ ảnh, cư dân, hóa đơn, phản hồi tham chiếu.
        builder.Entity<Apartment>(entity =>
        {
            entity.ToTable("Apartments");
            entity.Property(x => x.ApartmentNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Area).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CoverImagePath).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

            entity.HasIndex(x => x.ApartmentNumber).IsUnique();

            entity.HasMany(x => x.Images)
                  .WithOne(x => x.Apartment)
                  .HasForeignKey(x => x.ApartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Residents)
                  .WithOne(x => x.Apartment)
                  .HasForeignKey(x => x.ApartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Invoices)
                  .WithOne(x => x.Apartment)
                  .HasForeignKey(x => x.ApartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Feedbacks)
                  .WithOne(x => x.ReferenceApartment)
                  .HasForeignKey(x => x.ReferenceApartmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Ảnh căn hộ — unique (ApartmentId, SortOrder).
        builder.Entity<ApartmentImage>(entity =>
        {
            entity.ToTable("ApartmentImages");
            entity.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.MimeType).HasMaxLength(120).IsRequired();

            entity.HasIndex(x => new { x.ApartmentId, x.SortOrder }).IsUnique();
        });

        // Cư dân — liên kết tài khoản (0-1) hoặc chỉ SĐT; ràng buộc unique theo quy tắc filter.
        builder.Entity<Resident>(entity =>
        {
            entity.ToTable("Residents");
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.PhoneNumber);

            entity.HasOne(x => x.Account)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
            entity.HasIndex(x => new { x.ApartmentId, x.PhoneNumber }).IsUnique().HasFilter("[UserId] IS NULL");
        });

        // Dịch vụ tiện ích (đơn giá, đơn vị) — tên dịch vụ unique.
        builder.Entity<UtilityService>(entity =>
        {
            entity.ToTable("UtilityServices");
            entity.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.ServiceName).IsUnique();
        });

        // Hóa đơn theo căn hộ + tháng thanh toán — unique (ApartmentId, BillingMonth).
        builder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);

            entity.HasIndex(x => new { x.ApartmentId, x.BillingMonth }).IsUnique();

            entity.HasMany(x => x.InvoiceDetails)
                  .WithOne(x => x.Invoice)
                  .HasForeignKey(x => x.InvoiceId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Feedbacks)
                  .WithOne(x => x.ReferenceInvoice)
                  .HasForeignKey(x => x.ReferenceInvoiceId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Chi tiết hóa đơn — mỗi dịch vụ một dòng trên hóa đơn (unique theo cặp Invoice+Utility).
        builder.Entity<InvoiceDetail>(entity =>
        {
            entity.ToTable("InvoiceDetails");
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(500);

            entity.HasIndex(x => new { x.InvoiceId, x.UtilityServiceId }).IsUnique();

            entity.HasOne(x => x.UtilityService)
                  .WithMany()
                  .HasForeignKey(x => x.UtilityServiceId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Phản hồi dạng cây (materialized path), tham chiếu căn hộ/hóa đơn tùy chọn.
        builder.Entity<Feedback>(entity =>
        {
            entity.ToTable("Feedbacks");
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(2000).IsRequired();

            entity.HasIndex(x => x.Path).IsUnique();
            entity.HasIndex(x => x.ParentFeedbackId);
            entity.HasIndex(x => x.ReferenceApartmentId);
            entity.HasIndex(x => x.ReferenceInvoiceId);
            entity.HasIndex(x => x.UserId);

            entity.HasOne(x => x.ParentFeedback)
                  .WithMany(x => x.Replies)
                  .HasForeignKey(x => x.ParentFeedbackId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        // Duyệt mọi CLR type trong model; nếu implement ISoftDeletable thì gọi generic ApplySoftDeleteFilter.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(ApartmentDbContext)
                .GetMethod(nameof(ApplySoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var genericMethod = method.MakeGenericMethod(entityType.ClrType);
            genericMethod.Invoke(null, new object[] { builder });
        }
    }

    // Áp bộ lọc truy vấn mặc định: ẩn bản ghi đã xóa mềm (IsDeleted=true).
    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : class, ISoftDeletable
        => builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
}

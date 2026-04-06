// Cấu hình AutoMapper: ánh xạ entity ↔ DTO cho API v1 (bỏ qua trường audit/soft-delete qua IgnoreBaseEntityMembers).
using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.DTOs.Invoices;
using ApartmentManagement.API.V1.DTOs.Residents;
using ApartmentManagement.API.V1.DTOs.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Security;
using AutoMapper;

namespace ApartmentManagement.API.V1.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Căn hộ: đếm cư dân/hóa đơn khi đọc; tạo/cập nhật không map quan hệ con.
        CreateMap<Apartment, ApartmentReadDto>()
            .ForMember(d => d.ResidentCount, opt => opt.MapFrom(s => s.Residents.Count))
            .ForMember(d => d.InvoiceCount, opt => opt.MapFrom(s => s.Invoices.Count));

        CreateMap<ApartmentCreateDto, Apartment>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.CoverImagePath, opt => opt.Ignore())
            .ForMember(d => d.Images, opt => opt.Ignore())
            .ForMember(d => d.Residents, opt => opt.Ignore())
            .ForMember(d => d.Invoices, opt => opt.Ignore())
            .ForMember(d => d.Feedbacks, opt => opt.Ignore());

        CreateMap<ApartmentUpdateDto, Apartment>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.CoverImagePath, opt => opt.Ignore())
            .ForMember(d => d.Images, opt => opt.Ignore())
            .ForMember(d => d.Residents, opt => opt.Ignore())
            .ForMember(d => d.Invoices, opt => opt.Ignore())
            .ForMember(d => d.Feedbacks, opt => opt.Ignore());

        CreateMap<ApartmentImage, ApartmentImageReadDto>();

        // Cư dân
        CreateMap<Resident, ResidentReadDto>();

        CreateMap<ResidentCreateDto, Resident>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.Apartment, opt => opt.Ignore())
            .ForMember(d => d.Account, opt => opt.Ignore())
            .ForMember(d => d.MoveOutDate, opt => opt.Ignore());

        CreateMap<ResidentUpdateDto, Resident>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.Apartment, opt => opt.Ignore())
            .ForMember(d => d.Account, opt => opt.Ignore());

        CreateMap<UtilityService, UtilityServiceReadDto>();

        // Dịch vụ tiện ích
        CreateMap<UtilityServiceCreateDto, UtilityService>()
            .IgnoreBaseEntityMembers();

        CreateMap<UtilityServiceUpdateDto, UtilityService>()
            .IgnoreBaseEntityMembers();

        CreateMap<InvoiceDetail, InvoiceDetailReadDto>()
            .ForMember(d => d.ServiceName, opt => opt.MapFrom(s => s.UtilityService.ServiceName));

        // Hóa đơn và dòng chi tiết (SubTotal thường do service tính)
        CreateMap<InvoiceDetailCreateDto, InvoiceDetail>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.InvoiceId, opt => opt.Ignore())
            .ForMember(d => d.Invoice, opt => opt.Ignore())
            .ForMember(d => d.UtilityService, opt => opt.Ignore())
            .ForMember(d => d.SubTotal, opt => opt.Ignore());

        CreateMap<Invoice, InvoiceReadDto>()
            .ForMember(d => d.ApartmentNumber, opt => opt.MapFrom(s => s.Apartment.ApartmentNumber))
            .ForMember(d => d.Details, opt => opt.MapFrom(s => s.InvoiceDetails));

        CreateMap<InvoiceCreateDto, Invoice>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.Apartment, opt => opt.Ignore())
            .ForMember(d => d.InvoiceDetails, opt => opt.Ignore())
            .ForMember(d => d.Feedbacks, opt => opt.Ignore())
            .ForMember(d => d.TotalAmount, opt => opt.Ignore())
            .ForMember(d => d.PaidAt, opt => opt.Ignore());

        CreateMap<InvoiceUpdateDto, Invoice>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.Apartment, opt => opt.Ignore())
            .ForMember(d => d.InvoiceDetails, opt => opt.Ignore())
            .ForMember(d => d.Feedbacks, opt => opt.Ignore())
            .ForMember(d => d.TotalAmount, opt => opt.Ignore())
            .ForMember(d => d.PaidAt, opt => opt.Ignore());

        CreateMap<Feedback, FeedbackReadDto>()
            .ForMember(
                d => d.UserName,
                opt => opt.MapFrom(s =>
                    s.User != null
                        ? (s.User.UserName ?? s.User.Email ?? string.Empty)
                        : string.Empty));

        // Phản hồi: UserId/Path do tầng service gán
        CreateMap<FeedbackCreateDto, Feedback>()
            .IgnoreBaseEntityMembers()
            .ForMember(d => d.UserId, opt => opt.Ignore())
            .ForMember(d => d.User, opt => opt.Ignore())
            .ForMember(d => d.ReferenceApartment, opt => opt.Ignore())
            .ForMember(d => d.ReferenceInvoice, opt => opt.Ignore())
            .ForMember(d => d.ParentFeedback, opt => opt.Ignore())
            .ForMember(d => d.Replies, opt => opt.Ignore())
            .ForMember(d => d.Path, opt => opt.Ignore());

        // Người dùng hiện tại (vai trò map riêng)
        CreateMap<ApplicationUser, CurrentUserDto>()
            .ForMember(d => d.UserId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.Email, opt => opt.MapFrom(s => s.Email ?? string.Empty))
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.FullName))
            .ForMember(d => d.PhoneNumber, opt => opt.MapFrom(s => s.PhoneNumber))
            .ForMember(d => d.AvatarPath, opt => opt.MapFrom(s => s.AvatarPath))
            .ForMember(d => d.Roles, opt => opt.Ignore());
    }
}

// Bỏ qua các trường BaseEntity khi map từ DTO sang entity (Id, audit, soft-delete).
internal static class MappingProfileExtensions
{
    public static IMappingExpression<TSource, TDestination> IgnoreBaseEntityMembers<TSource, TDestination>(
        this IMappingExpression<TSource, TDestination> expression)
        where TDestination : BaseEntity
    {
        return expression
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())
            .ForMember(d => d.CreatedBy, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAt, opt => opt.Ignore())
            .ForMember(d => d.UpdatedBy, opt => opt.Ignore())
            .ForMember(d => d.IsDeleted, opt => opt.Ignore())
            .ForMember(d => d.DeletedAt, opt => opt.Ignore());
    }
}

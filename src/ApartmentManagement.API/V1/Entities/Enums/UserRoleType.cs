namespace ApartmentManagement.API.V1.Entities.Enums;

// Vai trò người dùng cấp ứng dụng (ánh xạ claim/role trong Identity).
public enum UserRoleType
{
    // Quản trị viên: toàn quyền cấu hình và dữ liệu nhạy cảm.
    Admin = 0,
    // Người dùng thường: quyền hạn theo chính sách nghiệp vụ.
    User = 1
}

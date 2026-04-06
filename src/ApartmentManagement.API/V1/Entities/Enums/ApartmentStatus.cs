namespace ApartmentManagement.API.V1.Entities.Enums;

// Trạng thái vận hành của một căn hộ trong tòa nhà.
public enum ApartmentStatus
{
    // Còn trống, có thể cho thuê hoặc bán.
    Available = 0,
    // Đang có người ở / đã gán cư dân.
    Occupied = 1,
    // Đang bảo trì, sửa chữa — tạm không dùng cho nghiệp vụ cho thuê bình thường.
    Maintenance = 2
}

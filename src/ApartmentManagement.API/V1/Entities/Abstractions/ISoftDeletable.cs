namespace ApartmentManagement.API.V1.Entities.Abstractions;

// Hợp đồng đánh dấu bản ghi bị xoá mềm (ẩn khỏi truy vấn mặc định thay vì xoá vật lý).
public interface ISoftDeletable
{
    // True khi bản ghi được đánh dấu đã xoá.
    bool IsDeleted { get; set; }
    // Thời điểm thực hiện xoá mềm (null nếu chưa xoá).
    DateTime? DeletedAt { get; set; }
}

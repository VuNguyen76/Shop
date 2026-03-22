using ClothingShop.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime OrderDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Cancelled
    
    // Thông tin giao hàng
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    
    // Lý do hủy đơn (nếu có)
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; } // "Admin" hoặc "Customer"

    public List<OrderItem> Items { get; set; } = new();
}
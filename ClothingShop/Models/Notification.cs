namespace ClothingShop.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // info, success, warning, danger
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Liên kết đến đơn hàng (nếu có)
        public int? OrderId { get; set; }
        
        // Liên kết đến support ticket (nếu có)
        public int? SupportTicketId { get; set; }
    }
}

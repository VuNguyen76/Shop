namespace ClothingShop.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Subject { get; set; } = null!;
        public string Status { get; set; } = "Mở"; // Mở, Đang xử lý, Đã đóng
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation
        public User? User { get; set; }
        public List<SupportMessage> Messages { get; set; } = new();
    }
    
    public class SupportMessage
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public int SenderId { get; set; }
        public bool IsAdmin { get; set; }
        public string Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        
        // Navigation
        public SupportTicket? Ticket { get; set; }
        public User? Sender { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace ClothingShop.Models
{
    // Model cho lịch sử xem sản phẩm
    public class ProductView
    {
        public int Id { get; set; }
        
        public int? UserId { get; set; } // Null nếu chưa đăng nhập (dùng session)
        
        [Required]
        public int ProductId { get; set; }
        
        [MaxLength(50)]
        public string? SessionId { get; set; } // Dùng cho khách chưa đăng nhập
        
        public DateTime ViewedAt { get; set; } = DateTime.Now;
        
        // Navigation properties
        public User? User { get; set; }
        public Product? Product { get; set; }
    }
}

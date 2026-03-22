using System.ComponentModel.DataAnnotations;

namespace ClothingShop.Models
{
    // Model cho quản lý nhập/xuất kho
    public class InventoryTransaction
    {
        public int Id { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = null!; // "Nhập" hoặc "Xuất"
        
        [Required]
        public int Quantity { get; set; }
        
        [MaxLength(500)]
        public string? Reason { get; set; }
        
        [MaxLength(100)]
        public string? Supplier { get; set; } // Nhà cung cấp (nếu là nhập hàng)
        
        public decimal? Cost { get; set; } // Giá nhập (nếu là nhập hàng)
        
        public int? OrderId { get; set; } // ID đơn hàng (nếu là xuất hàng do bán)
        
        public int CreatedBy { get; set; } // Admin thực hiện
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Navigation properties
        public Product? Product { get; set; }
        public User? Creator { get; set; }
        public Order? Order { get; set; }
    }
}

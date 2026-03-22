// Models/Product.cs
namespace ClothingShop.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal? Cost { get; set; } // Giá nhập (tùy chọn)
        public string? ImageUrl { get; set; }
        
        // ẢNH AVIF (tối ưu hiệu suất)
        public string? ImageUrlAvif { get; set; }
        
        // ẢNH PHỤ (lưu dạng JSON string)
        public string? AdditionalImages { get; set; }
        
        // ẢNH PHỤ AVIF (lưu dạng JSON string)
        public string? AdditionalImagesAvif { get; set; }
        
        // THÊM CÁC TRƯỜNG LỌC
        public string Category { get; set; } = "Áo"; // Áo, Quần, Giày, Phụ kiện
        public string Gender { get; set; } = "Nam"; // Nam, Nữ, Unisex
        
        // SIZE VÀ MÀU (lưu dạng JSON string - có thể chọn nhiều)
        public string Size { get; set; } = "M";     // JSON: ["S", "M", "L"]
        public string Color { get; set; } = "Đen";  // JSON: ["Đen", "Trắng"]
        
        public int Quantity { get; set; } = 100;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false; // Soft delete
        public DateTime? DeletedAt { get; set; } // Thời gian xóa
        public bool IsNew => CreatedAt > DateTime.Now.AddDays(-7);
    }
}
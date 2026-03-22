using System.ComponentModel.DataAnnotations;

namespace ClothingShop.Models
{
    public class FashionCategory
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // URL ảnh AVIF (tùy chọn, để tối ưu hiệu suất)
        [StringLength(500)]
        public string? ImageUrlAvif { get; set; }

        [StringLength(200)]
        public string? LinkUrl { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

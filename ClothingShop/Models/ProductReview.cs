namespace ClothingShop.Models
{
    public class ProductReview
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public int OrderId { get; set; }
        public int Rating { get; set; } // 1-5 sao
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public Product? Product { get; set; }
        public User? User { get; set; }
        public Order? Order { get; set; }
    }
}

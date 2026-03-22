namespace ClothingShop.Models
{
    public class Cart
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public DateTime AddedDate { get; set; }
        
        // Navigation properties
        public User? User { get; set; }
        public Product? Product { get; set; }
    }
}

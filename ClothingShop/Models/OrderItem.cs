namespace ClothingShop.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal Price { get; set; } // Giá bán
    public decimal? Cost { get; set; } // Giá nhập (để tính lợi nhuận)
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
}
using ClothingShop.Models;

namespace ClothingShop.Services
{
    public interface ICartService
    {
        List<CartItem> GetCartItems();
        void AddToCart(int productId, int quantity = 1, string? selectedSize = null, string? selectedColor = null);
        void UpdateQuantity(int productId, int quantity, string? size = null, string? color = null);
        void RemoveFromCart(int productId, string? size = null, string? color = null);
        void ClearCart();
        int GetTotalItems();
        decimal GetTotalPrice();
    }
}
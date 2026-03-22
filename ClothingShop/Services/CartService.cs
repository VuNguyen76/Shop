using ClothingShop.Models; 
using ClothingShop.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Services
{
    public class CartService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context) : ICartService
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ApplicationDbContext _context = context;

        private int? GetUserId()
        {
            var userIdString = _httpContextAccessor.HttpContext?.Session.GetString("UserId");
            return string.IsNullOrEmpty(userIdString) ? null : int.Parse(userIdString);
        }

        // 1. BẮT BUỘC PHẢI CÓ
        public List<CartItem> GetCartItems()
        {
            var userId = GetUserId();
            if (userId == null) return [];

            var cartItems = _context.Carts
                .Where(c => c.UserId == userId.Value)
                .Include(c => c.Product)
                .Select(c => new CartItem
                {
                    ProductId = c.ProductId,
                    Name = c.Product!.Name,
                    Price = c.Product.Price,
                    ImageUrl = c.Product.ImageUrl,
                    Quantity = c.Quantity,
                    Size = c.Size,
                    Color = c.Color
                })
                .ToList();

            return cartItems;
        }

        public void AddToCart(int productId, int quantity = 1, string? selectedSize = null, string? selectedColor = null)
        {
            var userId = GetUserId();
            if (userId == null) return;

            var product = _context.Products.Find(productId);
            if (product == null || product.Quantity < quantity) return;

            // Tìm cart item hiện có
            var existingCart = _context.Carts
                .FirstOrDefault(c => c.UserId == userId.Value 
                    && c.ProductId == productId 
                    && c.Size == selectedSize 
                    && c.Color == selectedColor);

            if (existingCart != null)
            {
                existingCart.Quantity += quantity;
            }
            else
            {
                var newCart = new Cart
                {
                    UserId = userId.Value,
                    ProductId = productId,
                    Quantity = quantity,
                    Size = selectedSize,
                    Color = selectedColor,
                    AddedDate = DateTime.Now
                };
                _context.Carts.Add(newCart);
            }
            _context.SaveChanges();
        }

        public void UpdateQuantity(int productId, int quantity, string? size = null, string? color = null)
        {
            if (quantity <= 0) return;
            
            var userId = GetUserId();
            if (userId == null) return;

            var normalizedSize = size == "N/A" ? null : size;
            var normalizedColor = color == "N/A" ? null : color;
            
            var cartItem = _context.Carts
                .FirstOrDefault(c => c.UserId == userId.Value 
                    && c.ProductId == productId 
                    && c.Size == normalizedSize 
                    && c.Color == normalizedColor);
            
            if (cartItem != null)
            {
                cartItem.Quantity = quantity;
                _context.SaveChanges();
            }
        }

        public void RemoveFromCart(int productId, string? size = null, string? color = null)
        {
            var userId = GetUserId();
            if (userId == null) return;

            var normalizedSize = size == "N/A" ? null : size;
            var normalizedColor = color == "N/A" ? null : color;
            
            var cartItems = _context.Carts
                .Where(c => c.UserId == userId.Value 
                    && c.ProductId == productId 
                    && c.Size == normalizedSize 
                    && c.Color == normalizedColor)
                .ToList();
            
            _context.Carts.RemoveRange(cartItems);
            _context.SaveChanges();
        }

        public void ClearCart()
        {
            var userId = GetUserId();
            if (userId == null) return;

            var cartItems = _context.Carts.Where(c => c.UserId == userId.Value).ToList();
            _context.Carts.RemoveRange(cartItems);
            _context.SaveChanges();
        }

        public int GetTotalItems()
        {
            return GetCartItems().Sum(x => x.Quantity);
        }

        public decimal GetTotalPrice()
        {
            return GetCartItems().Sum(x => x.Price * x.Quantity);
        }
    }
}
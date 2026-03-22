using ClothingShop.Data;
using ClothingShop.Models;
using ClothingShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ClothingShop.Controllers
{
    public class HomeController(ApplicationDbContext context, ICartService cartService) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ICartService _cartService = cartService;

        // GET: /
        public async Task<IActionResult> Index()
        {
            // Lấy Fashion Categories
            var fashionCategories = await _context.FashionCategories
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            ViewBag.FashionCategories = fashionCategories;

            // Lấy sản phẩm bán chạy (dựa trên số lượng đã bán trong OrderItems)
            var bestSellingProducts = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Đã giao")
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalSold = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(12)
                .Join(_context.Products.Where(p => p.Quantity > 0),
                      x => x.ProductId,
                      p => p.Id,
                      (x, p) => p)
                .ToListAsync();

            // Nếu không có sản phẩm bán chạy, lấy sản phẩm mới nhất
            if (bestSellingProducts.Count == 0)
            {
                bestSellingProducts = await _context.Products
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(12)
                    .ToListAsync();
            }

            ViewBag.BestSellingProducts = bestSellingProducts;

            // CẬP NHẬT BADGE GIỎ HÀNG
            ViewBag.CartCount = _cartService.GetTotalItems();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using ClothingShop.Models;
using ClothingShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class WishlistController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // Hiển thị trang danh sách yêu thích
        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem danh sách yêu thích";
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var products = await _context.WishlistItems
                .Where(w => w.UserId == userId)
                .Include(w => w.Product)
                .Select(w => w.Product!)
                .ToListAsync();
            
            // Nếu chưa có sản phẩm yêu thích, lấy sản phẩm đề xuất
            if (products.Count == 0)
            {
                var recommendedProducts = await _context.Products
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(8)
                    .ToListAsync();
                ViewBag.RecommendedProducts = recommendedProducts;
            }
            
            return View(products);
        }

        // Thêm sản phẩm vào wishlist
        [HttpPost]
        public async Task<IActionResult> Add(int productId)
        {
            // Kiểm tra đăng nhập
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm vào yêu thích", requireLogin = true });
            }

            var userId = int.Parse(userIdString);
            
            // Kiểm tra sản phẩm đã có trong wishlist chưa
            var exists = await _context.WishlistItems
                .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
            
            if (!exists)
            {
                var wishlistItem = new WishlistItem
                {
                    UserId = userId,
                    ProductId = productId,
                    AddedDate = DateTime.Now
                };
                _context.WishlistItems.Add(wishlistItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã thêm vào yêu thích", inWishlist = true });
            }
            
            return Json(new { success = false, message = "Sản phẩm đã có trong danh sách yêu thích", inWishlist = true });
        }

        // Xóa sản phẩm khỏi wishlist
        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            // Kiểm tra đăng nhập
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập", requireLogin = true });
            }

            var userId = int.Parse(userIdString);
            
            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);
            
            if (wishlistItem != null)
            {
                _context.WishlistItems.Remove(wishlistItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa khỏi yêu thích", inWishlist = false });
            }
            
            return Json(new { success = false, message = "Sản phẩm không có trong danh sách yêu thích", inWishlist = false });
        }

        // Toggle (thêm/xóa) sản phẩm
        [HttpPost]
        public async Task<IActionResult> Toggle([FromBody] ToggleRequest request)
        {
            // Kiểm tra đăng nhập
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm vào yêu thích", requireLogin = true });
            }

            var userId = int.Parse(userIdString);
            
            var wishlistItem = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == request.ProductId);
            
            if (wishlistItem != null)
            {
                _context.WishlistItems.Remove(wishlistItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa khỏi yêu thích", inWishlist = false });
            }
            else
            {
                var newWishlistItem = new WishlistItem
                {
                    UserId = userId,
                    ProductId = request.ProductId,
                    AddedDate = DateTime.Now
                };
                _context.WishlistItems.Add(newWishlistItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã thêm vào yêu thích", inWishlist = true });
            }
        }
        
        public class ToggleRequest
        {
            public int ProductId { get; set; }
        }

        // Kiểm tra sản phẩm có trong wishlist không
        [HttpGet]
        public async Task<IActionResult> Check(int productId)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { inWishlist = false });
            }

            var userId = int.Parse(userIdString);
            var inWishlist = await _context.WishlistItems
                .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
            
            return Json(new { inWishlist });
        }

        // Lấy số lượng sản phẩm trong wishlist
        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { count = 0 });
            }

            var userId = int.Parse(userIdString);
            var count = await _context.WishlistItems
                .CountAsync(w => w.UserId == userId);
            
            return Json(new { count });
        }
    }
}

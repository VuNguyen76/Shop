// Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class ProductsController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<IActionResult> Index(
            string category = "", 
            string gender = "", 
            string size = "", 
            string color = "", 
            string search = "", 
            string sort = "newest")
        {
            // Load danh mục động cho bộ lọc
            ViewBag.Categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Name)
                .ToListAsync();
            
            var products = _context.Products.Where(p => p.Quantity > 0).AsQueryable();
            
            // Lấy rating cho tất cả sản phẩm
            var productRatings = await _context.ProductReviews
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AverageRating = g.Average(r => r.Rating),
                    TotalReviews = g.Count()
                })
                .ToListAsync();
            
            // Luôn khởi tạo ViewBag.ProductRatings (ngay cả khi rỗng)
            ViewBag.ProductRatings = productRatings.Count > 0
                ? productRatings.ToDictionary(x => x.ProductId, x => (dynamic)new { x.AverageRating, x.TotalReviews })
                : [];

            if (!string.IsNullOrEmpty(search))
                products = products.Where(p => p.Name.Contains(search));

            if (!string.IsNullOrEmpty(category))
                products = products.Where(p => p.Category == category);
            if (!string.IsNullOrEmpty(gender))
                products = products.Where(p => p.Gender == gender || p.Gender == "Unisex");
            if (!string.IsNullOrEmpty(size))
                products = products.Where(p => p.Size.Contains(size));
            if (!string.IsNullOrEmpty(color))
                products = products.Where(p => p.Color.Contains(color));

            products = sort switch
            {
                "price_asc" => products.OrderBy(p => p.Price),
                "price_desc" => products.OrderByDescending(p => p.Price),
                "name" => products.OrderBy(p => p.Name),
                _ => products.OrderByDescending(p => p.CreatedAt)
            };

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Gender = gender;
            ViewBag.Size = size;
            ViewBag.Color = color;
            ViewBag.Sort = sort;

            return View(await products.ToListAsync());
        }

        // CHI TIẾT SẢN PHẨM
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                return NotFound();
            }

            // Tự động ghi nhận lượt xem
            await TrackProductView(id);

            // Lấy đánh giá của sản phẩm
            var reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
            ViewBag.TotalReviews = reviews.Count;

            // Kiểm tra xem user đã mua sản phẩm này chưa (để hiển thị form đánh giá)
            var userIdString = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userIdString))
            {
                var userId = int.Parse(userIdString);
                
                // Lấy OrderId của đơn hàng đã giao có sản phẩm này (lấy đơn hàng mới nhất)
                var order = await _context.OrderItems
                    .Where(oi => oi.ProductId == id)
                    .Join(_context.Orders,
                          oi => oi.OrderId,
                          o => o.Id,
                          (oi, o) => new { oi, o })
                    .Where(x => x.o.UserId == userId && x.o.Status == "Đã giao")
                    .OrderByDescending(x => x.o.OrderDate)
                    .Select(x => x.o.Id)
                    .FirstOrDefaultAsync();
                
                if (order > 0)
                {
                    ViewBag.CanReview = true;
                    ViewBag.OrderId = order;
                }
            }

            // Lấy sản phẩm liên quan (cùng danh mục hoặc cùng giới tính, loại trừ sản phẩm hiện tại)
            var relatedProducts = await _context.Products
                .Where(p => p.Id != id && p.Quantity > 0 && 
                       (p.Category == product.Category || p.Gender == product.Gender))
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }

        // Hàm helper để track lượt xem sản phẩm
        private async Task TrackProductView(int productId)
        {
            try
            {
                var userIdString = HttpContext.Session.GetString("UserId");
                var sessionId = HttpContext.Session.Id;

                int? userId = null;
                if (!string.IsNullOrEmpty(userIdString))
                {
                    userId = int.Parse(userIdString);
                }

                // Kiểm tra xem đã xem trong vòng 5 phút chưa
                var recentView = await _context.ProductViews
                    .Where(pv => pv.ProductId == productId &&
                                pv.ViewedAt >= DateTime.Now.AddMinutes(-5) &&
                                (userId.HasValue ? pv.UserId == userId : pv.SessionId == sessionId))
                    .FirstOrDefaultAsync();

                if (recentView == null)
                {
                    var productView = new ProductView
                    {
                        UserId = userId,
                        ProductId = productId,
                        SessionId = userId.HasValue ? null : sessionId,
                        ViewedAt = DateTime.Now
                    };

                    _context.ProductViews.Add(productView);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    recentView.ViewedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // Không làm gì nếu có lỗi (không ảnh hưởng đến việc xem sản phẩm)
            }
        }
    }
}
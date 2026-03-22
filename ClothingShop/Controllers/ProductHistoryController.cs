using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class ProductHistoryController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /ProductHistory - Xem lịch sử sản phẩm đã xem
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            IQueryable<ProductView> query;

            if (!string.IsNullOrEmpty(userIdString))
            {
                // Nếu đã đăng nhập, lấy theo UserId
                var userId = int.Parse(userIdString);
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.UserId == userId);
            }
            else
            {
                // Nếu chưa đăng nhập, lấy theo SessionId
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.SessionId == sessionId);
            }

            // Lấy tất cả lịch sử xem trước
            var allViews = await query.ToListAsync();
            
            // Lấy sản phẩm duy nhất (distinct by ProductId) và sắp xếp theo thời gian xem gần nhất
            var productViews = allViews
                .GroupBy(pv => pv.ProductId)
                .Select(g => g.OrderByDescending(pv => pv.ViewedAt).First())
                .OrderByDescending(pv => pv.ViewedAt)
                .ToList();

            // Phân trang
            var totalItems = productViews.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var pagedViews = productViews
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            // Nếu chưa có sản phẩm đã xem, lấy sản phẩm đề xuất
            if (totalItems == 0)
            {
                var recommendedProducts = await _context.Products
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(8)
                    .ToListAsync();
                ViewBag.RecommendedProducts = recommendedProducts;
            }

            return View(pagedViews);
        }

        // API: /ProductHistory/Track - Ghi nhận lượt xem sản phẩm
        [HttpPost]
        public async Task<IActionResult> Track(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });
            }

            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            int? userId = null;
            if (!string.IsNullOrEmpty(userIdString))
            {
                userId = int.Parse(userIdString);
            }

            // Kiểm tra xem đã xem trong vòng 5 phút chưa (tránh spam)
            var recentView = await _context.ProductViews
                .Where(pv => pv.ProductId == productId &&
                            pv.ViewedAt >= DateTime.Now.AddMinutes(-5) &&
                            (userId.HasValue ? pv.UserId == userId : pv.SessionId == sessionId))
                .FirstOrDefaultAsync();

            if (recentView == null)
            {
                // Tạo record mới
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
                // Cập nhật thời gian xem
                recentView.ViewedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // API: /ProductHistory/GetHistory - Lấy lịch sử xem (JSON)
        [HttpGet]
        public async Task<IActionResult> GetHistory(int limit = 10)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            IQueryable<ProductView> query;

            if (!string.IsNullOrEmpty(userIdString))
            {
                var userId = int.Parse(userIdString);
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.UserId == userId);
            }
            else
            {
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.SessionId == sessionId);
            }

            var history = await query
                .GroupBy(pv => pv.ProductId)
                .Select(g => g.OrderByDescending(pv => pv.ViewedAt).First())
                .OrderByDescending(pv => pv.ViewedAt)
                .Take(limit)
                .Select(pv => new
                {
                    pv.ProductId,
                    pv.Product!.Name,
                    pv.Product.Price,
                    pv.Product.ImageUrl,
                    pv.Product.Category,
                    pv.ViewedAt
                })
                .ToListAsync();

            return Json(new { history });
        }

        // POST: /ProductHistory/Clear - Xóa lịch sử xem
        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            if (!string.IsNullOrEmpty(userIdString))
            {
                var userId = int.Parse(userIdString);
                var views = await _context.ProductViews
                    .Where(pv => pv.UserId == userId)
                    .ToListAsync();
                _context.ProductViews.RemoveRange(views);
            }
            else
            {
                var views = await _context.ProductViews
                    .Where(pv => pv.SessionId == sessionId)
                    .ToListAsync();
                _context.ProductViews.RemoveRange(views);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa lịch sử xem sản phẩm!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /ProductHistory/Remove - Xóa 1 sản phẩm khỏi lịch sử
        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            if (!string.IsNullOrEmpty(userIdString))
            {
                var userId = int.Parse(userIdString);
                var views = await _context.ProductViews
                    .Where(pv => pv.UserId == userId && pv.ProductId == productId)
                    .ToListAsync();
                _context.ProductViews.RemoveRange(views);
            }
            else
            {
                var views = await _context.ProductViews
                    .Where(pv => pv.SessionId == sessionId && pv.ProductId == productId)
                    .ToListAsync();
                _context.ProductViews.RemoveRange(views);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa sản phẩm khỏi lịch sử!";
            return RedirectToAction(nameof(Index));
        }

        // API: /ProductHistory/GetRecommendations - Gợi ý sản phẩm dựa trên lịch sử
        [HttpGet]
        public async Task<IActionResult> GetRecommendations(int limit = 8)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            var sessionId = HttpContext.Session.Id;

            // Lấy danh mục và giới tính từ lịch sử xem
            IQueryable<ProductView> query;

            if (!string.IsNullOrEmpty(userIdString))
            {
                var userId = int.Parse(userIdString);
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.UserId == userId);
            }
            else
            {
                query = _context.ProductViews
                    .Include(pv => pv.Product)
                    .Where(pv => pv.SessionId == sessionId);
            }

            var viewedProducts = await query
                .Select(pv => pv.Product)
                .ToListAsync();

            if (viewedProducts.Count == 0)
            {
                // Nếu chưa có lịch sử, trả về sản phẩm mới nhất
                var newProducts = await _context.Products
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(limit)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Price,
                        p.ImageUrl,
                        p.Category,
                        p.Gender
                    })
                    .ToListAsync();

                return Json(new { recommendations = newProducts });
            }

            // Lấy danh mục và giới tính phổ biến nhất
            var popularCategory = viewedProducts
                .GroupBy(p => p!.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var popularGender = viewedProducts
                .GroupBy(p => p!.Gender)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var viewedProductIds = viewedProducts.Select(p => p!.Id).ToList();

            // Tìm sản phẩm tương tự
            var recommendations = await _context.Products
                .Where(p => p.Quantity > 0 &&
                           !viewedProductIds.Contains(p.Id) &&
                           (p.Category == popularCategory || p.Gender == popularGender))
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.ImageUrl,
                    p.Category,
                    p.Gender
                })
                .ToListAsync();

            return Json(new { recommendations });
        }
    }
}

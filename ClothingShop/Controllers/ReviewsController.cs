using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: Thêm đánh giá
        [HttpPost]
        public async Task<IActionResult> Add(int productId, int orderId, int rating, string? comment)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                TempData["Error"] = "Vui lòng đăng nhập để đánh giá";
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);

            // Kiểm tra xem user đã mua sản phẩm này chưa
            var hasPurchased = await _context.OrderItems
                .AnyAsync(oi => oi.ProductId == productId &&
                               _context.Orders.Any(o => o.Id == oi.OrderId && 
                                                       o.UserId == userId && 
                                                       o.Status == "Đã giao"));

            if (!hasPurchased)
            {
                TempData["Error"] = "Bạn chỉ có thể đánh giá sản phẩm đã mua";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            var review = new ProductReview
            {
                ProductId = productId,
                UserId = userId,
                OrderId = orderId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _context.ProductReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cảm ơn bạn đã đánh giá!";
            return Redirect($"/Products/Details/{productId}#reviews");
        }

        // GET: Lấy đánh giá của sản phẩm
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            var reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Json(reviews);
        }
    }
}

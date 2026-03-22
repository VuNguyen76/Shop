using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class TestController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<IActionResult> CheckReviews()
        {
            var result = new System.Text.StringBuilder();
            
            // Kiểm tra reviews
            var reviews = await _context.ProductReviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .ToListAsync();

            result.AppendLine($"=== REVIEWS IN DATABASE ===");
            result.AppendLine($"Total reviews: {reviews.Count}\n");
            
            foreach (var review in reviews)
            {
                result.AppendLine($"Review ID: {review.Id}");
                result.AppendLine($"Product ID: {review.ProductId}");
                result.AppendLine($"Product Name: {review.Product?.Name ?? "N/A"}");
                result.AppendLine($"User: {review.User?.FullName ?? "N/A"}");
                result.AppendLine($"Rating: {review.Rating} stars");
                result.AppendLine($"Comment: {review.Comment}");
                result.AppendLine($"Created: {review.CreatedAt}");
                result.AppendLine("---\n");
            }

            // Kiểm tra products
            result.AppendLine($"\n=== PRODUCTS IN DATABASE ===");
            var products = await _context.Products.ToListAsync();
            result.AppendLine($"Total products: {products.Count}\n");
            
            foreach (var product in products)
            {
                result.AppendLine($"Product ID: {product.Id}");
                result.AppendLine($"Product Name: {product.Name}");
                result.AppendLine($"---");
            }

            // Kiểm tra ratings grouped
            result.AppendLine($"\n=== RATINGS GROUPED BY PRODUCT ===");
            var productRatings = await _context.ProductReviews
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AverageRating = g.Average(r => r.Rating),
                    TotalReviews = g.Count()
                })
                .ToListAsync();

            result.AppendLine($"Total products with ratings: {productRatings.Count}\n");
            foreach (var rating in productRatings)
            {
                result.AppendLine($"Product ID: {rating.ProductId}");
                result.AppendLine($"Average Rating: {rating.AverageRating:F1} stars");
                result.AppendLine($"Total Reviews: {rating.TotalReviews}");
                result.AppendLine($"---");
            }

            return Content(result.ToString(), "text/plain");
        }
    }
}

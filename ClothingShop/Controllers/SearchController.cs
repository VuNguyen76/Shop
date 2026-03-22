using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class SearchController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private static readonly char[] Separators = [' ', '-', '_'];

        // API: /Search/Autocomplete - Tìm kiếm gợi ý (autocomplete)
        [HttpGet]
        public async Task<IActionResult> Autocomplete(string q, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new { suggestions = new List<object>() });
            }

            // Lấy tất cả sản phẩm có sẵn
            var allProducts = await _context.Products
                .Where(p => p.Quantity > 0)
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

            var searchQuery = q.ToLower().Trim();
            
            // Tìm kiếm theo 3 cách:
            // 1. Tên chứa từ khóa (ưu tiên cao)
            // 2. Có từ bắt đầu bằng từ khóa
            // 3. Chữ cái đầu của các từ ghép lại khớp với từ khóa (acronym)
            var products = allProducts
                .Where(p => 
                {
                    var nameLower = p.Name.ToLower();
                    
                    // Tìm kiếm thông thường - tên chứa từ khóa
                    if (nameLower.Contains(searchQuery))
                        return true;
                    
                    // Tìm kiếm theo từ bắt đầu bằng chữ cái
                    var words = p.Name.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Any(w => w.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    
                    // Tìm kiếm theo acronym (chữ cái đầu của mỗi từ)
                    var acronym = string.Join("", words.Select(w => w[0])).ToLower();
                    return acronym.Contains(searchQuery);
                })
                .OrderByDescending(p => 
                {
                    var nameLower = p.Name.ToLower();
                    var words = p.Name.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Ưu tiên 1: Tên bắt đầu bằng từ khóa
                    if (nameLower.StartsWith(searchQuery, StringComparison.Ordinal))
                        return 4;
                    
                    // Ưu tiên 2: Có từ bắt đầu bằng từ khóa
                    if (words.Any(w => w.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase)))
                        return 3;
                    
                    // Ưu tiên 3: Tên chứa từ khóa
                    if (nameLower.Contains(searchQuery))
                        return 2;
                    
                    // Ưu tiên 4: Chỉ khớp acronym
                    return 1;
                })
                .Take(limit)
                .ToList();

            return Json(new { suggestions = products });
        }

        // API: /Search/QuickSearch - Tìm kiếm nhanh với nhiều tiêu chí
        [HttpGet]
        public async Task<IActionResult> QuickSearch(
            string? q,
            string? category,
            string? gender,
            decimal? minPrice,
            decimal? maxPrice,
            string? size,
            string? color,
            int page = 1,
            int pageSize = 20)
        {
            var query = _context.Products.Where(p => p.Quantity > 0).AsQueryable();

            // Tìm kiếm theo từ khóa
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => p.Name.Contains(q) || 
                                        (p.Description != null && p.Description.Contains(q)));
            }

            // Lọc theo danh mục
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Lọc theo giới tính
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(p => p.Gender == gender || p.Gender == "Unisex");
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // Lọc theo size
            if (!string.IsNullOrWhiteSpace(size))
            {
                query = query.Where(p => p.Size.Contains(size));
            }

            // Lọc theo màu
            if (!string.IsNullOrWhiteSpace(color))
            {
                query = query.Where(p => p.Color.Contains(color));
            }

            // Đếm tổng số
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Phân trang
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.ImageUrl,
                    p.Category,
                    p.Gender,
                    p.Size,
                    p.Color,
                    p.Quantity
                })
                .ToListAsync();

            return Json(new
            {
                products,
                pagination = new
                {
                    currentPage = page,
                    totalPages,
                    totalItems,
                    pageSize
                }
            });
        }

        // API: /Search/GetFilters - Lấy danh sách filter động
        [HttpGet]
        public async Task<IActionResult> GetFilters(string? category = null, string? gender = null)
        {
            var query = _context.Products.Where(p => p.Quantity > 0).AsQueryable();

            // Lọc theo danh mục nếu có
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Lọc theo giới tính nếu có
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(p => p.Gender == gender || p.Gender == "Unisex");
            }

            // Lấy danh sách categories
            var categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Name, c.Description })
                .ToListAsync();

            // Lấy danh sách giới tính có sẵn
            var genders = await query
                .Select(p => p.Gender)
                .Distinct()
                .ToListAsync();

            // Lấy danh sách size có sẵn (load về memory trước)
            var productsForFilters = await query
                .Select(p => new { p.Size, p.Color })
                .ToListAsync();

            var sizes = productsForFilters
                .SelectMany(p => p.Size.Split(','))
                .Select(s => s.Trim())
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            // Lấy danh sách màu có sẵn
            var colors = productsForFilters
                .SelectMany(p => p.Color.Split(','))
                .Select(c => c.Trim())
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Lấy khoảng giá
            var minPrice = await query.MinAsync(p => (decimal?)p.Price) ?? 0;
            var maxPrice = await query.MaxAsync(p => (decimal?)p.Price) ?? 0;

            return Json(new
            {
                categories,
                genders,
                sizes,
                colors,
                priceRange = new
                {
                    min = minPrice,
                    max = maxPrice
                }
            });
        }

        // API: /Search/Suggestions - Gợi ý từ khóa phổ biến
        [HttpGet]
        public async Task<IActionResult> Suggestions()
        {
            // Lấy top 10 sản phẩm bán chạy
            var bestSellers = await _context.OrderItems
                .Where(oi => _context.Orders.Any(o => o.Id == oi.OrderId && o.Status == "Đã giao"))
                .GroupBy(oi => oi.ProductName)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .Select(x => x.Name)
                .ToListAsync();

            // Lấy danh mục phổ biến
            var popularCategories = await _context.Products
                .Where(p => p.Quantity > 0)
                .GroupBy(p => p.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Select(x => x.Category)
                .ToListAsync();

            return Json(new
            {
                bestSellers,
                popularCategories
            });
        }

        // API: /Search/RelatedProducts - Sản phẩm liên quan
        [HttpGet]
        public async Task<IActionResult> RelatedProducts(int productId, int limit = 8)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return Json(new { products = new List<object>() });
            }

            // Tìm sản phẩm cùng danh mục hoặc cùng giới tính
            var relatedProducts = await _context.Products
                .Where(p => p.Id != productId && 
                           p.Quantity > 0 && 
                           (p.Category == product.Category || p.Gender == product.Gender))
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

            return Json(new { products = relatedProducts });
        }

        // API: /Search/TrendingProducts - Sản phẩm đang hot
        [HttpGet]
        public async Task<IActionResult> TrendingProducts(int limit = 12)
        {
            // Lấy sản phẩm được xem nhiều trong 7 ngày gần đây
            var sevenDaysAgo = DateTime.Now.AddDays(-7);
            
            var trendingProducts = await _context.ProductViews
                .Where(pv => pv.ViewedAt >= sevenDaysAgo)
                .GroupBy(pv => pv.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ViewCount = g.Count()
                })
                .OrderByDescending(x => x.ViewCount)
                .Take(limit)
                .Join(_context.Products.Where(p => p.Quantity > 0),
                      x => x.ProductId,
                      p => p.Id,
                      (x, p) => new
                      {
                          p.Id,
                          p.Name,
                          p.Price,
                          p.ImageUrl,
                          p.Category,
                          p.Gender,
                          x.ViewCount
                      })
                .ToListAsync();

            // Nếu không có dữ liệu view, lấy sản phẩm mới nhất
            if (trendingProducts.Count == 0)
            {
                trendingProducts = await _context.Products
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
                        p.Gender,
                        ViewCount = 0
                    })
                    .ToListAsync();
            }

            return Json(new { products = trendingProducts });
        }
    }
}

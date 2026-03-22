using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Inventory")]
    public class InventoryController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin/Inventory - Danh sách giao dịch kho
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? type, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.InventoryTransactions
                .Include(it => it.Product)
                .Include(it => it.Creator)
                .AsQueryable();

            // Lọc theo loại
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(it => it.Type == type);
                ViewBag.Type = type;
            }

            // Lọc theo ngày
            if (startDate.HasValue)
            {
                query = query.Where(it => it.CreatedAt >= startDate.Value);
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                query = query.Where(it => it.CreatedAt <= endDate.Value);
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            query = query.OrderByDescending(it => it.CreatedAt);

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Thống kê tổng quan
            ViewBag.TotalImport = await _context.InventoryTransactions
                .Where(it => it.Type == "Nhập")
                .SumAsync(it => it.Quantity);

            ViewBag.TotalExport = await _context.InventoryTransactions
                .Where(it => it.Type == "Xuất")
                .SumAsync(it => it.Quantity);

            return View(transactions);
        }

        // GET: /Admin/Inventory/ImportStock - Form nhập kho
        [HttpGet("ImportStock")]
        public async Task<IActionResult> ImportStock(int? productId)
        {
            ViewBag.Products = await _context.Products
                .OrderBy(p => p.Name)
                .ToListAsync();
            
            // Nếu có productId, tự động chọn sản phẩm đó
            if (productId.HasValue)
            {
                ViewBag.SelectedProductId = productId.Value;
                var product = await _context.Products.FindAsync(productId.Value);
                ViewBag.SelectedProduct = product;
            }
            
            return View();
        }

        // POST: /Admin/Inventory/ImportStock - Xử lý nhập kho
        [HttpPost("ImportStock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStock(int productId, int quantity, string? supplier, decimal? cost, decimal sellingPrice, string? reason)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);

            if (quantity <= 0)
            {
                TempData["Error"] = "Số lượng phải lớn hơn 0!";
                return RedirectToAction(nameof(ImportStock));
            }

            if (sellingPrice <= 0)
            {
                TempData["Error"] = "Giá bán phải lớn hơn 0!";
                return RedirectToAction(nameof(ImportStock));
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(ImportStock));
            }

            // Tạo giao dịch nhập kho
            var transaction = new InventoryTransaction
            {
                ProductId = productId,
                Type = "Nhập",
                Quantity = quantity,
                Supplier = supplier,
                Cost = cost,
                Reason = reason,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };

            _context.InventoryTransactions.Add(transaction);

            // Cập nhật số lượng tồn kho, giá nhập và giá bán
            product.Quantity += quantity;
            product.Price = sellingPrice;
            if (cost.HasValue)
            {
                product.Cost = cost.Value;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã nhập {quantity} sản phẩm '{product.Name}' vào kho với giá bán {sellingPrice:N0}₫!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Inventory/ExportStock - Form xuất kho
        [HttpGet("ExportStock")]
        public async Task<IActionResult> ExportStock(int? productId)
        {
            ViewBag.Products = await _context.Products
                .Where(p => p.Quantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();
            
            // Nếu có productId, tự động chọn sản phẩm đó
            if (productId.HasValue)
            {
                ViewBag.SelectedProductId = productId.Value;
                var product = await _context.Products.FindAsync(productId.Value);
                ViewBag.SelectedProduct = product;
            }
            
            return View();
        }

        // POST: /Admin/Inventory/ExportStock - Xử lý xuất kho
        [HttpPost("ExportStock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportStock(int productId, int quantity, string? reason)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);

            if (quantity <= 0)
            {
                TempData["Error"] = "Số lượng phải lớn hơn 0!";
                return RedirectToAction(nameof(ExportStock));
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(ExportStock));
            }

            if (product.Quantity < quantity)
            {
                TempData["Error"] = $"Không đủ hàng trong kho! Hiện có: {product.Quantity}";
                return RedirectToAction(nameof(ExportStock));
            }

            // Tạo giao dịch xuất kho
            var transaction = new InventoryTransaction
            {
                ProductId = productId,
                Type = "Xuất",
                Quantity = quantity,
                Reason = reason,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };

            _context.InventoryTransactions.Add(transaction);

            // Cập nhật số lượng tồn kho
            product.Quantity -= quantity;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xuất {quantity} sản phẩm '{product.Name}' khỏi kho!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Inventory/StockReport - Báo cáo tồn kho
        [HttpGet("StockReport")]
        public async Task<IActionResult> StockReport(string? search, string? category, string? sortBy)
        {
            var query = _context.Products.AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search));
                ViewBag.Search = search;
            }

            // Lọc theo danh mục
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
                ViewBag.Category = category;
            }

            // Sắp xếp
            query = sortBy switch
            {
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "stock_asc" => query.OrderBy(p => p.Quantity),
                "stock_desc" => query.OrderByDescending(p => p.Quantity),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                _ => query.OrderBy(p => p.Name)
            };
            ViewBag.SortBy = sortBy;

            var products = await query.ToListAsync();

            // Thống kê
            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalStock = products.Sum(p => p.Quantity);
            ViewBag.TotalValue = products.Sum(p => p.Quantity * p.Price);
            ViewBag.OutOfStock = products.Count(p => p.Quantity == 0);
            ViewBag.LowStock = products.Count(p => p.Quantity > 0 && p.Quantity <= 10);

            // Load danh mục
            ViewBag.Categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Name)
                .ToListAsync();

            return View(products);
        }

        // GET: /Admin/Inventory/ProductHistory/{id} - Lịch sử nhập/xuất của sản phẩm
        [HttpGet("ProductHistory/{id}")]
        public async Task<IActionResult> ProductHistory(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(StockReport));
            }

            var transactions = await _context.InventoryTransactions
                .Include(it => it.Creator)
                .Include(it => it.Order)
                .Where(it => it.ProductId == id)
                .OrderByDescending(it => it.CreatedAt)
                .ToListAsync();

            ViewBag.Product = product;
            ViewBag.TotalImport = transactions.Where(t => t.Type == "Nhập").Sum(t => t.Quantity);
            ViewBag.TotalExport = transactions.Where(t => t.Type == "Xuất").Sum(t => t.Quantity);

            return View(transactions);
        }

        // GET: /Admin/Inventory/EditProduct/{id} - Chỉnh sửa số lượng và giá
        [HttpGet("EditProduct/{id}")]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(LowStockAlert));
            }
            
            return View(product);
        }

        // POST: /Admin/Inventory/UpdateProduct - Cập nhật số lượng và giá
        [HttpPost("UpdateProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(int id, int quantity, decimal? cost, decimal price)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(LowStockAlert));
            }

            if (quantity < 0)
            {
                TempData["Error"] = "Số lượng không được âm!";
                return RedirectToAction(nameof(EditProduct), new { id });
            }

            if (price <= 0)
            {
                TempData["Error"] = "Giá bán phải lớn hơn 0!";
                return RedirectToAction(nameof(EditProduct), new { id });
            }

            // Lưu giá trị cũ để ghi log
            var oldQuantity = product.Quantity;
            var oldPrice = product.Price;

            // Cập nhật
            product.Quantity = quantity;
            product.Price = price;
            if (cost.HasValue)
            {
                product.Cost = cost.Value;
            }

            // Tạo log nếu số lượng thay đổi
            if (oldQuantity != quantity)
            {
                var transaction = new InventoryTransaction
                {
                    ProductId = id,
                    Type = quantity > oldQuantity ? "Nhập" : "Xuất",
                    Quantity = Math.Abs(quantity - oldQuantity),
                    Cost = cost,
                    Reason = $"Chỉnh sửa trực tiếp: {oldQuantity} → {quantity}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                };
                _context.InventoryTransactions.Add(transaction);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật sản phẩm '{product.Name}'!";
            return RedirectToAction(nameof(LowStockAlert));
        }

        // GET: /Admin/Inventory/LowStockAlert - Danh sách tồn kho
        [HttpGet("LowStockAlert")]
        public async Task<IActionResult> LowStockAlert(string? search, string? sortBy)
        {
            var query = _context.Products.AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Category.Contains(search));
                ViewBag.Search = search;
            }

            // Sắp xếp
            query = sortBy switch
            {
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "stock_asc" => query.OrderBy(p => p.Quantity),
                "stock_desc" => query.OrderByDescending(p => p.Quantity),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                _ => query.OrderBy(p => p.Quantity) // Mặc định: tồn kho thấp trước
            };
            ViewBag.SortBy = sortBy;

            var products = await query.ToListAsync();

            // Thống kê
            ViewBag.TotalProducts = products.Count;
            ViewBag.OutOfStock = products.Count(p => p.Quantity == 0);
            ViewBag.LowStock = products.Count(p => p.Quantity > 0 && p.Quantity <= 10);
            ViewBag.InStock = products.Count(p => p.Quantity > 10);

            return View(products);
        }
    }
}

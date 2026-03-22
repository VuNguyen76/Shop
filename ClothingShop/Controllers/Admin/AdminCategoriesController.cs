using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Categories")]
    public class AdminCategoriesController(ApplicationDbContext context, IWebHostEnvironment env) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;

        // ==================== FASHION CATEGORIES ====================
        
        // GET: /Admin/Categories/Fashion
        [HttpGet("Fashion")]
        public async Task<IActionResult> Fashion()
        {
            var categories = await _context.FashionCategories
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
            
            // Load danh mục sản phẩm để tạo link tự động
            ViewBag.ProductCategories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            
            return View("~/Views/Admin/FashionCategories.cshtml", categories);
        }

        [HttpPost("Fashion/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFashion(FashionCategory model, IFormFile? imageFile)
        {
            // Xử lý upload ảnh nếu có
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadPath = Path.Combine(_env.WebRootPath, "images", "fashion");
                Directory.CreateDirectory(uploadPath);

                var extension = Path.GetExtension(imageFile.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".avif" };
                
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] = "Chỉ hỗ trợ file ảnh: JPG, PNG, WebP, AVIF";
                    return RedirectToAction(nameof(Fashion));
                }

                var fileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(uploadPath, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                
                model.ImageUrl = "/images/fashion/" + fileName;
            }

            if (string.IsNullOrEmpty(model.ImageUrl))
            {
                TempData["Error"] = "Vui lòng upload ảnh hoặc nhập URL hình ảnh!";
                return RedirectToAction(nameof(Fashion));
            }

            if (model.DisplayOrder == 0)
            {
                var maxOrder = await _context.FashionCategories.MaxAsync(f => (int?)f.DisplayOrder) ?? 0;
                model.DisplayOrder = maxOrder + 1;
            }
            else
            {
                var exists = await _context.FashionCategories.AnyAsync(f => f.DisplayOrder == model.DisplayOrder);
                if (exists)
                {
                    TempData["Error"] = $"Thứ tự {model.DisplayOrder} đã tồn tại! Vui lòng chọn số khác.";
                    return RedirectToAction(nameof(Fashion));
                }
            }

            if (ModelState.IsValid)
            {
                _context.FashionCategories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm danh mục thành công!";
            }
            else
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
            }
            return RedirectToAction(nameof(Fashion));
        }

        [HttpPost("Fashion/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFashion(FashionCategory model)
        {
            var category = await _context.FashionCategories.FindAsync(model.Id);
            if (category != null)
            {
                if (category.DisplayOrder != model.DisplayOrder)
                {
                    var exists = await _context.FashionCategories
                        .AnyAsync(f => f.DisplayOrder == model.DisplayOrder && f.Id != model.Id);
                    if (exists)
                    {
                        TempData["Error"] = $"Thứ tự {model.DisplayOrder} đã tồn tại! Vui lòng chọn số khác.";
                        return RedirectToAction(nameof(Fashion));
                    }
                }

                category.Title = model.Title;
                category.ImageUrl = model.ImageUrl;
                category.LinkUrl = model.LinkUrl;
                category.DisplayOrder = model.DisplayOrder;
                category.IsActive = model.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật danh mục thành công!";
            }
            return RedirectToAction(nameof(Fashion));
        }

        [HttpPost("Fashion/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFashion(int id)
        {
            var category = await _context.FashionCategories.FindAsync(id);
            if (category != null)
            {
                _context.FashionCategories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa danh mục thành công!";
            }
            return RedirectToAction(nameof(Fashion));
        }

        // ==================== PRODUCT CATEGORIES ====================
        
        // GET: /Admin/Categories/Product
        [HttpGet("Product")]
        public async Task<IActionResult> Product()
        {
            var categories = await _context.ProductCategories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            return View("~/Views/Admin/ProductCategories.cshtml", categories);
        }

        [HttpPost("Product/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductCategory model)
        {
            if (model.DisplayOrder == 0)
            {
                var maxOrder = await _context.ProductCategories.MaxAsync(c => (int?)c.DisplayOrder) ?? 0;
                model.DisplayOrder = maxOrder + 1;
            }
            else
            {
                var exists = await _context.ProductCategories.AnyAsync(c => c.DisplayOrder == model.DisplayOrder);
                if (exists)
                {
                    TempData["Error"] = $"Thứ tự {model.DisplayOrder} đã tồn tại! Vui lòng chọn số khác.";
                    return RedirectToAction(nameof(Product));
                }
            }

            if (ModelState.IsValid)
            {
                _context.ProductCategories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm danh mục thành công!";
            }
            return RedirectToAction(nameof(Product));
        }

        [HttpPost("Product/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(ProductCategory model)
        {
            var category = await _context.ProductCategories.FindAsync(model.Id);
            if (category != null)
            {
                if (category.DisplayOrder != model.DisplayOrder)
                {
                    var exists = await _context.ProductCategories
                        .AnyAsync(c => c.DisplayOrder == model.DisplayOrder && c.Id != model.Id);
                    if (exists)
                    {
                        TempData["Error"] = $"Thứ tự {model.DisplayOrder} đã tồn tại! Vui lòng chọn số khác.";
                        return RedirectToAction(nameof(Product));
                    }
                }

                category.Name = model.Name;
                category.Description = model.Description;
                category.DisplayOrder = model.DisplayOrder;
                category.IsActive = model.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật danh mục thành công!";
            }
            return RedirectToAction(nameof(Product));
        }

        [HttpPost("Product/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var category = await _context.ProductCategories.FindAsync(id);
            if (category != null)
            {
                var hasProducts = await _context.Products.AnyAsync(p => p.Category == category.Name);
                if (hasProducts)
                {
                    TempData["Error"] = "Không thể xóa danh mục đang có sản phẩm!";
                }
                else
                {
                    _context.ProductCategories.Remove(category);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Xóa danh mục thành công!";
                }
            }
            return RedirectToAction(nameof(Product));
        }
    }
}

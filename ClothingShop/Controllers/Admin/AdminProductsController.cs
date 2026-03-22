using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Products")]
    public class AdminProductsController(ApplicationDbContext context, IWebHostEnvironment env) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;

        // GET: /Admin/Products
        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, string? category, string? gender, string? sortBy, int page = 1)
        {
            const int pageSize = 10;
            
            // Load danh mục động
            ViewBag.Categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Name)
                .ToListAsync();
            
            var query = _context.Products.Where(p => !p.IsDeleted).AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
                ViewBag.Search = search;
            }

            // Lọc theo danh mục
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
                ViewBag.Category = category;
            }

            // Lọc theo giới tính
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(p => p.Gender == gender);
                ViewBag.Gender = gender;
            }

            // Sắp xếp
            query = sortBy switch
            {
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "stock_asc" => query.OrderBy(p => p.Quantity),
                "stock_desc" => query.OrderByDescending(p => p.Quantity),
                _ => query.OrderByDescending(p => p.Id)
            };
            ViewBag.SortBy = sortBy;

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            return View("~/Views/Admin/Products.cshtml", products);
        }

        // GET: /Admin/Products/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            return View("~/Views/Admin/CreateProduct.cshtml");
        }

        // POST: /Admin/Products/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile, List<IFormFile>? additionalImages)
        {
            if (ModelState.IsValid)
            {
                var uploadPath = Path.Combine(_env.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadPath);

                // Xử lý upload ảnh chính
                if (imageFile != null && imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    
                    product.ImageUrl = "/images/products/" + fileName;
                }

                // Xử lý upload ảnh phụ
                if (additionalImages != null && additionalImages.Count > 0)
                {
                    var imageUrls = new List<string>();
                    
                    foreach (var image in additionalImages.Take(5)) // Giới hạn 5 ảnh
                    {
                        if (image.Length > 0)
                        {
                            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                            var filePath = Path.Combine(uploadPath, fileName);
                            
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(stream);
                            }
                            
                            imageUrls.Add("/images/products/" + fileName);
                        }
                    }
                    
                    product.AdditionalImages = System.Text.Json.JsonSerializer.Serialize(imageUrls);
                }

                // Set mặc định
                product.Quantity = 0;
                product.Price = 0; // Giá bán sẽ được set khi nhập kho
                product.Cost = 0; // Giá nhập sẽ được set khi nhập kho
                product.CreatedAt = DateTime.Now;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Thêm sản phẩm thành công! Vui lòng vào 'Quản lý Kho hàng → Nhập kho' để thêm hàng.";
                return RedirectToAction(nameof(Index));
            }

            return View(product);
        }

        // GET: /Admin/Products/Edit/{id}
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            
            return View("~/Views/Admin/EditProduct.cshtml", product);
        }

        // POST: /Admin/Products/Update
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(Product product, IFormFile? imageFile, List<IFormFile>? additionalImages)
        {
            var existingProduct = await _context.Products.FindAsync(product.Id);
            if (existingProduct == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(Index));
            }

            var uploadPath = Path.Combine(_env.WebRootPath, "images", "products");
            Directory.CreateDirectory(uploadPath);

            // Xử lý upload ảnh chính mới (nếu có)
            if (imageFile != null && imageFile.Length > 0)
            {
                // Xóa ảnh cũ
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadPath, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                
                existingProduct.ImageUrl = "/images/products/" + fileName;
            }

            // Xử lý upload ảnh phụ mới (nếu có)
            if (additionalImages != null && additionalImages.Count > 0)
            {
                // Xóa ảnh phụ cũ
                if (!string.IsNullOrEmpty(existingProduct.AdditionalImages))
                {
                    var oldImages = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingProduct.AdditionalImages);
                    if (oldImages != null)
                    {
                        foreach (var imgUrl in oldImages)
                        {
                            var oldImagePath = Path.Combine(_env.WebRootPath, imgUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }
                    }
                }

                var imageUrls = new List<string>();
                foreach (var image in additionalImages.Take(5))
                {
                    if (image.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                        var filePath = Path.Combine(uploadPath, fileName);
                        
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }
                        
                        imageUrls.Add("/images/products/" + fileName);
                    }
                }
                
                existingProduct.AdditionalImages = System.Text.Json.JsonSerializer.Serialize(imageUrls);
            }

            // Cập nhật thông tin sản phẩm (KHÔNG cập nhật Price và Quantity)
            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Category = product.Category;
            existingProduct.Gender = product.Gender;
            existingProduct.Size = product.Size;
            existingProduct.Color = product.Color;
            // Price và Quantity GIỮ NGUYÊN - chỉ được cập nhật qua "Nhập kho"
            // existingProduct.Price = existingProduct.Price; (không cần vì không thay đổi)
            // existingProduct.Quantity = existingProduct.Quantity; (không cần vì không thay đổi)

            // Đánh dấu chỉ những field được phép thay đổi
            _context.Entry(existingProduct).Property(p => p.Name).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.Description).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.Category).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.Gender).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.Size).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.Color).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.ImageUrl).IsModified = true;
            _context.Entry(existingProduct).Property(p => p.AdditionalImages).IsModified = true;
            // Price và Quantity KHÔNG được đánh dấu IsModified

            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Cập nhật thông tin sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Products/Delete/{id}
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(Index));
            }

            if (product.IsDeleted)
            {
                TempData["Error"] = "Sản phẩm này đã được xóa trước đó!";
                return RedirectToAction(nameof(Index));
            }

            // SOFT DELETE - Chỉ đánh dấu là đã xóa, không xóa thật
            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Products/Deleted - Danh sách sản phẩm đã xóa
        [HttpGet("Deleted")]
        public async Task<IActionResult> Deleted(string? search, int page = 1)
        {
            const int pageSize = 10;
            
            var query = _context.Products.Where(p => p.IsDeleted).AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search));
                ViewBag.Search = search;
            }

            // Sắp xếp theo thời gian xóa mới nhất
            query = query.OrderByDescending(p => p.DeletedAt);

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            return View("~/Views/Admin/DeletedProducts.cshtml", products);
        }

        // POST: /Admin/Products/Restore/{id} - Khôi phục sản phẩm
        [HttpPost("Restore/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(Deleted));
            }

            if (!product.IsDeleted)
            {
                TempData["Error"] = "Sản phẩm này chưa bị xóa!";
                return RedirectToAction(nameof(Index));
            }

            // Khôi phục sản phẩm
            product.IsDeleted = false;
            product.DeletedAt = null;
            
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã khôi phục sản phẩm '{product.Name}' thành công!";
            return RedirectToAction(nameof(Deleted));
        }

        // POST: /Admin/Products/PermanentDelete/{id} - Xóa vĩnh viễn
        [HttpPost("PermanentDelete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction(nameof(Deleted));
            }

            if (!product.IsDeleted)
            {
                TempData["Error"] = "Chỉ có thể xóa vĩnh viễn sản phẩm đã bị xóa mềm!";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra ràng buộc
            var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
            if (hasOrders)
            {
                TempData["Error"] = "Không thể xóa vĩnh viễn vì sản phẩm đã có trong đơn hàng!";
                return RedirectToAction(nameof(Deleted));
            }

            var hasInventory = await _context.InventoryTransactions.AnyAsync(it => it.ProductId == id);
            if (hasInventory)
            {
                TempData["Error"] = "Không thể xóa vĩnh viễn vì sản phẩm có lịch sử kho hàng!";
                return RedirectToAction(nameof(Deleted));
            }

            // Xóa ảnh
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                var imagePath = Path.Combine(_env.WebRootPath, product.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            if (!string.IsNullOrEmpty(product.AdditionalImages))
            {
                var additionalImages = System.Text.Json.JsonSerializer.Deserialize<List<string>>(product.AdditionalImages);
                if (additionalImages != null)
                {
                    foreach (var imgUrl in additionalImages)
                    {
                        var imagePath = Path.Combine(_env.WebRootPath, imgUrl.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }
                }
            }

            // Xóa thật khỏi database
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa vĩnh viễn sản phẩm!";
            return RedirectToAction(nameof(Deleted));
        }
    }
}

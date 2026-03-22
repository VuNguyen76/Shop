using ClothingShop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Customers")]
    public class AdminCustomersController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin/Customers
        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, string? gender, string? sortBy, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Users.AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.FullName.Contains(search) || 
                                        u.Email.Contains(search) || 
                                        u.PhoneNumber.Contains(search));
                ViewBag.Search = search;
            }

            // Lọc theo giới tính
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(u => u.Gender == gender);
                ViewBag.Gender = gender;
            }

            // Sắp xếp
            query = sortBy switch
            {
                "name_asc" => query.OrderBy(u => u.FullName),
                "name_desc" => query.OrderByDescending(u => u.FullName),
                "date_asc" => query.OrderBy(u => u.CreatedAt),
                "date_desc" => query.OrderByDescending(u => u.CreatedAt),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };
            ViewBag.SortBy = sortBy;

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var customers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy thống kê đơn hàng cho mỗi khách hàng
            var customerIds = customers.Select(c => c.Id).ToList();
            var orderStats = await _context.Orders
                .Where(o => customerIds.Contains(o.UserId))
                .GroupBy(o => o.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalOrders = g.Count(),
                    TotalSpent = g.Where(o => o.Status == "Đã giao").Sum(o => o.TotalAmount)
                })
                .ToListAsync();

            ViewBag.OrderStats = orderStats.ToDictionary(x => x.UserId, x => (dynamic)new { x.TotalOrders, x.TotalSpent });

            return View("~/Views/Admin/Customers.cshtml", customers);
        }

        // GET: /Admin/Customers/Details/{id}
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _context.Users.FindAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            // Lấy đơn hàng của khách hàng
            var orders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Orders = orders;
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalSpent = orders.Where(o => o.Status == "Đã giao").Sum(o => o.TotalAmount);
            ViewBag.PendingOrders = orders.Count(o => o.Status == "Chờ xác nhận" || o.Status == "Chờ lấy hàng" || o.Status == "Chờ giao hàng");

            return View("~/Views/Admin/CustomerDetails.cshtml", customer);
        }

        // POST: /Admin/Customers/ToggleStatus
        [HttpPost("ToggleStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var customer = await _context.Users.FindAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            if (customer.IsAdmin)
            {
                TempData["Error"] = "Không thể khóa tài khoản quản trị viên!";
                return RedirectToAction(nameof(Details), new { id });
            }

            customer.IsActive = !customer.IsActive;
            await _context.SaveChangesAsync();

            var statusText = customer.IsActive ? "mở khóa" : "khóa";
            TempData["Success"] = $"Đã {statusText} tài khoản khách hàng thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Admin/Customers/Update
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, string fullName, string email, string phoneNumber, string? gender)
        {
            var customer = await _context.Users.FindAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            // Validate
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Kiểm tra email trùng
            var emailExists = await _context.Users.AnyAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && u.Id != id);
            if (emailExists)
            {
                TempData["Error"] = "Email đã được sử dụng bởi tài khoản khác!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Kiểm tra số điện thoại trùng
            var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber && u.Id != id);
            if (phoneExists)
            {
                TempData["Error"] = "Số điện thoại đã được sử dụng bởi tài khoản khác!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Cập nhật thông tin
            customer.FullName = fullName.Trim();
            customer.Email = email.Trim().ToLower();
            customer.PhoneNumber = phoneNumber.Trim();
            customer.Gender = gender;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin khách hàng thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Admin/Customers/ResetPassword
        [HttpPost("ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            var customer = await _context.Users.FindAsync(id);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Vui lòng nhập mật khẩu mới!";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (newPassword.Length < 6)
            {
                TempData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Hash mật khẩu mới
            var hashedPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(newPassword)));
            
            customer.PasswordHash = hashedPassword;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đặt lại mật khẩu thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

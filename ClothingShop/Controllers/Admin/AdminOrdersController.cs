using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Orders")]
    public class AdminOrdersController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin/Orders
        [HttpGet("")]
        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status == status);
                ViewBag.Status = status;
            }

            // Tìm kiếm theo ID đơn hàng hoặc tên khách hàng
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (int.TryParse(search, out int orderId))
                {
                    query = query.Where(o => o.Id == orderId);
                }
                else
                {
                    query = query.Where(o => o.User.FullName.Contains(search) || o.User.Email.Contains(search));
                }
                ViewBag.Search = search;
            }

            // Sắp xếp theo ngày mới nhất
            query = query.OrderByDescending(o => o.OrderDate);

            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View("~/Views/Admin/Orders.cshtml", orders);
        }

        // GET: /Admin/Orders/Details/{id}
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/OrderDetails.cshtml", order);
        }

        // POST: /Admin/Orders/UpdateStatus
        [HttpPost("UpdateStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction(nameof(Index));
            }

            var oldStatus = order.Status;

            // Kiểm tra không được quay lại trạng thái trước đó
            var statusOrder = new Dictionary<string, int>
            {
                { "Chờ xác nhận", 1 },
                { "Chờ lấy hàng", 2 },
                { "Chờ giao hàng", 3 },
                { "Đã giao", 4 },
                { "Đã hủy", 5 }
            };

            if (statusOrder.ContainsKey(oldStatus) && statusOrder.ContainsKey(status))
            {
                if (statusOrder[status] < statusOrder[oldStatus] && status != "Đã hủy")
                {
                    TempData["Error"] = "Không thể quay lại trạng thái trước đó! Chỉ có thể chuyển tiến hoặc hủy đơn.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            // Không cho phép cập nhật nếu đơn đã giao hoặc đã hủy
            if (oldStatus == "Đã giao" || oldStatus == "Đã hủy")
            {
                TempData["Error"] = "Không thể thay đổi trạng thái đơn hàng đã hoàn tất!";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = status;

            // Nếu hủy đơn, hoàn lại số lượng tồn kho
            if (status == "Đã hủy" && oldStatus != "Đã hủy")
            {
                foreach (var item in order.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity += item.Quantity;
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notificationTitle = status switch
            {
                "Chờ lấy hàng" => "Đơn hàng đã được xác nhận",
                "Chờ giao hàng" => "Đơn hàng đang được giao",
                "Đã giao" => "Đơn hàng đã giao thành công",
                "Đã hủy" => "Đơn hàng đã bị hủy",
                _ => "Cập nhật trạng thái đơn hàng"
            };

            var notificationMessage = status switch
            {
                "Chờ lấy hàng" => $"Đơn hàng #{order.Id:D6} đã được xác nhận và đang được chuẩn bị.",
                "Chờ giao hàng" => $"Đơn hàng #{order.Id:D6} đang trên đường giao đến bạn.",
                "Đã giao" => $"Đơn hàng #{order.Id:D6} đã được giao thành công. Cảm ơn bạn đã mua hàng!",
                "Đã hủy" => $"Đơn hàng #{order.Id:D6} đã bị hủy bởi quản trị viên.",
                _ => $"Trạng thái đơn hàng #{order.Id:D6} đã được cập nhật."
            };

            var notificationType = status switch
            {
                "Đã giao" => "success",
                "Đã hủy" => "danger",
                _ => "info"
            };

            var notification = new Notification
            {
                UserId = order.UserId,
                Title = notificationTitle,
                Message = notificationMessage,
                Type = notificationType,
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật trạng thái đơn hàng thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Admin/Orders/Cancel
        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string cancelReason)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction(nameof(Index));
            }

            if (order.Status == "Đã hủy")
            {
                TempData["Error"] = "Đơn hàng đã bị hủy trước đó!";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (order.Status == "Đã giao")
            {
                TempData["Error"] = "Không thể hủy đơn hàng đã giao!";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đơn!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Cập nhật trạng thái
            order.Status = "Đã hủy";
            order.CancelReason = cancelReason;
            order.CancelledAt = DateTime.Now;
            order.CancelledBy = "Admin";

            // Hoàn lại số lượng tồn kho
            foreach (var item in order.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Quantity += item.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notification = new Notification
            {
                UserId = order.UserId,
                Title = "Đơn hàng đã bị hủy",
                Message = $"Đơn hàng #{order.Id:D6} đã bị hủy bởi quản trị viên. Lý do: {cancelReason}",
                Type = "danger",
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã hủy đơn hàng #{id}. Lý do: {cancelReason}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

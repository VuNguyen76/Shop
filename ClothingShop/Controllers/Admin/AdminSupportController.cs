using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Support")]
    public class AdminSupportController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin/Support
        [HttpGet("")]
        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.SupportTickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
                ViewBag.Status = status;
            }

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (int.TryParse(search, out int ticketId))
                {
                    query = query.Where(t => t.Id == ticketId);
                }
                else
                {
                    query = query.Where(t => t.Subject.Contains(search) ||
                                           (t.User != null && t.User.FullName.Contains(search)) ||
                                           (t.User != null && t.User.Email.Contains(search)));
                }
                ViewBag.Search = search;
            }

            query = query.OrderByDescending(t => t.CreatedAt);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var tickets = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View("~/Views/Admin/SupportTickets.cshtml", tickets);
        }

        // GET: /Admin/Support/Details/{id}
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _context.SupportTickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu hỗ trợ!";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/SupportTicketDetails.cshtml", ticket);
        }

        // POST: /Admin/Support/Reply
        [HttpPost("Reply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int ticketId, string message)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "Vui lòng nhập nội dung tin nhắn!";
                return RedirectToAction(nameof(Details), new { id = ticketId });
            }

            var ticket = await _context.SupportTickets.FindAsync(ticketId);
            if (ticket == null)
            {
                return NotFound();
            }

            var adminId = int.Parse(userIdString);
            var newMessage = new SupportMessage
            {
                TicketId = ticketId,
                SenderId = adminId,
                IsAdmin = true,
                Message = message,
                CreatedAt = DateTime.Now
            };

            _context.SupportMessages.Add(newMessage);
            
            // Cập nhật trạng thái ticket
            if (ticket.Status == "Mở")
            {
                ticket.Status = "Đang xử lý";
            }
            ticket.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notification = new Notification
            {
                UserId = ticket.UserId,
                Title = "Admin đã trả lời yêu cầu hỗ trợ",
                Message = $"Yêu cầu #{ticket.Id} - {ticket.Subject} có phản hồi mới từ admin.",
                Type = "info",
                SupportTicketId = ticketId,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi phản hồi thành công!";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        // POST: /Admin/Support/UpdateStatus
        [HttpPost("UpdateStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu hỗ trợ!";
                return RedirectToAction(nameof(Index));
            }

            ticket.Status = status;
            ticket.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notificationMessage = status switch
            {
                "Đang xử lý" => $"Yêu cầu #{ticket.Id} đang được xử lý bởi admin.",
                "Đã đóng" => $"Yêu cầu #{ticket.Id} đã được đóng.",
                _ => $"Trạng thái yêu cầu #{ticket.Id} đã được cập nhật."
            };

            var notification = new Notification
            {
                UserId = ticket.UserId,
                Title = "Cập nhật trạng thái yêu cầu hỗ trợ",
                Message = notificationMessage,
                Type = "info",
                SupportTicketId = id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật trạng thái thành công!";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

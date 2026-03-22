using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    public class SupportController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // DANH SÁCH TICKET CỦA KHÁCH HÀNG
        public async Task<IActionResult> Index()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var tickets = await _context.SupportTickets
                .Include(t => t.Messages)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        // TẠO TICKET MỚI
        [HttpPost]
        public async Task<IActionResult> Create(string subject, string message)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
                return RedirectToAction("Index");
            }

            var userId = int.Parse(userIdString);
            var ticket = new SupportTicket
            {
                UserId = userId,
                Subject = subject,
                Status = "Mở",
                CreatedAt = DateTime.Now
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            // Thêm tin nhắn đầu tiên
            var firstMessage = new SupportMessage
            {
                TicketId = ticket.Id,
                SenderId = userId,
                IsAdmin = false,
                Message = message,
                CreatedAt = DateTime.Now
            };

            _context.SupportMessages.Add(firstMessage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã tạo yêu cầu hỗ trợ thành công!";
            return RedirectToAction("Details", new { id = ticket.Id });
        }

        // CHI TIẾT TICKET
        public async Task<IActionResult> Details(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var ticket = await _context.SupportTickets
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // GỬI TIN NHẮN
        [HttpPost]
        public async Task<IActionResult> SendMessage(int ticketId, string message)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "Vui lòng nhập nội dung tin nhắn!";
                return RedirectToAction("Details", new { id = ticketId });
            }

            var userId = int.Parse(userIdString);
            var ticket = await _context.SupportTickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId);

            if (ticket == null)
            {
                return NotFound();
            }

            var newMessage = new SupportMessage
            {
                TicketId = ticketId,
                SenderId = userId,
                IsAdmin = false,
                Message = message,
                CreatedAt = DateTime.Now
            };

            _context.SupportMessages.Add(newMessage);
            ticket.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = ticketId });
        }

        // ĐÓNG TICKET
        [HttpPost]
        public async Task<IActionResult> Close(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var ticket = await _context.SupportTickets
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (ticket == null)
            {
                return NotFound();
            }

            ticket.Status = "Đã đóng";
            ticket.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã đóng yêu cầu hỗ trợ!";
            return RedirectToAction("Index");
        }
    }
}

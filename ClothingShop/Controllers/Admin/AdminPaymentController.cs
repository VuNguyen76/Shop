using ClothingShop.Data;
using ClothingShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Payment")]
    public class AdminPaymentController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin/Payment
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var paymentInfo = await _context.PaymentInfos.FirstOrDefaultAsync();
            
            // Nếu chưa có, tạo mới với giá trị mặc định
            if (paymentInfo == null)
            {
                paymentInfo = new PaymentInfo();
                _context.PaymentInfos.Add(paymentInfo);
                await _context.SaveChangesAsync();
            }
            
            return View("~/Views/Admin/PaymentSettings.cshtml", paymentInfo);
        }

        // POST: /Admin/Payment/Update
        [HttpPost("Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(PaymentInfo model)
        {
            var paymentInfo = await _context.PaymentInfos.FirstOrDefaultAsync();
            
            if (paymentInfo == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin thanh toán!";
                return RedirectToAction(nameof(Index));
            }

            paymentInfo.BankName = model.BankName;
            paymentInfo.BankAccountNumber = model.BankAccountNumber;
            paymentInfo.BankAccountName = model.BankAccountName;
            paymentInfo.MoMoPhone = model.MoMoPhone;
            paymentInfo.MoMoName = model.MoMoName;
            paymentInfo.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin thanh toán thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}

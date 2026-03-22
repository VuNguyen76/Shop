using ClothingShop.Data;
using ClothingShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers;

public class PaymentController(ApplicationDbContext context, IVNPayService vnPayService) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IVNPayService _vnPayService = vnPayService;

    // GET: /Payment/VNPay
    [HttpGet]
    public IActionResult VNPay(int orderId)
    {
        var order = _context.Orders.Find(orderId);
        if (order == null)
        {
            TempData["Error"] = "Không tìm thấy đơn hàng!";
            return RedirectToAction("Index", "Orders");
        }

        // Lấy IP address
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        
        // Tạo URL thanh toán
        string orderInfo = $"Thanh toán đơn hàng #{orderId:D6}";
        string paymentUrl = _vnPayService.CreatePaymentUrl(orderId, order.TotalAmount, orderInfo, ipAddress);

        return Redirect(paymentUrl);
    }

    // GET: /Payment/VNPayReturn
    [HttpGet]
    public async Task<IActionResult> VNPayReturn()
    {
        // Lấy tất cả query params
        var queryParams = Request.Query;
        
        // Lấy vnp_SecureHash
        string vnpSecureHash = queryParams["vnp_SecureHash"]!;
        
        // Validate signature
        bool isValidSignature = _vnPayService.ValidateSignature(queryParams, vnpSecureHash);
        
        if (!isValidSignature)
        {
            TempData["Error"] = "Chữ ký không hợp lệ!";
            return RedirectToAction("Index", "Orders");
        }

        // Lấy thông tin giao dịch
        string vnpResponseCode = queryParams["vnp_ResponseCode"]!;
        string vnpTransactionNo = queryParams["vnp_TransactionNo"]!;
        string vnpTxnRef = queryParams["vnp_TxnRef"]!;
        string vnpAmount = queryParams["vnp_Amount"]!;
        
        int orderId = int.Parse(vnpTxnRef);
        var order = await _context.Orders.FindAsync(orderId);
        
        if (order == null)
        {
            TempData["Error"] = "Không tìm thấy đơn hàng!";
            return RedirectToAction("Index", "Orders");
        }

        // Kiểm tra kết quả thanh toán
        if (vnpResponseCode == "00")
        {
            // Thanh toán thành công
            // Cập nhật trạng thái đơn hàng từ "Chờ thanh toán" sang "Chờ xác nhận"
            order.Status = "Chờ xác nhận";
            
            // Cập nhật số lượng tồn kho
            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .ToListAsync();
                
            foreach (var item in orderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Quantity -= item.Quantity;
                }
            }
            
            await _context.SaveChangesAsync();
            
            // Tạo thông báo cho khách hàng
            var notification = new ClothingShop.Models.Notification
            {
                UserId = order.UserId,
                Title = "Thanh toán thành công",
                Message = $"Đơn hàng #{order.Id:D6} đã được thanh toán thành công qua VNPay. Mã giao dịch: {vnpTransactionNo}",
                Type = "success",
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            // Xóa session giỏ hàng
            var cartService = HttpContext.RequestServices.GetRequiredService<ClothingShop.Services.ICartService>();
            cartService.ClearCart();
            
            // Xóa session "Mua Ngay" nếu có
            HttpContext.Session.Remove("BuyNowProductId");
            HttpContext.Session.Remove("BuyNowQuantity");
            HttpContext.Session.Remove("BuyNowSize");
            HttpContext.Session.Remove("BuyNowColor");
            
            // Xóa session pending order
            HttpContext.Session.Remove("PendingOrder_Id");
            HttpContext.Session.Remove("PendingOrder_FullName");
            HttpContext.Session.Remove("PendingOrder_PhoneNumber");
            HttpContext.Session.Remove("PendingOrder_Address");
            HttpContext.Session.Remove("PendingOrder_Note");
            HttpContext.Session.Remove("PendingOrder_PaymentMethod");
            HttpContext.Session.Remove("PendingOrder_TotalAmount");
            
            TempData["Success"] = $"Thanh toán thành công! Mã giao dịch: {vnpTransactionNo}";
            return RedirectToAction("OrderSuccess", "Orders", new { id = orderId });
        }
        else
        {
            // Thanh toán thất bại - Xóa đơn hàng tạm
            if (order.Status == "Chờ thanh toán")
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }
            
            // Thanh toán thất bại
            string errorMessage = vnpResponseCode switch
            {
                "07" => "Trừ tiền thành công. Giao dịch bị nghi ngờ (liên quan tới lừa đảo, giao dịch bất thường).",
                "09" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng.",
                "10" => "Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần",
                "11" => "Giao dịch không thành công do: Đã hết hạn chờ thanh toán. Xin quý khách vui lòng thực hiện lại giao dịch.",
                "12" => "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bị khóa.",
                "13" => "Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP).",
                "24" => "Giao dịch không thành công do: Khách hàng hủy giao dịch",
                "51" => "Giao dịch không thành công do: Tài khoản của quý khách không đủ số dư để thực hiện giao dịch.",
                "65" => "Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày.",
                "75" => "Ngân hàng thanh toán đang bảo trì.",
                "79" => "Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá số lần quy định.",
                _ => "Giao dịch không thành công!"
            };
            
            TempData["Error"] = errorMessage;
            return RedirectToAction("Checkout", "Orders");
        }
    }
}

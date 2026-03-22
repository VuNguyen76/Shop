using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using ClothingShop.Models;
using ClothingShop.Services;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers
{
    // Class để deserialize selectedItems từ JSON
    public class SelectedCartItem
    {
        public string ProductId { get; set; } = "";
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public string Quantity { get; set; } = "";
    }

    public class OrdersController(ApplicationDbContext context, ICartService cartService) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ICartService _cartService = cartService;

        // GET: Danh sách đơn hàng
        public async Task<IActionResult> Index()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Trang Checkout
        public async Task<IActionResult> Checkout(string? selectedItems)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                TempData["Error"] = "Vui lòng đăng nhập để thanh toán";
                return RedirectToAction("Login", "Account", new { returnUrl = "/Orders/Checkout" });
            }

            List<CartItem> cartItems;
            decimal totalPrice;

            // Kiểm tra xem có phải "Mua Ngay" không
            var buyNowProductId = HttpContext.Session.GetInt32("BuyNowProductId");
            if (buyNowProductId.HasValue)
            {
                // Xử lý "Mua Ngay" - tạo giỏ hàng tạm thời
                var quantity = HttpContext.Session.GetInt32("BuyNowQuantity") ?? 1;
                var size = HttpContext.Session.GetString("BuyNowSize");
                var color = HttpContext.Session.GetString("BuyNowColor");

                var product = await _context.Products.FindAsync(buyNowProductId.Value);
                if (product == null)
                {
                    TempData["Error"] = "Sản phẩm không tồn tại";
                    return RedirectToAction("Index", "Home");
                }

                cartItems =
                [
                    new()
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Price = product.Price,
                        Quantity = quantity,
                        ImageUrl = product.ImageUrl,
                        Size = size,
                        Color = color
                    }
                ];
                totalPrice = product.Price * quantity;
                ViewBag.IsBuyNow = true;
            }
            else
            {
                // Xử lý giỏ hàng thông thường
                var allCartItems = _cartService.GetCartItems();
                if (allCartItems.Count == 0)
                {
                    TempData["Error"] = "Giỏ hàng trống";
                    return RedirectToAction("Index", "Cart");
                }
                
                // Nếu có selectedItems, chỉ lấy các sản phẩm đã chọn
                if (!string.IsNullOrEmpty(selectedItems))
                {
                    try
                    {
                        var selectedItemsList = System.Text.Json.JsonSerializer.Deserialize<List<SelectedCartItem>>(selectedItems);
                        if (selectedItemsList != null && selectedItemsList.Count > 0)
                        {
                            cartItems = [];
                            foreach (var selected in selectedItemsList)
                            {
                                var matchedItem = allCartItems.FirstOrDefault(item => 
                                    item.ProductId.ToString() == selected.ProductId && 
                                    item.Size == selected.Size && 
                                    item.Color == selected.Color
                                );
                                
                                if (matchedItem != null)
                                {
                                    // Tạo bản sao và cập nhật số lượng
                                    var cartItem = new CartItem
                                    {
                                        ProductId = matchedItem.ProductId,
                                        Name = matchedItem.Name,
                                        Price = matchedItem.Price,
                                        ImageUrl = matchedItem.ImageUrl,
                                        Size = matchedItem.Size,
                                        Color = matchedItem.Color,
                                        Quantity = int.TryParse(selected.Quantity, out int qty) ? qty : matchedItem.Quantity
                                    };
                                    cartItems.Add(cartItem);
                                }
                            }
                            
                            if (cartItems.Count == 0)
                            {
                                cartItems = allCartItems;
                            }
                        }
                        else
                        {
                            cartItems = allCartItems;
                        }
                    }
                    catch
                    {
                        cartItems = allCartItems;
                    }
                }
                else
                {
                    cartItems = allCartItems;
                }
                
                totalPrice = cartItems.Sum(item => item.Price * item.Quantity);
                ViewBag.IsBuyNow = false;
            }

            ViewBag.CartItems = cartItems;
            ViewBag.TotalPrice = totalPrice;

            // Lấy thông tin user
            var userId = int.Parse(userIdString);
            var user = _context.Users.Find(userId);
            ViewBag.User = user;

            // Lấy thông tin thanh toán
            var paymentInfo = await _context.PaymentInfos.FirstOrDefaultAsync();
            if (paymentInfo == null)
            {
                paymentInfo = new PaymentInfo();
            }
            ViewBag.PaymentInfo = paymentInfo;

            return View();
        }

        // POST: Xử lý đặt hàng
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string fullName, string phoneNumber, string address, string? note, string? paymentMethod)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            List<CartItem> cartItems;
            decimal totalPrice;

            // Kiểm tra xem có phải "Mua Ngay" không
            var buyNowProductId = HttpContext.Session.GetInt32("BuyNowProductId");
            if (buyNowProductId.HasValue)
            {
                // Xử lý "Mua Ngay"
                var quantity = HttpContext.Session.GetInt32("BuyNowQuantity") ?? 1;
                var size = HttpContext.Session.GetString("BuyNowSize");
                var color = HttpContext.Session.GetString("BuyNowColor");

                var product = await _context.Products.FindAsync(buyNowProductId.Value);
                if (product == null)
                {
                    TempData["Error"] = "Sản phẩm không tồn tại";
                    return RedirectToAction("Index", "Home");
                }

                if (product.Quantity < quantity)
                {
                    TempData["Error"] = $"Sản phẩm {product.Name} không đủ số lượng";
                    return RedirectToAction("Checkout");
                }

                cartItems =
                [
                    new()
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Price = product.Price,
                        Quantity = quantity,
                        ImageUrl = product.ImageUrl,
                        Size = size,
                        Color = color
                    }
                ];
                totalPrice = product.Price * quantity;
            }
            else
            {
                // Xử lý giỏ hàng thông thường
                cartItems = _cartService.GetCartItems();
                if (cartItems.Count == 0)
                {
                    TempData["Error"] = "Giỏ hàng trống";
                    return RedirectToAction("Index", "Cart");
                }
                totalPrice = _cartService.GetTotalPrice();

                // Kiểm tra số lượng tồn kho
                foreach (var item in cartItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.Quantity < item.Quantity)
                    {
                        TempData["Error"] = $"Sản phẩm {item.Name} không đủ số lượng";
                        return RedirectToAction("Checkout");
                    }
                }
            }

            // Tính phí vận chuyển
            decimal shippingFee = totalPrice < 500000 ? 20000 : 0;
            decimal finalTotal = totalPrice + shippingFee;

            // Nếu chọn VNPay, lưu thông tin vào session và redirect đến VNPay TRƯỚC KHI tạo đơn
            if (paymentMethod == "VNPay")
            {
                // Lưu thông tin đơn hàng vào session
                HttpContext.Session.SetString("PendingOrder_FullName", fullName);
                HttpContext.Session.SetString("PendingOrder_PhoneNumber", phoneNumber);
                HttpContext.Session.SetString("PendingOrder_Address", address);
                HttpContext.Session.SetString("PendingOrder_Note", note ?? "");
                HttpContext.Session.SetString("PendingOrder_PaymentMethod", paymentMethod);
                HttpContext.Session.SetString("PendingOrder_TotalAmount", finalTotal.ToString());
                
                // Tạo đơn hàng tạm để lấy orderId cho VNPay
                var tempOrder = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    TotalAmount = finalTotal,
                    Status = "Chờ thanh toán", // Trạng thái tạm
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    Address = address,
                    Note = note,
                    Items = [.. cartItems.Select(item =>
                    {
                        var product = _context.Products.Find(item.ProductId);
                        return new OrderItem
                        {
                            ProductId = item.ProductId,
                            ProductName = item.Name,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            Cost = product?.Cost, // Lưu giá nhập để tính lợi nhuận
                            Size = item.Size,
                            Color = item.Color
                        };
                    })]
                };
                
                _context.Orders.Add(tempOrder);
                await _context.SaveChangesAsync();
                
                // Lưu orderId vào session
                HttpContext.Session.SetInt32("PendingOrder_Id", tempOrder.Id);
                
                return RedirectToAction("VNPay", "Payment", new { orderId = tempOrder.Id });
            }

            // Với COD hoặc Bank Transfer, tạo đơn hàng ngay
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                TotalAmount = finalTotal,
                Status = "Chờ xác nhận",
                FullName = fullName,
                PhoneNumber = phoneNumber,
                Address = address,
                Note = note,
                Items = [.. cartItems.Select(item =>
                {
                    var product = _context.Products.Find(item.ProductId);
                    return new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Name,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        Cost = product?.Cost, // Lưu giá nhập để tính lợi nhuận
                        Size = item.Size,
                        Color = item.Color
                    };
                })]
            };

            _context.Orders.Add(order);

            // Cập nhật số lượng tồn kho
            foreach (var item in cartItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.Quantity -= item.Quantity;
                }
            }

            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notification = new Notification
            {
                UserId = userId,
                Title = "Đặt hàng thành công",
                Message = $"Đơn hàng #{order.Id:D6} của bạn đã được đặt thành công. Chúng tôi sẽ xác nhận đơn hàng sớm nhất.",
                Type = "success",
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Xóa session "Mua Ngay" hoặc giỏ hàng
            if (buyNowProductId.HasValue)
            {
                HttpContext.Session.Remove("BuyNowProductId");
                HttpContext.Session.Remove("BuyNowQuantity");
                HttpContext.Session.Remove("BuyNowSize");
                HttpContext.Session.Remove("BuyNowColor");
            }
            else
            {
                _cartService.ClearCart();
            }

            TempData["Success"] = "Đặt hàng thành công!";
            return RedirectToAction("OrderSuccess", new { id = order.Id });
        }

        // GET: Trang đặt hàng thành công
        public async Task<IActionResult> OrderSuccess(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Chi tiết đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Hủy đơn hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string cancelReason)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Chỉ cho phép hủy khi "Chờ xác nhận" hoặc "Chờ lấy hàng"
            if (order.Status != "Chờ xác nhận" && order.Status != "Chờ lấy hàng")
            {
                TempData["Error"] = "Không thể hủy đơn hàng này. Chỉ có thể hủy khi đơn hàng đang chờ xác nhận hoặc chờ lấy hàng.";
                return RedirectToAction("Details", new { id });
            }

            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đơn!";
                return RedirectToAction("Details", new { id });
            }

            order.Status = "Đã hủy";
            order.CancelReason = cancelReason;
            order.CancelledAt = DateTime.Now;
            order.CancelledBy = "Customer";

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
                UserId = userId,
                Title = "Đơn hàng đã được hủy",
                Message = $"Đơn hàng #{order.Id:D6} của bạn đã được hủy thành công.",
                Type = "warning",
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã hủy đơn hàng thành công";
            return RedirectToAction("Index");
        }

        // POST: Xác nhận đã nhận hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int orderId)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdString);
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Chỉ cho phép xác nhận khi đơn hàng đang "Chờ giao hàng"
            if (order.Status != "Chờ giao hàng")
            {
                TempData["Error"] = "Không thể xác nhận đơn hàng này. Chỉ có thể xác nhận khi đơn hàng đang được giao.";
                return RedirectToAction("Details", new { id = orderId });
            }

            // Cập nhật trạng thái đơn hàng
            order.Status = "Đã giao";

            await _context.SaveChangesAsync();

            // Tạo thông báo cho khách hàng
            var notification = new Notification
            {
                UserId = userId,
                Title = "Đơn hàng đã hoàn thành",
                Message = $"Cảm ơn bạn đã xác nhận nhận hàng cho đơn hàng #{order.Id:D6}. Hãy đánh giá sản phẩm để chia sẻ trải nghiệm của bạn!",
                Type = "success",
                OrderId = order.Id,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cảm ơn bạn đã xác nhận! Đơn hàng đã hoàn thành. Bạn có thể đánh giá sản phẩm ngay bây giờ.";
            return RedirectToAction("Details", new { id = orderId });
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using ClothingShop.Services;


namespace ClothingShop.Controllers
{
    public class CartController(ICartService cartService) : Controller
    {
        private readonly ICartService _cartService = cartService;

        // GET: /Cart
        public IActionResult Index()
        {
            var cartItems = _cartService.GetCartItems();
            ViewBag.TotalPrice = _cartService.GetTotalPrice();
            ViewBag.CartCount = _cartService.GetTotalItems();
            return View(cartItems);
        }

        // POST: /Cart/Add
        [HttpPost]
        public IActionResult Add(int productId, int quantity = 1, string? selectedSize = null, string? selectedColor = null)
        {
            _cartService.AddToCart(productId, quantity, selectedSize, selectedColor);
            TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng!";
            return RedirectToAction("Index");
        }

        // POST: /Cart/Update
        [HttpPost]
        public IActionResult Update(int productId, int quantity, string? size = null, string? color = null)
        {
            if (quantity <= 0)
            {
                _cartService.RemoveFromCart(productId, size, color);
            }
            else
            {
                _cartService.UpdateQuantity(productId, quantity, size, color);
            }
            return RedirectToAction("Index");
        }

        // POST: /Cart/Remove
        [HttpPost]
        public IActionResult Remove(int productId, string? size = null, string? color = null)
        {
            _cartService.RemoveFromCart(productId, size, color);
            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
            return RedirectToAction("Index");
        }

        // POST: /Cart/Clear
        [HttpPost]
        public IActionResult Clear()
        {
            _cartService.ClearCart();
            TempData["Success"] = "Đã làm trống giỏ hàng.";
            return RedirectToAction("Index");
        }

        // POST: /Cart/BuyNow - Tạo session riêng và chuyển đến trang thanh toán
        [HttpPost]
        public IActionResult BuyNow(int productId, int quantity = 1, string? selectedSize = null, string? selectedColor = null)
        {
            // Lưu thông tin "Mua Ngay" vào session riêng (không thêm vào giỏ hàng)
            HttpContext.Session.SetInt32("BuyNowProductId", productId);
            HttpContext.Session.SetInt32("BuyNowQuantity", quantity);
            HttpContext.Session.SetString("BuyNowSize", selectedSize ?? "");
            HttpContext.Session.SetString("BuyNowColor", selectedColor ?? "");
            
            // Chuyển thẳng đến trang checkout
            return RedirectToAction("Checkout", "Orders");
        }

        // Alias cho Add để tương thích
        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            return Add(productId, quantity);
        }

        // API: Get cart items as JSON
        [HttpGet]
        public IActionResult GetCartItems()
        {
            var cartItems = _cartService.GetCartItems();
            var total = _cartService.GetTotalPrice();
            
            return Json(new
            {
                items = cartItems.Select(item => new
                {
                    item.ProductId,
                    item.Name,
                    item.Price,
                    item.Quantity,
                    item.ImageUrl,
                    item.Color,
                    item.Size
                }),
                total
            });
        }

        // API: Get cart count
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var count = _cartService.GetTotalItems();
            return Json(new { count });
        }

        // API: Update quantity via JSON
        [HttpPost]
        public IActionResult UpdateQuantity([FromBody] UpdateQuantityRequest request)
        {
            if (request.Quantity <= 0)
            {
                _cartService.RemoveFromCart(request.ProductId, request.Size, request.Color);
            }
            else
            {
                _cartService.UpdateQuantity(request.ProductId, request.Quantity, request.Size, request.Color);
            }
            return Json(new { success = true });
        }

        // API: Remove item via JSON
        [HttpPost]
        public IActionResult RemoveItem([FromBody] RemoveItemRequest request)
        {
            _cartService.RemoveFromCart(request.ProductId, request.Size, request.Color);
            return Json(new { success = true });
        }

        public class UpdateQuantityRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public string? Size { get; set; }
            public string? Color { get; set; }
        }

        public class RemoveItemRequest
        {
            public int ProductId { get; set; }
            public string? Size { get; set; }
            public string? Color { get; set; }
        }
    }
}
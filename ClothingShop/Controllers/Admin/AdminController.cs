using ClothingShop.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin")]
    public class AdminController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: /Admin - Dashboard
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // === THỐNG KÊ CƠ BẢN ===
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Chờ xác nhận");
            ViewBag.ActiveCustomers = await _context.Users
                .Where(u => u.CreatedAt > DateTime.Now.AddDays(-30))
                .CountAsync();

            // === DOANH SỐ, CHI PHÍ, LỢI NHUẬN 7 NGÀY GẦN NHẤT ===
            var sevenDaysAgo = DateTime.Today.AddDays(-6);
            
            // Tính doanh số và chi phí từ OrderItems
            var financialData = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= sevenDaysAgo && oi.Order.Status == "Đã giao")
                .GroupBy(oi => oi.Order.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(oi => oi.Quantity * oi.Price), // Tổng doanh số
                    Cost = g.Sum(oi => oi.Quantity * (oi.Cost ?? 0)) // Tổng chi phí
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var labels = new List<string>();
            var revenues = new List<decimal>();
            var costs = new List<decimal>();
            var profits = new List<decimal>();
            
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                labels.Add(date.ToString("dd/MM"));
                var data = financialData.FirstOrDefault(x => x.Date == date.Date);
                var revenue = data?.Revenue ?? 0;
                var cost = data?.Cost ?? 0;
                var profit = revenue - cost;
                
                revenues.Add(revenue);
                costs.Add(cost);
                profits.Add(profit);
            }

            ViewBag.RevenueLabels = labels;
            ViewBag.RevenueData = revenues;
            ViewBag.CostData = costs;
            ViewBag.ProfitData = profits;
            ViewBag.TotalRevenue = revenues.Sum();
            ViewBag.TotalCost = costs.Sum();
            ViewBag.TotalProfit = profits.Sum();

            return View("~/Views/Admin/Index.cshtml");
        }


    }
}

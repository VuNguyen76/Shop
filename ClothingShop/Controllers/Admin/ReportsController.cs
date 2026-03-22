using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClothingShop.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ClothingShop.Controllers.Admin
{
    [Authorize(Policy = "AdminOnly")]
    [Route("Admin/Reports")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
            // Cấu hình EPPlus license
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ==================== BÁO CÁO NÂNG CAO ====================
        
        // GET: /Admin/Reports/AdvancedStatistics
        [HttpGet("AdvancedStatistics")]
        public async Task<IActionResult> AdvancedStatistics(string period = "month", int? year = null, int? month = null, int? quarter = null)
        {
            year ??= DateTime.Now.Year;
            month ??= DateTime.Now.Month;
            quarter ??= (DateTime.Now.Month - 1) / 3 + 1;

            ViewBag.Period = period;
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.Quarter = quarter;

            DateTime startDate, endDate;

            switch (period)
            {
                case "month":
                    startDate = new DateTime(year.Value, month.Value, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    ViewBag.PeriodName = $"Tháng {month}/{year}";
                    break;
                case "quarter":
                    startDate = new DateTime(year.Value, (quarter.Value - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3).AddDays(-1);
                    ViewBag.PeriodName = $"Quý {quarter}/{year}";
                    break;
                case "year":
                    startDate = new DateTime(year.Value, 1, 1);
                    endDate = new DateTime(year.Value, 12, 31);
                    ViewBag.PeriodName = $"Năm {year}";
                    break;
                default:
                    startDate = DateTime.Now.AddDays(-30);
                    endDate = DateTime.Now;
                    ViewBag.PeriodName = "30 ngày gần nhất";
                    break;
            }

            // Thống kê tổng quan
            var orders = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .ToListAsync();

            // Tính doanh số, chi phí, lợi nhuận từ OrderItems
            var financialData = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= startDate && 
                            oi.Order.OrderDate <= endDate && 
                            oi.Order.Status == "Đã giao")
                .GroupBy(oi => 1)
                .Select(g => new
                {
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price),
                    TotalCost = g.Sum(oi => oi.Quantity * (oi.Cost ?? 0))
                })
                .FirstOrDefaultAsync();

            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalRevenue = financialData?.TotalRevenue ?? 0;
            ViewBag.TotalCost = financialData?.TotalCost ?? 0;
            ViewBag.TotalProfit = ViewBag.TotalRevenue - ViewBag.TotalCost;
            ViewBag.ProfitMargin = ViewBag.TotalRevenue > 0 
                ? (ViewBag.TotalProfit / ViewBag.TotalRevenue * 100) 
                : 0;
            ViewBag.PendingOrders = orders.Count(o => o.Status == "Chờ xác nhận");
            ViewBag.CompletedOrders = orders.Count(o => o.Status == "Đã giao");
            ViewBag.CancelledOrders = orders.Count(o => o.Status == "Đã hủy");
            ViewBag.AverageOrderValue = ViewBag.CompletedOrders > 0 
                ? ViewBag.TotalRevenue / ViewBag.CompletedOrders 
                : 0;

            // Sản phẩm bán chạy (7 ngày gần nhất, theo số lượng)
            var last7Days = DateTime.Now.AddDays(-7);
            var bestSellers = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= last7Days && 
                            oi.Order.Status == "Đã giao")
                .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price),
                    TotalCost = g.Sum(oi => oi.Quantity * (oi.Cost ?? 0)),
                    TotalProfit = g.Sum(oi => oi.Quantity * oi.Price) - g.Sum(oi => oi.Quantity * (oi.Cost ?? 0))
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(10)
                .ToListAsync();

            ViewBag.BestSellers = bestSellers;

            // Doanh thu, chi phí, lợi nhuận theo ngày (cho biểu đồ)
            var dailyFinancial = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= startDate && 
                            oi.Order.OrderDate <= endDate && 
                            oi.Order.Status == "Đã giao")
                .GroupBy(oi => oi.Order.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(oi => oi.Quantity * oi.Price),
                    Cost = g.Sum(oi => oi.Quantity * (oi.Cost ?? 0)),
                    Profit = g.Sum(oi => oi.Quantity * oi.Price) - g.Sum(oi => oi.Quantity * (oi.Cost ?? 0)),
                    OrderCount = g.Select(oi => oi.OrderId).Distinct().Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            ViewBag.DailyFinancial = dailyFinancial;

            // === DOANH THU THEO DANH MỤC (7 ngày gần nhất) ===
            var revenueByCategory = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= last7Days && 
                            oi.Order.Status == "Đã giao")
                .Join(_context.Products, oi => oi.ProductId, p => p.Id, (oi, p) => new { oi, p.Category })
                .GroupBy(x => x.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Revenue = g.Sum(x => x.oi.Quantity * x.oi.Price)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToListAsync();
            ViewBag.CategoryLabels = revenueByCategory.Select(x => x.Category).ToList();
            ViewBag.CategoryData = revenueByCategory.Select(x => x.Revenue).ToList();

            // === TRẠNG THÁI ĐỚN HÀNG ===
            var ordersByStatus = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.StatusLabels = ordersByStatus.Select(x => x.Status).ToList();
            ViewBag.StatusData = ordersByStatus.Select(x => x.Count).ToList();

            // Khách hàng mới
            var newCustomers = await _context.Users
                .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate && !u.IsAdmin)
                .CountAsync();

            ViewBag.NewCustomers = newCustomers;

            return View();
        }

        // ==================== EXPORT EXCEL ====================
        
        // GET: /Admin/Reports/ExportRevenueExcel
        [HttpGet("ExportRevenueExcel")]
        public async Task<IActionResult> ExportRevenueExcel(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == "Đã giao")
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Báo cáo doanh thu");

            // Header
            worksheet.Cells["A1"].Value = "BÁO CÁO TÀI CHÍNH";
            worksheet.Cells["A1:J1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            worksheet.Cells["A2"].Value = $"Từ ngày: {startDate:dd/MM/yyyy} - Đến ngày: {endDate:dd/MM/yyyy}";
            worksheet.Cells["A2:J2"].Merge = true;
            worksheet.Cells["A2"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            // Column headers
            worksheet.Cells["A4"].Value = "Mã ĐH";
            worksheet.Cells["B4"].Value = "Ngày đặt";
            worksheet.Cells["C4"].Value = "Khách hàng";
            worksheet.Cells["D4"].Value = "Email";
            worksheet.Cells["E4"].Value = "Số lượng SP";
            worksheet.Cells["F4"].Value = "Doanh số";
            worksheet.Cells["G4"].Value = "Chi phí";
            worksheet.Cells["H4"].Value = "Lợi nhuận";
            worksheet.Cells["I4"].Value = "Tỷ suất (%)";
            worksheet.Cells["J4"].Value = "Trạng thái";

            worksheet.Cells["A4:J4"].Style.Font.Bold = true;
            worksheet.Cells["A4:J4"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells["A4:J4"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            // Data
            int row = 5;
            decimal totalRevenue = 0, totalCost = 0, totalProfit = 0;
            
            foreach (var order in orders)
            {
                var revenue = order.Items.Sum(i => i.Quantity * i.Price);
                var cost = order.Items.Sum(i => i.Quantity * (i.Cost ?? 0));
                var profit = revenue - cost;
                var margin = revenue > 0 ? (profit / revenue * 100) : 0;
                
                worksheet.Cells[row, 1].Value = $"#{order.Id:D6}";
                worksheet.Cells[row, 2].Value = order.OrderDate.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[row, 3].Value = order.User?.FullName ?? "N/A";
                worksheet.Cells[row, 4].Value = order.User?.Email ?? "N/A";
                worksheet.Cells[row, 5].Value = order.Items.Sum(i => i.Quantity);
                worksheet.Cells[row, 6].Value = revenue;
                worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 7].Value = cost;
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 8].Value = profit;
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 9].Value = margin;
                worksheet.Cells[row, 9].Style.Numberformat.Format = "0.0%";
                worksheet.Cells[row, 10].Value = order.Status;
                
                totalRevenue += revenue;
                totalCost += cost;
                totalProfit += profit;
                row++;
            }

            // Tổng cộng
            worksheet.Cells[row, 5].Value = "TỔNG CỘNG:";
            worksheet.Cells[row, 5].Style.Font.Bold = true;
            worksheet.Cells[row, 6].Value = totalRevenue;
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0 ₫";
            worksheet.Cells[row, 6].Style.Font.Bold = true;
            worksheet.Cells[row, 7].Value = totalCost;
            worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0 ₫";
            worksheet.Cells[row, 7].Style.Font.Bold = true;
            worksheet.Cells[row, 8].Value = totalProfit;
            worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0 ₫";
            worksheet.Cells[row, 8].Style.Font.Bold = true;
            worksheet.Cells[row, 9].Value = totalRevenue > 0 ? (totalProfit / totalRevenue) : 0;
            worksheet.Cells[row, 9].Style.Numberformat.Format = "0.0%";
            worksheet.Cells[row, 9].Style.Font.Bold = true;

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"BaoCaoTaiChinh_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: /Admin/Reports/ExportOrdersExcel
        [HttpGet("ExportOrdersExcel")]
        public async Task<IActionResult> ExportOrdersExcel(DateTime? startDate, DateTime? endDate, string? status)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Danh sách đơn hàng");

            // Header
            worksheet.Cells["A1"].Value = "DANH SÁCH ĐỚN HÀNG";
            worksheet.Cells["A1:H1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            worksheet.Cells["A2"].Value = $"Từ ngày: {startDate:dd/MM/yyyy} - Đến ngày: {endDate:dd/MM/yyyy}";
            worksheet.Cells["A2:H2"].Merge = true;
            worksheet.Cells["A2"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            // Column headers
            worksheet.Cells["A4"].Value = "Mã ĐH";
            worksheet.Cells["B4"].Value = "Ngày đặt";
            worksheet.Cells["C4"].Value = "Khách hàng";
            worksheet.Cells["D4"].Value = "SĐT";
            worksheet.Cells["E4"].Value = "Email";
            worksheet.Cells["F4"].Value = "Số SP";
            worksheet.Cells["G4"].Value = "Tổng tiền";
            worksheet.Cells["H4"].Value = "Trạng thái";

            worksheet.Cells["A4:H4"].Style.Font.Bold = true;
            worksheet.Cells["A4:H4"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells["A4:H4"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            // Data
            int row = 5;
            foreach (var order in orders)
            {
                worksheet.Cells[row, 1].Value = $"#{order.Id:D6}";
                worksheet.Cells[row, 2].Value = order.OrderDate.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[row, 3].Value = order.User?.FullName ?? "N/A";
                worksheet.Cells[row, 4].Value = order.User?.PhoneNumber ?? "N/A";
                worksheet.Cells[row, 5].Value = order.User?.Email ?? "N/A";
                worksheet.Cells[row, 6].Value = order.Items.Sum(i => i.Quantity);
                worksheet.Cells[row, 7].Value = order.TotalAmount;
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 8].Value = order.Status;
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"DanhSachDonHang_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: /Admin/Reports/ExportBestSellersExcel
        [HttpGet("ExportBestSellersExcel")]
        public async Task<IActionResult> ExportBestSellersExcel()
        {
            var last7Days = DateTime.Now.AddDays(-7);
            var endDate = DateTime.Now;

            var bestSellers = await _context.OrderItems
                .Where(oi => oi.Order.OrderDate >= last7Days && 
                            oi.Order.Status == "Đã giao")
                .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price),
                    TotalCost = g.Sum(oi => oi.Quantity * (oi.Cost ?? 0)),
                    TotalProfit = g.Sum(oi => oi.Quantity * oi.Price) - g.Sum(oi => oi.Quantity * (oi.Cost ?? 0))
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(10)
                .ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Sản phẩm bán chạy");

            // Header
            worksheet.Cells["A1"].Value = "TOP 10 SẢN PHẨM BÁN CHẠY NHẤT";
            worksheet.Cells["A1:H1"].Merge = true;
            worksheet.Cells["A1"].Style.Font.Size = 16;
            worksheet.Cells["A1"].Style.Font.Bold = true;
            worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            worksheet.Cells["A2"].Value = $"7 ngày gần nhất (Từ {last7Days:dd/MM/yyyy} - {endDate:dd/MM/yyyy})";
            worksheet.Cells["A2:H2"].Merge = true;
            worksheet.Cells["A2"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            // Column headers
            worksheet.Cells["A4"].Value = "Xếp hạng";
            worksheet.Cells["B4"].Value = "Mã SP";
            worksheet.Cells["C4"].Value = "Tên sản phẩm";
            worksheet.Cells["D4"].Value = "Số lượng bán";
            worksheet.Cells["E4"].Value = "Doanh thu";
            worksheet.Cells["F4"].Value = "Chi phí";
            worksheet.Cells["G4"].Value = "Lợi nhuận";
            worksheet.Cells["H4"].Value = "Tỷ suất (%)";

            worksheet.Cells["A4:H4"].Style.Font.Bold = true;
            worksheet.Cells["A4:H4"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells["A4:H4"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            // Data
            int row = 5;
            int rank = 1;
            foreach (var item in bestSellers)
            {
                var margin = item.TotalRevenue > 0 ? (item.TotalProfit / item.TotalRevenue * 100) : 0;
                
                worksheet.Cells[row, 1].Value = rank++;
                worksheet.Cells[row, 2].Value = item.ProductId;
                worksheet.Cells[row, 3].Value = item.ProductName;
                worksheet.Cells[row, 4].Value = item.TotalQuantity;
                worksheet.Cells[row, 5].Value = item.TotalRevenue;
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 6].Value = item.TotalCost;
                worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 7].Value = item.TotalProfit;
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0 ₫";
                worksheet.Cells[row, 8].Value = margin;
                worksheet.Cells[row, 8].Style.Numberformat.Format = "0.0%";
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Top10SanPhamBanChay_7NgayGanNhat_{endDate:yyyyMMdd}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ==================== EXPORT PDF ====================
        
        // GET: /Admin/Reports/ExportRevenuePdf
        [HttpGet("ExportRevenuePdf")]
        public async Task<IActionResult> ExportRevenuePdf(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Now.AddMonths(-1);
            endDate ??= DateTime.Now;

            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == "Đã giao")
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var stream = new MemoryStream();
            var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30); // Landscape mode
            PdfWriter.GetInstance(document, stream);
            document.Open();

            // Font cho tiếng Việt (sử dụng Arial Unicode MS nếu có)
            var baseFont = BaseFont.CreateFont("c:\\windows\\fonts\\arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var titleFont = new Font(baseFont, 18, Font.BOLD);
            var headerFont = new Font(baseFont, 10, Font.BOLD);
            var normalFont = new Font(baseFont, 9, Font.NORMAL);

            // Title
            var title = new Paragraph("BÁO CÁO TÀI CHÍNH", titleFont)
            {
                Alignment = Element.ALIGN_CENTER
            };
            document.Add(title);

            var period = new Paragraph($"Từ ngày: {startDate:dd/MM/yyyy} - Đến ngày: {endDate:dd/MM/yyyy}", normalFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(period);

            // Table
            var table = new PdfPTable(9) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 8, 12, 18, 12, 8, 12, 12, 12, 10 });

            // Headers
            AddCell(table, "Mã ĐH", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Ngày đặt", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Khách hàng", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Email", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "SL", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Doanh số", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Chi phí", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Lợi nhuận", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "Tỷ suất", headerFont, Element.ALIGN_CENTER);

            // Data
            decimal totalRevenue = 0, totalCost = 0, totalProfit = 0;
            
            foreach (var order in orders)
            {
                var revenue = order.Items.Sum(i => i.Quantity * i.Price);
                var cost = order.Items.Sum(i => i.Quantity * (i.Cost ?? 0));
                var profit = revenue - cost;
                var margin = revenue > 0 ? (profit / revenue * 100) : 0;
                
                AddCell(table, $"#{order.Id:D6}", normalFont, Element.ALIGN_CENTER);
                AddCell(table, order.OrderDate.ToString("dd/MM/yyyy"), normalFont, Element.ALIGN_CENTER);
                AddCell(table, order.User?.FullName ?? "N/A", normalFont, Element.ALIGN_LEFT);
                AddCell(table, order.User?.Email ?? "N/A", normalFont, Element.ALIGN_LEFT);
                AddCell(table, order.Items.Sum(i => i.Quantity).ToString(), normalFont, Element.ALIGN_CENTER);
                AddCell(table, $"{revenue:N0}₫", normalFont, Element.ALIGN_RIGHT);
                AddCell(table, $"{cost:N0}₫", normalFont, Element.ALIGN_RIGHT);
                AddCell(table, $"{profit:N0}₫", normalFont, Element.ALIGN_RIGHT);
                AddCell(table, $"{margin:N1}%", normalFont, Element.ALIGN_RIGHT);
                
                totalRevenue += revenue;
                totalCost += cost;
                totalProfit += profit;
            }

            // Total
            AddCell(table, "", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "", headerFont, Element.ALIGN_CENTER);
            AddCell(table, "TỔNG:", headerFont, Element.ALIGN_RIGHT);
            AddCell(table, $"{totalRevenue:N0}₫", headerFont, Element.ALIGN_RIGHT);
            AddCell(table, $"{totalCost:N0}₫", headerFont, Element.ALIGN_RIGHT);
            AddCell(table, $"{totalProfit:N0}₫", headerFont, Element.ALIGN_RIGHT);
            var totalMargin = totalRevenue > 0 ? (totalProfit / totalRevenue * 100) : 0;
            AddCell(table, $"{totalMargin:N1}%", headerFont, Element.ALIGN_RIGHT);

            document.Add(table);
            document.Close();

            stream.Position = 0;
            var fileName = $"BaoCaoTaiChinh_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            return File(stream, "application/pdf", fileName);
        }

        private static void AddCell(PdfPTable table, string text, Font font, int alignment)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = alignment,
                Padding = 5
            };
            table.AddCell(cell);
        }
    }
}

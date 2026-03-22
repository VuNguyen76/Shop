    using ClothingShop.Data;
    using ClothingShop.Models;
    using ClothingShop.Services;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;

    namespace ClothingShop.Controllers
    {
        public class AccountController(ApplicationDbContext context, IEmailService emailService) : Controller
        {
            private readonly ApplicationDbContext _context = context;
            private readonly IEmailService _emailService = emailService;

            // [GET] /Account/Login
            [HttpGet]
            public IActionResult Login(string? returnUrl)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // [POST] /Account/Login
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Login(string Email, string Password, string? ReturnUrl)
            {
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    TempData["Error"] = "Vui lòng nhập email và mật khẩu!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                Email = Email.Trim().ToLower();
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == Email);

                if (user == null || !VerifyPassword(Password, user.PasswordHash))
                {
                    TempData["Error"] = "Email hoặc mật khẩu không đúng!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                // Kiểm tra tài khoản có bị khóa không (Admin luôn được phép đăng nhập)
                if (!user.IsActive && !user.IsAdmin)
                {
                    TempData["Error"] = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để biết thêm chi tiết.";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                // TẠO CLAIMS
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.FullName ?? user.Email),
                    new(ClaimTypes.Email, user.Email)
                };

                if (user.IsAdmin)
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // ĐĂNG NHẬP THỰC SỰ
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // LƯU VÀO SESSION
                HttpContext.Session.SetString("UserId", user.Id.ToString());
                HttpContext.Session.SetString("UserName", user.FullName ?? user.Email);
                HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

                return Redirect(ReturnUrl ?? "~/");
            }

            // [GET] /Account/Register
            [HttpGet]
            public IActionResult Register(string? returnUrl)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // [POST] /Account/Register
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Register(string FullName, string Email, string Password, string ConfirmPassword, string PhoneNumber, string? Gender, string? ReturnUrl)
            {
                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                if (Password != ConfirmPassword)
                {
                    TempData["Error"] = "Mật khẩu và xác nhận mật khẩu không khớp!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                Email = Email.Trim().ToLower();
                PhoneNumber = PhoneNumber.Trim();

                // Kiểm tra format email phải có .com
                if (!Email.Contains('@') || !Email.EndsWith(".com"))
                {
                    TempData["Error"] = "Email phải có định dạng hợp lệ và kết thúc bằng .com!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                if (await _context.Users.AnyAsync(u => u.Email == Email))
                {
                    TempData["Error"] = "Email đã được sử dụng!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                if (await _context.Users.AnyAsync(u => u.PhoneNumber == PhoneNumber))
                {
                    TempData["Error"] = "Số điện thoại đã được sử dụng!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                if (Password.Length < 6 || !Password.Any(char.IsUpper) ||
                    !Password.Any(ch => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(ch)))
                {
                    TempData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự, 1 chữ hoa và 1 ký tự đặc biệt!";
                    ViewBag.ReturnUrl = ReturnUrl;
                    return View();
                }

                var newUser = new User
                {
                    FullName = FullName.Trim(),
                    Email = Email,
                    PasswordHash = HashPassword(Password),
                    PhoneNumber = PhoneNumber.Trim(),
                    Gender = Gender,
                    CreatedAt = DateTime.Now,
                    IsAdmin = Email == "gamer957ola@gmail.com",
                    IsActive = true
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tạo tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login", new { ReturnUrl });
            }

            // ĐĂNG XUẤT
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Logout()
            {
                // Xóa TempData trước khi clear session
                TempData.Clear();
                
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                
                // Thêm headers để ngăn cache
                Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";
                
                return RedirectToAction("Index", "Home");
            }

            // [GET] /Account/Profile
            [HttpGet]
            public async Task<IActionResult> Profile()
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                    return RedirectToAction("Login");

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var user = await _context.Users.FindAsync(userId);
                return user == null ? RedirectToAction("Login") : View(user);
            }

            // [POST] /Account/UpdateProfile
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> UpdateProfile(string FullName, string PhoneNumber, string? Gender)
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                    return RedirectToAction("Login");

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin người dùng!";
                    return RedirectToAction("Profile");
                }

                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
                    return RedirectToAction("Profile");
                }

                // Kiểm tra số điện thoại đã được sử dụng bởi người khác chưa
                var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == PhoneNumber.Trim() && u.Id != userId);
                if (phoneExists)
                {
                    TempData["Error"] = "Số điện thoại đã được sử dụng bởi tài khoản khác!";
                    return RedirectToAction("Profile");
                }

                user.FullName = FullName.Trim();
                user.PhoneNumber = PhoneNumber.Trim();
                user.Gender = Gender;

                await _context.SaveChangesAsync();

                // Cập nhật session
                HttpContext.Session.SetString("UserName", user.FullName);

                TempData["Success"] = "Cập nhật thông tin thành công!";
                return RedirectToAction("Profile");
            }

            // [POST] /Account/ChangePassword
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> ChangePassword(string CurrentPassword, string NewPassword, string ConfirmNewPassword)
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                    return RedirectToAction("Login");

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin người dùng!";
                    return RedirectToAction("Profile");
                }

                if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmNewPassword))
                {
                    TempData["Error"] = "Vui lòng điền đầy đủ thông tin!";
                    return RedirectToAction("Profile");
                }

                if (!VerifyPassword(CurrentPassword, user.PasswordHash))
                {
                    TempData["Error"] = "Mật khẩu hiện tại không đúng!";
                    return RedirectToAction("Profile");
                }

                if (NewPassword != ConfirmNewPassword)
                {
                    TempData["Error"] = "Mật khẩu mới và xác nhận mật khẩu không khớp!";
                    return RedirectToAction("Profile");
                }

                if (NewPassword.Length < 6 || !NewPassword.Any(char.IsUpper) ||
                    !NewPassword.Any(ch => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(ch)))
                {
                    TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự, 1 chữ hoa và 1 ký tự đặc biệt!";
                    return RedirectToAction("Profile");
                }

                user.PasswordHash = HashPassword(NewPassword);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("Profile");
            }

            // HÀM MÃ HÓA
            private static string HashPassword(string password)
            {
                return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            }

            private static bool VerifyPassword(string password, string hash)
                => HashPassword(password) == hash;

            // [GET] /Account/ForgotPassword
            [HttpGet]
            public IActionResult ForgotPassword()
            {
                return View();
            }

            // [POST] /Account/ForgotPassword - Gửi OTP
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> ForgotPassword(string email)
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["Error"] = "Vui lòng nhập email!";
                    return View();
                }

                email = email.Trim().ToLower();

                // Kiểm tra format email phải có .com
                if (!email.Contains('@') || !email.EndsWith(".com"))
                {
                    TempData["Error"] = "Email phải có định dạng hợp lệ và kết thúc bằng .com!";
                    return View();
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    TempData["Error"] = "Email không tồn tại trong hệ thống!";
                    return View();
                }

                // Tạo mã OTP 6 số
                var otp = new Random().Next(100000, 999999).ToString();
                
                // Lưu OTP vào session (có thời hạn 5 phút)
                HttpContext.Session.SetString("ResetOTP", otp);
                HttpContext.Session.SetString("ResetEmail", email);
                HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(5).ToString());

                try
                {
                    // Gửi OTP qua email
                    await _emailService.SendOtpEmailAsync(email, otp);
                    TempData["Success"] = "Mã OTP đã được gửi đến email của bạn!";
                    return RedirectToAction("VerifyOTP");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Không thể gửi email: {ex.Message}";
                    return View();
                }
            }

            // [GET] /Account/VerifyOTP
            [HttpGet]
            public IActionResult VerifyOTP()
            {
                var resetEmail = HttpContext.Session.GetString("ResetEmail");
                if (string.IsNullOrEmpty(resetEmail))
                {
                    return RedirectToAction("ForgotPassword");
                }
                
                ViewBag.Email = resetEmail;
                return View();
            }

            // [POST] /Account/VerifyOTP - Xác thực OTP
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult VerifyOTP(string otp)
            {
                var savedOTP = HttpContext.Session.GetString("ResetOTP");
                var resetEmail = HttpContext.Session.GetString("ResetEmail");
                var expiryString = HttpContext.Session.GetString("OTPExpiry");

                if (string.IsNullOrEmpty(savedOTP) || string.IsNullOrEmpty(resetEmail))
                {
                    TempData["Error"] = "Phiên làm việc đã hết hạn. Vui lòng thử lại!";
                    return RedirectToAction("ForgotPassword");
                }

                // Kiểm tra thời hạn OTP
                if (!string.IsNullOrEmpty(expiryString) && DateTime.TryParse(expiryString, out var expiry))
                {
                    if (DateTime.Now > expiry)
                    {
                        HttpContext.Session.Remove("ResetOTP");
                        HttpContext.Session.Remove("ResetEmail");
                        HttpContext.Session.Remove("OTPExpiry");
                        TempData["Error"] = "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới!";
                        return RedirectToAction("ForgotPassword");
                    }
                }

                if (otp?.Trim() != savedOTP)
                {
                    TempData["Error"] = "Mã OTP không đúng!";
                    ViewBag.Email = resetEmail;
                    return View();
                }

                // OTP đúng, chuyển đến trang đặt lại mật khẩu
                HttpContext.Session.SetString("OTPVerified", "true");
                return RedirectToAction("ResetPassword");
            }

            // [GET] /Account/ResetPassword
            [HttpGet]
            public IActionResult ResetPassword()
            {
                var verified = HttpContext.Session.GetString("OTPVerified");
                var resetEmail = HttpContext.Session.GetString("ResetEmail");

                if (verified != "true" || string.IsNullOrEmpty(resetEmail))
                {
                    return RedirectToAction("ForgotPassword");
                }

                ViewBag.Email = resetEmail;
                return View();
            }

            // [POST] /Account/ResetPassword - Đặt lại mật khẩu
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
            {
                var verified = HttpContext.Session.GetString("OTPVerified");
                var resetEmail = HttpContext.Session.GetString("ResetEmail");

                if (verified != "true" || string.IsNullOrEmpty(resetEmail))
                {
                    TempData["Error"] = "Phiên làm việc không hợp lệ!";
                    return RedirectToAction("ForgotPassword");
                }

                if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                {
                    TempData["Error"] = "Vui lòng nhập đầy đủ thông tin!";
                    ViewBag.Email = resetEmail;
                    return View();
                }

                if (newPassword != confirmPassword)
                {
                    TempData["Error"] = "Mật khẩu và xác nhận mật khẩu không khớp!";
                    ViewBag.Email = resetEmail;
                    return View();
                }

                if (newPassword.Length < 6 || !newPassword.Any(char.IsUpper) ||
                    !newPassword.Any(ch => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(ch)))
                {
                    TempData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự, 1 chữ hoa và 1 ký tự đặc biệt!";
                    ViewBag.Email = resetEmail;
                    return View();
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == resetEmail);
                if (user == null)
                {
                    TempData["Error"] = "Không tìm thấy người dùng!";
                    return RedirectToAction("ForgotPassword");
                }

                user.PasswordHash = HashPassword(newPassword);
                await _context.SaveChangesAsync();

                // Xóa session
                HttpContext.Session.Remove("ResetOTP");
                HttpContext.Session.Remove("ResetEmail");
                HttpContext.Session.Remove("OTPExpiry");
                HttpContext.Session.Remove("OTPVerified");

                TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
        }
    }
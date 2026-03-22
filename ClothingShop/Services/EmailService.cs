using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ClothingShop.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]
            ));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(
                    _configuration["EmailSettings:SmtpServer"],
                    int.Parse(_configuration["EmailSettings:Port"] ?? "587"),
                    SecureSocketOptions.StartTls
                );

                await smtp.AuthenticateAsync(
                    _configuration["EmailSettings:Username"],
                    _configuration["EmailSettings:Password"]
                );

                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                throw new Exception($"Không thể gửi email: {ex.Message}");
            }
        }

        public async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            var subject = "Mã OTP đặt lại mật khẩu - ClothingShop";
            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #000; color: white; padding: 20px; text-align: center; }}
                        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 5px; margin-top: 20px; }}
                        .otp-code {{ font-size: 32px; font-weight: bold; color: #dc3545; text-align: center; 
                                     padding: 20px; background-color: white; border-radius: 5px; 
                                     letter-spacing: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
                        .warning {{ color: #dc3545; font-weight: bold; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>CLOTHINGSHOP</h1>
                        </div>
                        <div class='content'>
                            <h2>Đặt lại mật khẩu</h2>
                            <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản ClothingShop của mình.</p>
                            <p>Mã OTP của bạn là:</p>
                            <div class='otp-code'>{otp}</div>
                            <p class='warning'>⚠️ Mã OTP này có hiệu lực trong 5 phút.</p>
                            <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                        </div>
                        <div class='footer'>
                            <p>© 2024 ClothingShop. All rights reserved.</p>
                            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}

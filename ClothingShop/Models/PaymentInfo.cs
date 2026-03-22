namespace ClothingShop.Models
{
    public class PaymentInfo
    {
        public int Id { get; set; }
        
        // Thông tin ngân hàng
        public string BankName { get; set; } = "Vietcombank";
        public string BankAccountNumber { get; set; } = "1234567890";
        public string BankAccountName { get; set; } = "NGUYEN VAN A";
        
        // Thông tin MoMo
        public string MoMoPhone { get; set; } = "0901234567";
        public string MoMoName { get; set; } = "NGUYEN VAN A";
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

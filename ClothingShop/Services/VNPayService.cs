using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ClothingShop.Services;

public interface IVNPayService
{
    string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, string ipAddress);
    bool ValidateSignature(IQueryCollection queryParams, string inputHash);
}

public class VNPayService(IConfiguration configuration) : IVNPayService
{
    private readonly IConfiguration _configuration = configuration;

    public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, string ipAddress)
    {
        var vnpay = new VNPayLibrary();
        
        vnpay.AddRequestData("vnp_Version", _configuration["VNPay:Version"] ?? "2.1.0");
        vnpay.AddRequestData("vnp_Command", _configuration["VNPay:Command"] ?? "pay");
        vnpay.AddRequestData("vnp_TmnCode", _configuration["VNPay:TmnCode"] ?? "");
        vnpay.AddRequestData("vnp_Amount", ((long)(amount * 100)).ToString());
        vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", _configuration["VNPay:CurrCode"] ?? "VND");
        vnpay.AddRequestData("vnp_IpAddr", ipAddress);
        vnpay.AddRequestData("vnp_Locale", _configuration["VNPay:Locale"] ?? "vn");
        vnpay.AddRequestData("vnp_OrderInfo", orderInfo);
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", _configuration["VNPay:ReturnUrl"] ?? "");
        vnpay.AddRequestData("vnp_TxnRef", orderId.ToString());

        string paymentUrl = vnpay.CreateRequestUrl(_configuration["VNPay:Url"]!, _configuration["VNPay:HashSecret"]!);
        return paymentUrl;
    }

    public bool ValidateSignature(IQueryCollection queryParams, string inputHash)
    {
        var vnpay = new VNPayLibrary();
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(key, value!);
            }
        }

        string hashSecret = _configuration["VNPay:HashSecret"]!;
        bool checkSignature = vnpay.ValidateSignature(inputHash, hashSecret);
        return checkSignature;
    }
}

public class VNPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(new VNPayCompare());
    private readonly SortedList<string, string> _responseData = new(new VNPayCompare());

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData.Add(key, value);
        }
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData.Add(key, value);
        }
    }

    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out string? retValue) ? retValue : string.Empty;
    }

    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        StringBuilder data = new();
        foreach (var (key, value) in _requestData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        string queryString = data.ToString();
        baseUrl += "?" + queryString;
        string signData = queryString;
        if (signData.Length > 0)
        {
            signData = signData[..^1]; // Simplified: Remove last character
        }

        string vnpSecureHash = HmacSHA512(vnpHashSecret, signData);
        baseUrl += "vnp_SecureHash=" + vnpSecureHash;

        return baseUrl;
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        StringBuilder data = new();
        foreach (var (key, value) in _responseData.Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "vnp_SecureHash"))
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        string signData = data.ToString();
        if (signData.Length > 0)
        {
            signData = signData[..^1];
        }

        string myChecksum = HmacSHA512(secretKey, signData);
        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string HmacSHA512(string key, string inputData)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        byte[] hashValue = hmac.ComputeHash(inputBytes);
        return BitConverter.ToString(hashValue).Replace("-", "").ToLower();
    }
}

public class VNPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}

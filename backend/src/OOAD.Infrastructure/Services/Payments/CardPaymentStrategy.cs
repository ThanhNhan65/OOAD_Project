using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;

namespace OOAD.Infrastructure.Services.Payments;

public class CardPaymentStrategy : IPaymentStrategy
{
    private readonly IConfiguration _config;
    public string Method => "CARD";
    public CardPaymentStrategy(IConfiguration config)
    {
        _config = config;
    }
    public async Task<GatewayRespone> ChargeAsync(decimal amount, bool mockSuccess = true, PaymentContext? context = null, CancellationToken ct = default)
    {
        if(context?.UseSandbox == true)
        {
            return BuildVnpayUrl(amount, context);
        }
        await Task.Delay(200, ct);
        if(!mockSuccess || amount <= 0)
        {
            return new GatewayRespone
            {
                Success = false,
                ErrorMessage = "Thẻ ngân hàng không hợp lệ, hết hạn mức hoặc bị ngân hàng từ chối"
            };
        }
        return new GatewayRespone
        {
            Success = true,
            TransactionRef = $"CARD_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
        };
    }
    private GatewayRespone BuildVnpayUrl(decimal amount, PaymentContext context)
    {
        var vnpSettings = _config.GetSection("PaymentSettings:Vnpay");
        var tmnCode = vnpSettings["TmnCode"] ?? "8O0IA3FA"; // mã định danh website, dựa vào mã này để biết giao dịch với merchant nào
        var hashSecret = vnpSettings["HashSecret"] ?? "ICFVGKFTSDUXWXLDYBBHRSCWZZEEBDBU"; // khóa bí mật vnpay cung cấp merchant, sử dụng khóa tạo chữ kí HMAC
        var baseUrl = vnpSettings["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"; // URL của trang thanh toán VNPAY
        var returnUrl = context.ReturnUrl ?? vnpSettings["ReturnUrl"] ?? "http://localhost:5173/payment-return"; // địa chỉ vnpay chuyển về sau khi thanh toán thành công
        var txnRef = $"CARD_{context.OrderCode}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"; // mã định danh duy nhất của giao dịch
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");
        var vnpParams = new SortedDictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", tmnCode },
            { "vnp_Locale", "vn" },
            { "vnp_CurrCode", "VND" },
            { "vnp_TxnRef", txnRef },
            { "vnp_OrderInfo", $"Thanh toan goi {context.OrderCode}" },
            { "vnp_OrderType", "other" },
            { "vnp_Amount", ((long)(amount * 100)).ToString() }, // Đơn vị nhân 100
            { "vnp_ReturnUrl", returnUrl },
            { "vnp_IpAddr", context.IpAddress ?? "127.0.0.1" },
            { "vnp_CreateDate", createDate }
        };
        // Ký HMAC-SHA512 
        var signData = string.Join("&", vnpParams.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signData));
        var secureHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var paymentUrl = $"{baseUrl}?{signData}&vnp_SecureHash={secureHash}";
        
        return new GatewayRespone
        {
            Success = true,
            TransactionRef = txnRef,
            PaymentUrl = paymentUrl
        };
    }
    // Xác thực chữ ký khi VNPay trả về
    public bool VerifyVnpaySignature(IDictionary<string,string> queryParams)
    {
        var vnpSettings = _config.GetSection("PaymentSettings:Vnpay");
        var hashSecret = vnpSettings["HashSecret"] ?? "ICFVGKFTSDUXWXLDYBBHRSCWZZEEBDBU";
        if (!queryParams.TryGetValue("vnp_SecureHash", out var vnpSecureHash))
            return false;

        var cleanParams = new SortedDictionary<string, string>();
        foreach (var (k, v) in queryParams)
        {
            if (k.StartsWith("vnp_") && k != "vnp_SecureHash" && k != "vnp_SecureHashType")
            {
                cleanParams[k] = v;
            }
        }

        var signData = string.Join("&", cleanParams.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
        var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signData))).Replace("-", "").ToLowerInvariant();
        return computedHash.Equals(vnpSecureHash, StringComparison.OrdinalIgnoreCase);
    }

}
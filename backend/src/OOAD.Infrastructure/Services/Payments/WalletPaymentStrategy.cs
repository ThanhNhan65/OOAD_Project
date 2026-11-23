using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
namespace OOAD.Infrastructure.Services.Payments;

public class WalletPaymentStrategy : IPaymentStrategy
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    public string Method => "WALLET";

    public WalletPaymentStrategy(IConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }
    
    public async Task<GatewayRespone> ChargeAsync(decimal amount, bool mockSuccess = true, PaymentContext? context = null, CancellationToken ct = default)
    {
        if(context?.UseSandbox == true)
        {
            return await CreateZaloPayOrderAsync(amount, context, ct);

        }

        await Task.Delay(150, ct);
        if (!mockSuccess || amount <= 0)
        {
            return new GatewayRespone
            {
                Success = false,
                ErrorMessage = "Số dư ví điện tử không đủ hoặc giao dịch bị từ chối bởi cổng Ví."
            };
        }

        return new GatewayRespone
        {
            Success = true,
            TransactionRef = $"WALLET_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
        };
    }
    
    private async Task<GatewayRespone> CreateZaloPayOrderAsync(decimal amount, PaymentContext context, CancellationToken ct)
    {
        try
        {
            var zalo = _config.GetSection("PaymentSettings:Zalopay");
            var appId = int.Parse(zalo["AppId"] ?? "2554");
            var key1 = zalo["Key1"] ?? "sdngKKJmqEMzvh5QQcdD2A9XBSKUNaYn";
            var createUrl = zalo ["CreateUrl"] ?? "https://sb-openapi.zalopay.vn/v2/create";
            var returnUrl = context.ReturnUrl ?? zalo["ReturnUrl"] ?? "http://localhost:5173/payment-return";
            var now = DateTime.UtcNow.AddHours(7);
            var transId = $"{now:yyMMdd}_{context.OrderCode}_{Random.Shared.Next(100000, 999999)}";
            var appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var embedData = JsonSerializer.Serialize(new { redirecturl = $"{returnUrl}?orderCode={context.OrderCode}" });
            var items = "[]";

            // Công thức MAC của Zalopay
            var rawMac = $"{appId}|{transId}|gym_member|{(long)amount}|{appTime}|{embedData}|{items}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key1));
            var mac = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawMac))).Replace("-", "").ToLowerInvariant();

            var payload = new Dictionary<string, object>
            {
                { "app_id", appId },
                { "app_user", "gym_member" },
                { "app_trans_id", transId },
                { "app_time", appTime },
                { "amount", (long)amount },
                { "item", items },
                { "embed_data", embedData },
                { "description", $"Thanh toan don hang {context.OrderCode}" },
                { "bank_code", "" },
                { "mac", mac }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync(createUrl, content, ct);
            var jsonRes = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(jsonRes);
            var returnCode = doc.RootElement.GetProperty("return_code").GetInt32();
            if(returnCode == 1 && doc.RootElement.TryGetProperty("order_url", out var orderUrl))
            {
                return new GatewayRespone
                {
                    Success = true,
                    TransactionRef = transId,
                    PaymentUrl = orderUrl.GetString()
                };
            }
            return new GatewayRespone
            {
              Success = false,
                ErrorMessage = doc.RootElement.TryGetProperty("return_message", out var msg) ? msg.GetString() : "Lỗi khi gọi ZaloPaySanbox"  
            };
        }catch (Exception ex)
        {
            return new GatewayRespone
            {
                Success = false,
                ErrorMessage = $"Không thể kết nối đến Zalopay Sanbox: {ex.Message}"
            };
        }
    }
}
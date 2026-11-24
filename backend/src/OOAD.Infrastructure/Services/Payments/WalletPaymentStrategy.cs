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
            var zalo = _config.GetSection("PaymentSettings:ZaloPay");
            var appId = int.Parse(zalo["AppId"] ?? "2554");
            var key1 = zalo["Key1"] ?? "sdngKKJmqEMzvh5QQcdD2A9XBSKUNaYn";
            var createUrl = zalo["CreateUrl"] ?? "https://sb-openapi.zalopay.vn/v2/create";
            var returnUrl = context.ReturnUrl ?? zalo["ReturnUrl"] ?? "http://localhost:5173/payment-return";
            var now = DateTime.UtcNow.AddHours(7);
            var transId = $"{now:yyMMdd}_{context.OrderCode}_{Random.Shared.Next(100000, 999999)}";
            var appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var embedData = JsonSerializer.Serialize(new { redirecturl = returnUrl });
            var items = "[]";

            // Công thức MAC của ZaloPay: app_id|app_trans_id|app_user|amount|app_time|embed_data|item
            var rawMac = $"{appId}|{transId}|gym_member|{(long)amount}|{appTime}|{embedData}|{items}";
            var mac = ComputeHmacSha256(key1, rawMac);

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
            var returnCode = doc.RootElement.TryGetProperty("return_code", out var rcEl) ? rcEl.GetInt32() : 0;
            if (returnCode == 1 && doc.RootElement.TryGetProperty("order_url", out var orderUrl))
            {
                return new GatewayRespone
                {
                    Success = true,
                    TransactionRef = transId,
                    PaymentUrl = orderUrl.GetString()
                };
            }

            var retMsg = doc.RootElement.TryGetProperty("return_message", out var msg) ? msg.GetString() : "Lỗi khi gọi ZaloPay Sandbox";
            var subCode = doc.RootElement.TryGetProperty("sub_return_code", out var scEl) ? scEl.GetInt32() : 0;
            var subMsg = doc.RootElement.TryGetProperty("sub_return_message", out var smEl) ? smEl.GetString() : null;
            var detailError = string.IsNullOrWhiteSpace(subMsg)
                ? $"{retMsg} (code: {returnCode}, sub: {subCode})"
                : $"{retMsg} - {subMsg} (code: {returnCode}, sub: {subCode})";

            return new GatewayRespone
            {
                Success = false,
                ErrorMessage = detailError
            };
        }
        catch (Exception ex)
        {
            return new GatewayRespone
            {
                Success = false,
                ErrorMessage = $"Không thể kết nối đến ZaloPay Sandbox: {ex.Message}"
            };
        }
    }

    public async Task<(int ReturnCode, int SubReturnCode, string ReturnMessage, string? ZpTransId)> QueryZaloPayOrderAsync(
        string appTransId,
        CancellationToken ct = default)
    {
        var zalo = _config.GetSection("PaymentSettings:ZaloPay");
        var appId = int.Parse(zalo["AppId"] ?? "2554");
        var key1 = zalo["Key1"] ?? "sdngKKJmqEMzvh5QQcdD2A9XBSKUNaYn";
        var queryUrl = zalo["QueryUrl"] ?? "https://sb-openapi.zalopay.vn/v2/query";

        // Công thức MAC truy vấn đơn hàng ZaloPay v2: app_id|app_trans_id|key1
        var rawMac = $"{appId}|{appTransId}|{key1}";
        var mac = ComputeHmacSha256(key1, rawMac);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "app_id", appId.ToString() },
            { "app_trans_id", appTransId },
            { "mac", mac }
        });

        var res = await _httpClient.PostAsync(queryUrl, form, ct);
        var jsonRes = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonRes);

        var returnCode = doc.RootElement.TryGetProperty("return_code", out var rcEl) ? rcEl.GetInt32() : 0;
        var subCode = doc.RootElement.TryGetProperty("sub_return_code", out var scEl) ? scEl.GetInt32() : 0;
        var retMsg = doc.RootElement.TryGetProperty("return_message", out var msgEl)
            ? (msgEl.GetString() ?? string.Empty)
            : string.Empty;
        var subMsg = doc.RootElement.TryGetProperty("sub_return_message", out var smEl) ? smEl.GetString() : null;
        string? zpTransId = null;
        if (doc.RootElement.TryGetProperty("zp_trans_id", out var zpEl))
        {
            zpTransId = zpEl.ValueKind == JsonValueKind.Number
                ? zpEl.GetInt64().ToString()
                : zpEl.GetString();
        }

        var fullMessage = string.IsNullOrWhiteSpace(subMsg) ? retMsg : $"{retMsg} ({subMsg})";
        return (returnCode, subCode, fullMessage, zpTransId);
    }

    public bool VerifyZaloPayCallbackMac(string data, string reqMac)
    {
        var zalo = _config.GetSection("PaymentSettings:ZaloPay");
        var key2 = zalo["Key2"] ?? "trMrHtvjo6myautxDUiAcYsVtaeQ8nhf";
        var computed = ComputeHmacSha256(key2, data);
        return computed.Equals(reqMac, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeHmacSha256(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLowerInvariant();
    }
}
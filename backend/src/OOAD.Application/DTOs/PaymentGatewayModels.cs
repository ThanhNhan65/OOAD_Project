namespace OOAD.Application.DTOs;

// Kết quả trả về từ Gateway/Strategy
public class GatewayRespone
{
    public bool Success {get; set;}
    public string? TransactionRef {get; set;}
    public string? PaymentUrl {get; set;}
    public string? ErrorMessage {get; set;}
}
// Context dữ liệu truyền vào Strategy
public class PaymentContext
{
    public Guid MembershipId {get; set;}
    public string OrderCode {get; set;} = string.Empty;
    public string? IpAddress {get; set;}
    public string? ReturnUrl {get; set;}
    public bool UseSandbox {get; set;}
}
// Request khi gọi API thanh toán
public class ProcessPaymentRequest
{
    public Guid MembershipId {get; set;}
    public string Method {get; set;} = "ZaloPay";
    public string IdempotencyKey {get; set;} = string.Empty;
    public bool MockSuccess {get; set; } = true;
    public bool UseSandbox {get; set;} = false;
    public string? IpAddr {get; set;}
}
// Respone trả ra cho Client
public class ProcessPaymentRespone
{
    public bool Success {get; set;}
    public bool IsIdempotent {get; set;}
    public Guid MembershipId {get; set;}
    public string MembershipStatus {get; set;} = string.Empty;
    public PaymentDto Payment {get; set;} = null!;
    public string? ErrorMessage {get; set;}
    public string? PaymentUrl {get; set;}
}

public class PaymentVerificationResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string VerifiedBy { get; set; } = string.Empty;
    public string? TransactionCode { get; set; }
    public string? GatewayTransactionNo { get; set; }
    public string? BankCode { get; set; }
    public decimal Amount { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? MembershipId { get; set; }
    public string MembershipStatus { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class ZaloPayCallbackRequest
{
    public string Data { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
}
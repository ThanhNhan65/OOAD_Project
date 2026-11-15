using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

/// <summary>
/// Lớp Controller cơ sở (Base Controller) cho tất cả Controller trong API.
/// Kế thừa ControllerBase, cấu hình tiền tố route và chuẩn hóa phản hồi API.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
}

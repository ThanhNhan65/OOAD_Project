using OOAD.API.Extensions;
using OOAD.API.Filters;
using OOAD.API.Middlewares;
using OOAD.API.Services;
using OOAD.Application;
using OOAD.Application.Common.Options;
using OOAD.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký cấu hình quy tắc nghiệp vụ & các tầng Clean Architecture vào IoC Container
var businessRules = new BusinessRuleOptions();
builder.Configuration.GetSection(BusinessRuleOptions.SectionName).Bind(businessRules);
builder.Services.AddSingleton(businessRules);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
// 2. Cấu hình JWT Authentication & Role-based Authorization
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Bearer";
        options.DefaultChallengeScheme = "Bearer";
    })
    .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>("Bearer", null);

builder.Services.AddAuthorization();

// 3. Cấu hình Controllers, Validation Handler, OpenAPI & Background Services
builder.Services.AddScoped<ValidationActionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<ValidationActionFilter>());
builder.Services.AddOpenApi();
builder.Services.AddHostedService<WaitlistCutoffBackgroundService>();

// 4. Cấu hình CORS cho Frontend React (Vite / Next.js)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1"))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            var configuredOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            policy.SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        return false;

                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1")
                        return true;

                    if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                        uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                        return true;

                    return configuredOrigins.Any(o =>
                        string.Equals(o.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

// 5. Kích hoạt tự động tạo lại Database (25 bảng Fluent API) & Seed dữ liệu mẫu
await app.InitialiseDatabaseAsync();

// 6. Cấu hình HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Bật giao diện test API Scalar trực quan tại đường dẫn /scalar
    app.MapScalarApiReference();
}

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

// Middleware kiểm tra tài khoản LOCKED & xử lý ngoại lệ tập trung
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Healthy_System.Data;
using Healthy_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Healthy_System.Filters
{
    // US41: Tự động ghi nhật ký cho mọi thao tác làm thay đổi dữ liệu (POST thành công)
    // trên toàn hệ thống, không cần sửa từng controller của các thành viên khác.
    public class AuditLogActionFilter : IAsyncActionFilter
    {
        // Các action đã tự ghi log thủ công trong AdminController -> bỏ qua để tránh ghi trùng
        private static readonly string[] AlreadyLoggedAdminActions =
            { "ChangeRole", "CreateTimeSlot", "ToggleTimeSlot", "DeleteTimeSlot" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            try
            {
                var http = context.HttpContext;

                // Chỉ ghi các thao tác thay đổi dữ liệu
                if (!HttpMethods.IsPost(http.Request.Method)) return;
                if (http.User?.Identity?.IsAuthenticated != true) return;
                if (executed.Exception != null && !executed.ExceptionHandled) return;

                var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
                var actionName = context.RouteData.Values["action"]?.ToString() ?? "";

                // Không ghi log đăng nhập / đăng ký (chứa mật khẩu) và các action đã log thủ công
                if (controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase)) return;
                if (controllerName.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                    AlreadyLoggedAdminActions.Contains(actionName, StringComparer.OrdinalIgnoreCase)) return;

                // Thao tác thành công trong hệ thống này luôn kết thúc bằng redirect
                // (nếu validate lỗi thì trả lại View -> không ghi log)
                var succeeded = executed.Result is RedirectToActionResult
                                || executed.Result is RedirectResult
                                || executed.Result is RedirectToRouteResult
                                || executed.Result is LocalRedirectResult;
                if (!succeeded) return;

                var tempData = (context.Controller as Controller)?.TempData;
                if (tempData != null && tempData.ContainsKey("ErrorMessage")) return;

                var description = tempData?.Peek("SuccessMessage")?.ToString();
                if (string.IsNullOrWhiteSpace(description))
                    description = $"Thực hiện thao tác {actionName} tại {controllerName}";

                var db = http.RequestServices.GetRequiredService<AppDbContext>();

                var idStr = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? userId = int.TryParse(idStr, out var uid) ? uid : null;

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    ActorName = Truncate(http.User.Identity?.Name ?? "Không rõ", 100),
                    ActorRole = Truncate(http.User.FindFirst(ClaimTypes.Role)?.Value ?? "System", 50),
                    Action = ClassifyAction(actionName),
                    EntityName = Truncate(controllerName, 100),
                    EntityId = Truncate(FindEntityId(context), 100),
                    Description = Truncate(description, 500),
                    IpAddress = Truncate(http.Connection.RemoteIpAddress?.ToString(), 50),
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nhưng tuyệt đối không làm hỏng thao tác chính của người dùng
                context.HttpContext.RequestServices.GetService<ILogger<AuditLogActionFilter>>()
                    ?.LogWarning(ex, "Không ghi được Audit Log");
            }
        }

        private static string ClassifyAction(string actionName)
        {
            var a = actionName.ToLowerInvariant();
            if (a.Contains("delete") || a.Contains("remove")) return "Delete";
            if (a.Contains("cancel") || a.Contains("reject")) return "Cancel";
            if (a.Contains("create") || a.Contains("add") || a.Contains("book") || a.Contains("register")) return "Create";
            return "Update";
        }

        private static string? FindEntityId(ActionExecutingContext context)
        {
            if (context.RouteData.Values.TryGetValue("id", out var routeId) && routeId != null)
                return routeId.ToString();

            foreach (var arg in context.ActionArguments)
            {
                if ((arg.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                     arg.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    && arg.Value != null && (arg.Value is int || arg.Value is long || arg.Value is string))
                    return arg.Value.ToString();
            }
            return null;
        }

        private static string? Truncate(string? value, int max)
            => value == null ? null : (value.Length <= max ? value : value.Substring(0, max));
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectTalaria.ControlPlane.Api.Services;

namespace ProjectTalaria.ControlPlane.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute(string resource, string action) : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _resource = resource;
    private readonly string _action = action;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = context.HttpContext.User.FindFirst("sub")?.Value
            ?? context.HttpContext.User.FindFirst("nameidentifier")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid token" });
            return;
        }

        var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var hasPermission = await authService.HasPermissionAsync(userId, _resource, _action);

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}

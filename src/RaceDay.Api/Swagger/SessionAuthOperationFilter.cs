using System.Reflection;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Auth;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Swagger;

/// <summary>
/// Reads [SessionAuthorize] off each action and states in Swagger UI who may call it,
/// adding the 401/403 responses the filter can return.
/// </summary>
public class SessionAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttribute<SessionAuthorizeAttribute>()
            ?? context.MethodInfo.DeclaringType?.GetCustomAttribute<SessionAuthorizeAttribute>();

        string access;
        if (attribute is null)
        {
            access = "**Access:** Public, no login required.";
        }
        else
        {
            access = attribute.Roles.Length == 0
                ? "**Access:** Any logged-in user (Organiser or Participant)."
                : $"**Access:** {string.Join(" / ", attribute.Roles)} only.";

            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Not logged in. Call POST /api/auth/login first." });
            if (attribute.Roles.Length > 0)
            {
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Logged in with the wrong role for this endpoint." });
            }
        }

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? access
            : $"{access}\n\n{operation.Description}";
    }
}

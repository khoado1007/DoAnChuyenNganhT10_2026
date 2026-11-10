using ITManagement.API.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ITManagement.API.Extensions;

public sealed class AgentSecurityOperationFilter : IOperationFilter
{
    /// <summary>Bổ sung mô tả xác thực riêng cho credential của agent trong OpenAPI.</summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(AgentsController)) return;
        if (context.MethodInfo.Name == nameof(AgentsController.Enroll))
            operation.Security = [new OpenApiSecurityRequirement()];
        if (context.MethodInfo.Name == nameof(AgentsController.Heartbeat))
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Agent" } }] = []
            }];
    }
}

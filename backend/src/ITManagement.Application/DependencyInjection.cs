using ITManagement.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ITManagement.Application;

public static class DependencyInjection
{
    /// <summary>Đăng ký các dịch vụ và pipeline của tầng ứng dụng.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);

        // AutoMapper v16+ sử dụng keyed registration
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        return services;
    }
}

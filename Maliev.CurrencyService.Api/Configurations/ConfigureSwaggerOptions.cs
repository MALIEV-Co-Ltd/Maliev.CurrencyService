using Asp.Versioning.ApiExplorer;
using Maliev.CurrencyService.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Maliev.CurrencyService.Api.Configurations;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly SwaggerOptions _swaggerOptions;

    public ConfigureSwaggerOptions(
        IApiVersionDescriptionProvider provider,
        IOptions<SwaggerOptions> swaggerOptions)
    {
        _provider = provider;
        _swaggerOptions = swaggerOptions.Value;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }

        // Add JWT Bearer authorization
        options.AddSecurityDefinition(_swaggerOptions.SecurityScheme.Id, new OpenApiSecurityScheme
        {
            Description = _swaggerOptions.SecurityScheme.Description,
            Name = _swaggerOptions.SecurityScheme.Name,
            In = Enum.Parse<ParameterLocation>(_swaggerOptions.SecurityScheme.In),
            Type = Enum.Parse<SecuritySchemeType>(_swaggerOptions.SecurityScheme.Type),
            Scheme = _swaggerOptions.SecurityScheme.Scheme
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = _swaggerOptions.SecurityScheme.Id
                    },
                    Scheme = "oauth2",
                    Name = _swaggerOptions.SecurityScheme.Scheme,
                    In = Enum.Parse<ParameterLocation>(_swaggerOptions.SecurityScheme.In)
                },
                new List<string>()
            }
        });
    }

    private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = _swaggerOptions.Title,
            Version = description.ApiVersion.ToString(),
            Description = _swaggerOptions.Description,
            Contact = new OpenApiContact
            {
                Name = _swaggerOptions.Contact.Name,
                Email = _swaggerOptions.Contact.Email,
                Url = new Uri(_swaggerOptions.Contact.Url)
            },
            License = new OpenApiLicense
            {
                Name = _swaggerOptions.License.Name,
                Url = new Uri(_swaggerOptions.License.Url)
            }
        };

        if (description.IsDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }
}
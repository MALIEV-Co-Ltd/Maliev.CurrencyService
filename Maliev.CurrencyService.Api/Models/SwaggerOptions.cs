namespace Maliev.CurrencyService.Api.Models;

public class SwaggerOptions
{
    public const string SectionName = "Swagger";

    public string Title { get; set; } = "Maliev Currency Service API";
    public string Description { get; set; } = "A comprehensive CRUD API for managing currency data with advanced features including caching, rate limiting, and full-text search capabilities.";
    public string Version { get; set; } = "v1";
    public ContactOptions Contact { get; set; } = new();
    public LicenseOptions License { get; set; } = new();
    public SecuritySchemeOptions SecurityScheme { get; set; } = new();
}

public class ContactOptions
{
    public string Name { get; set; } = "Maliev Co. Ltd.";
    public string Email { get; set; } = "support@maliev.com";
    public string Url { get; set; } = "https://maliev.com";
}

public class LicenseOptions
{
    public string Name { get; set; } = "Proprietary";
    public string Url { get; set; } = "https://maliev.com/license";
}

public class SecuritySchemeOptions
{
    public string Name { get; set; } = "Authorization";
    public string Description { get; set; } = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.";
    public string Scheme { get; set; } = "Bearer";
    public string Type { get; set; } = "ApiKey";
    public string In { get; set; } = "Header";
    public string Id { get; set; } = "Bearer";
}
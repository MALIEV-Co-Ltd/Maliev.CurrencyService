# Maliev.CurrencyService Migration to .NET 10

This document summarizes the key changes and rationale behind the migration of the `Maliev.CurrencyService` project to .NET 10, incorporating best practices for API development and deployment.

## Key Changes Made

*   **Target Framework Update**: Migrated all projects (`Maliev.CurrencyService.Api`, `Maliev.CurrencyService.Data`, `Maliev.CurrencyService.Tests`) to `net10.0`.
*   **API Controller Refinement**:
    *   Introduced **Data Transfer Objects (DTOs)** (`CurrencyDto`, `CreateCurrencyRequest`, `UpdateCurrencyRequest`) for clear API contracts and robust input validation using `System.ComponentModel.DataAnnotations`.
    *   Implemented a **Service Layer** (`ICurrencyService`, `CurrencyService`) to encapsulate business logic, separating concerns from the controller.
    *   Integrated `ILogger` for comprehensive logging within the controller and service.
    *   Ensured all API operations are asynchronous (`async/await`).
*   **Project File (`.csproj`) Cleanup**:
    *   Removed unused build configurations, keeping only `Debug` and `Release`.
    *   Removed references to XML documentation files (`<DocumentationFile>`, `<None Update="*.xml">`) as they are generated and not directly consumed by the application at runtime, and are now excluded from source control.
    *   Removed references to `Resources.resx` and `Resources.Designer.cs` as they were not being used by the project.
    *   Cleaned up unnecessary `PackageReference` and `ProjectReference` entries.
    *   Added `required` keyword to properties in DTOs to enforce initialization and resolve `CS8618` warnings.
    *   Added XML documentation comments (`CS1591`) to public types and members in `CurrencyContext.cs`, `Currency.cs`, and test files.
    *   Ensured all necessary `using` directives are present in relevant files.
*   **Configuration Management**:
    *   Removed sensitive information (connection strings, JWT keys) from `appsettings.json` and `appsettings.Development.json`.
    *   Updated `launchSettings.json` to configure local development, including setting `launchUrl` to the Swagger UI page.
*   **Deployment Configuration**:
    *   Migrated and adapted Kubernetes manifests (`deployment.yaml`, `service.yaml`) and PowerShell deployment scripts (`deploy.ps1`, `deploy-service.ps1`) to reflect `CurrencyService` specifics.
    *   Integrated with Google Secret Manager via `SecretProviderClass` for secure secret management in Kubernetes, ensuring secrets are mounted into pods.
*   **Boilerplate Cleanup**: Removed all traces of 'WeatherForecast' boilerplate code.

## Rationale

The migration aimed to bring `Maliev.CurrencyService` in line with modern .NET development standards, improve maintainability, testability, and security, and ensure consistency with other services. By adopting DTOs, a service layer, and externalized secret management, the project is now more robust, scalable, and easier to deploy in a cloud-native environment.

## Important Considerations

*   **Secrets in Google Secret Manager**: Ensure the `ConnectionStrings-CurrencyServiceDbContext` secret is correctly configured in Google Secret Manager before deployment.
*   **`SecretProviderClass`**: Verify that the `maliev-shared-secrets` `SecretProviderClass` is correctly applied to your Kubernetes cluster and configured to fetch the necessary secrets from Google Secret Manager.
*   **Local Development Secrets**: For local development, the `ConnectionStrings-CurrencyServiceDbContext` is configured in `appsettings.Development.json`.
*   **Build and Test**: Always run `dotnet build` and `dotnet test` after any changes to ensure project integrity.

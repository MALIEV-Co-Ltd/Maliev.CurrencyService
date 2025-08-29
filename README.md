# Maliev.CurrencyService

This project contains the `Maliev.CurrencyService` API, Data, and Test projects.

## Project Structure

*   **Maliev.CurrencyService.Api**: The main ASP.NET Core Web API project.
*   **Maliev.CurrencyService.Data**: Contains the Entity Framework Core DbContext and data models.
*   **Maliev.CurrencyService.Tests**: Contains unit tests for the API and service layers.

## Build Instructions

To build the solution, navigate to the root directory of the project and run:

```bash
dotnet build
```

## Run Instructions

To run the API locally, navigate to the `Maliev.CurrencyService.Api` directory and run:

```bash
dotnet run
```

The API will typically be available at `https://localhost:7206` (or `http://localhost:5143`). The Swagger UI will be accessible at `/swagger`.

## Test Instructions

To run the unit tests, navigate to the root directory of the project and run:

```bash
dotnet test
```
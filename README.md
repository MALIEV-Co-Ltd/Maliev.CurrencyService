# Maliev Currency Service

A comprehensive microservice for managing currencies in the Maliev Co. Ltd. ecosystem. This service provides CRUD operations, search functionality, and caching for currency data with support for 153+ global currencies.

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)
- [API Documentation](#api-documentation)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Monitoring](#monitoring)
- [Contributing](#contributing)

## 🌟 Overview

The Currency Service is part of the Maliev microservices architecture, providing centralized currency management for all business operations. It supports multiple environments, comprehensive caching, and follows GitOps deployment practices.

### Key Capabilities

- **Currency Management**: Full CRUD operations for currency entities
- **Search & Filtering**: Advanced search capabilities across currency codes and names
- **Performance**: In-memory caching with configurable expiration
- **Security**: JWT authentication and rate limiting
- **Observability**: Health checks, structured logging, and monitoring
- **Scalability**: Horizontal pod autoscaling and resource management

## ✨ Features

### Core Functionality
- ✅ **153+ Currencies**: Pre-seeded with global currency data
- ✅ **CRUD Operations**: Create, read, update, delete currencies
- ✅ **Advanced Search**: Case-insensitive search by code or name
- ✅ **Pagination**: Configurable page sizes for large result sets
- ✅ **Caching**: Multi-level caching strategy for optimal performance

### Technical Features
- ✅ **API Versioning**: Supports v1.0 and v1 endpoint formats
- ✅ **Authentication**: JWT-based authentication with AuthService integration
- ✅ **Rate Limiting**: Configurable rate limits per endpoint and IP
- ✅ **Health Checks**: Liveness and readiness probes for Kubernetes
- ✅ **Swagger Documentation**: Interactive API documentation
- ✅ **Structured Logging**: Serilog with correlation IDs

## 🛠 Technology Stack

### Backend
- **Framework**: ASP.NET Core 9.0
- **Database**: PostgreSQL with Entity Framework Core
- **Caching**: In-Memory Cache with size limits
- **Authentication**: JWT tokens with custom middleware
- **Logging**: Serilog with structured logging

### Infrastructure
- **Containerization**: Docker with multi-stage builds
- **Orchestration**: Kubernetes with GKE
- **CI/CD**: GitHub Actions with GitOps workflow
- **Monitoring**: Health checks and observability
- **Security**: Non-root containers, secret management

### Development Tools
- **Testing**: xUnit, FluentAssertions, TestContainers
- **API Documentation**: Swagger/OpenAPI 3.0
- **Code Quality**: .NET analyzers and security scanning

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/get-started) (for containerized development)
- [PostgreSQL](https://www.postgresql.org/download/) (or use Docker Compose)
- [Git](https://git-scm.com/downloads)

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService.git
   cd Maliev.CurrencyService
   ```

2. **Using Docker Compose (Recommended)**
   ```bash
   docker-compose up -d
   ```
   - API: http://localhost:8080
   - Swagger: http://localhost:8080/currencies/swagger

3. **Local Development**
   ```bash
   # Restore dependencies
   dotnet restore Maliev.CurrencyService.sln
   
   # Update database connection in appsettings.Development.json
   # Run database migrations
   dotnet ef database update --project Maliev.CurrencyService.Data
   
   # Start the application
   dotnet run --project Maliev.CurrencyService.Api
   ```

### Environment Variables

```bash
# Database
ConnectionStrings__Default=Host=localhost;Port=5432;Database=currency_service_db;Username=postgres;Password=your-password

# JWT Configuration
Jwt__Issuer=your-issuer
Jwt__Audience=your-audience
Jwt__SecurityKey=your-secret-key

# Caching
Cache__CurrencyCacheDurationMinutes=60
Cache__SearchCacheDurationMinutes=30
Cache__MaxCacheSize=1000
```

## 📚 API Documentation

### Base URLs
- **Development**: `https://dev.api.maliev.com/currencies`
- **Staging**: `https://staging.api.maliev.com/currencies`
- **Production**: `https://api.maliev.com/currencies`

### Authentication
All endpoints require JWT authentication via the `Authorization` header:
```
Authorization: Bearer <jwt-token>
```

### Core Endpoints

#### Get All Currencies
```http
GET /currencies/v1.0
GET /currencies/v1.0?page=1&pageSize=20
GET /currencies/v1.0?search=Dollar
```

**Response**:
```json
{
  "items": [
    {
      "id": 1,
      "shortName": "USD",
      "longName": "US Dollar",
      "createdDate": "2018-07-23T19:51:25.713Z",
      "modifiedDate": "2018-09-30T19:30:04.797Z"
    }
  ],
  "totalCount": 153,
  "page": 1,
  "pageSize": 20
}
```

#### Get Currency by ID
```http
GET /currencies/v1.0/{id}
```

#### Create Currency
```http
POST /currencies/v1.0
Content-Type: application/json

{
  "shortName": "BTC",
  "longName": "Bitcoin"
}
```

#### Update Currency
```http
PUT /currencies/v1.0/{id}
Content-Type: application/json

{
  "shortName": "BTC",
  "longName": "Bitcoin (Updated)"
}
```

#### Delete Currency
```http
DELETE /currencies/v1.0/{id}
```

#### Get Currency Codes
```http
GET /currencies/v1.0/codes
```

### Health Check Endpoints
```http
GET /currencies/liveness    # Kubernetes liveness probe
GET /currencies/readiness   # Kubernetes readiness probe
```

### Interactive Documentation
Visit `/currencies/swagger` for interactive API documentation and testing.

## 💻 Development

### Project Structure
```
Maliev.CurrencyService/
├── Maliev.CurrencyService.Api/          # Web API layer
│   ├── Controllers/                     # REST API controllers
│   ├── Services/                        # Business logic services
│   ├── Models/                          # Request/response DTOs
│   ├── Middleware/                      # Custom middleware
│   ├── HealthChecks/                    # Health check implementations
│   └── Program.cs                       # Application entry point
├── Maliev.CurrencyService.Data/         # Data access layer
│   ├── DbContexts/                      # Entity Framework contexts
│   ├── Entities/                        # Database entities
│   ├── Extensions/                      # Data extensions and utilities
│   └── Migrations/                      # EF Core migrations
├── Maliev.CurrencyService.Tests/        # Test suite
│   ├── CurrencyControllerIntegrationTests.cs
│   ├── CurrencyServiceUnitTests.cs
│   └── HealthCheckTests.cs
└── docker-compose.yml                   # Local development stack
```

### Database Schema

**Currencies Table**:
```sql
CREATE TABLE "Currencies" (
    "Id" integer NOT NULL,
    "ShortName" character varying(3) NOT NULL,
    "LongName" character varying(100) NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "ModifiedDate" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Currencies" PRIMARY KEY ("Id"),
    CONSTRAINT "IX_Currencies_ShortName" UNIQUE ("ShortName"),
    CONSTRAINT "IX_Currencies_LongName" UNIQUE ("LongName")
);
```

### Adding New Currencies

The service includes 153 pre-seeded currencies. To add new currencies:

1. **Via API**: Use the POST endpoint to add currencies dynamically
2. **Via Migration**: Add currencies to the seed data migration
3. **Via CSV**: Update the CSV file and regenerate migrations

### Caching Strategy

The service implements a multi-level caching strategy:

- **Individual Currencies**: Cached by ID and short name (60min default)
- **Search Results**: Cached by search term and pagination (30min default)  
- **Currency Codes**: Cached list of all currency codes (60min default)
- **Cache Invalidation**: Automatic cache clearing on CUD operations

## 🧪 Testing

### Test Categories

1. **Unit Tests**: Business logic and service layer testing
2. **Integration Tests**: End-to-end API testing with TestContainers
3. **Health Check Tests**: Database connectivity and health monitoring

### Running Tests

```bash
# Run all tests
dotnet test Maliev.CurrencyService.sln --verbosity normal

# Run specific test categories
dotnet test --filter "FullyQualifiedName~UnitTests"
dotnet test --filter "FullyQualifiedName~IntegrationTests"
dotnet test --filter "FullyQualifiedName~HealthCheckTests"
```

### Test Coverage

- ✅ **18 Unit Tests**: Service layer and business logic
- ✅ **15 Integration Tests**: Full API endpoints with PostgreSQL
- ✅ **2 Health Check Tests**: Database connectivity validation

**Key Test Scenarios**:
- CRUD operations with validation
- Search functionality with various parameters
- Caching behavior and invalidation
- Error handling and edge cases
- Authentication and authorization
- Health check reliability

## 🚀 Deployment

### CI/CD Pipeline

GitHub Actions provides credential-free validation for pull requests, `main`,
`develop`, and `release/v*` tags. The shared validation job restores and audits
dependencies, builds the solution, and runs the complete test suite.

No workflow in this repository publishes container images, authenticates to
Google Cloud, modifies the GitOps repository, or deploys to Kubernetes. Release
promotion remains intentionally separate and pending Aspire owner review. The
owner must approve the final release after local Aspire validation; this CI
boundary does not authorize or perform a production cutover.

### Environment Configuration

**Development**:
- Registry: `asia-southeast1-docker.pkg.dev/maliev-website/maliev-website-artifact-dev`
- URL: `https://dev.api.maliev.com/currencies`
- Namespace: `maliev-dev`

**Staging**:
- Registry: `asia-southeast1-docker.pkg.dev/maliev-website/maliev-website-artifact-staging`
- URL: `https://staging.api.maliev.com/currencies` (ready to activate)
- Namespace: `maliev-staging`

**Production**:
- Registry: `asia-southeast1-docker.pkg.dev/maliev-website/maliev-website-artifact-prod`
- URL: `https://api.maliev.com/currencies` (ready to activate)
- Namespace: `maliev-prod`

### Manual Deployment

```bash
# Build Docker image
docker build -t maliev-currency-service -f Maliev.CurrencyService.Api/Dockerfile .

# Run with Docker Compose
docker-compose up -d

# Deploy to Kubernetes
kubectl apply -k ./k8s/overlays/development
```

## 📊 Monitoring

### Health Checks

The service provides comprehensive health monitoring:

- **Liveness Probe**: `/currencies/liveness` - Basic service health
- **Readiness Probe**: `/currencies/readiness` - Database connectivity and dependencies

### Logging

Structured logging with Serilog:

```json
{
  "timestamp": "2025-01-15T10:30:00.123Z",
  "level": "Information",
  "correlationId": "abc-123-def",
  "sourceContext": "CurrencyService.Api.Controllers.CurrenciesController",
  "message": "Retrieved currency USD successfully",
  "properties": {
    "currencyId": 1,
    "shortName": "USD"
  }
}
```

### Performance Metrics

- **Cache Hit Ratio**: Monitor cache effectiveness
- **Response Times**: Track API endpoint performance  
- **Database Queries**: Monitor EF Core query performance
- **Memory Usage**: Track cache size and memory consumption

### Alerts

Configure monitoring for:
- Health check failures
- High error rates (>5%)
- Slow response times (>2s)
- Cache miss ratio (>20%)
- Database connectivity issues

## 🤝 Contributing

### Development Workflow

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Make** your changes following the coding standards
4. **Add** tests for new functionality
5. **Commit** your changes: `git commit -m 'Add amazing feature'`
6. **Push** to your branch: `git push origin feature/amazing-feature`
7. **Create** a Pull Request

### Coding Standards

- Follow C# coding conventions and .NET guidelines
- Write comprehensive tests for new features
- Update documentation for API changes
- Use conventional commit messages
- Ensure all tests pass before submitting

### Security Guidelines

- Never commit secrets or connection strings
- Use environment variables for configuration
- Follow security best practices for JWT handling
- Implement proper input validation and sanitization
- Keep dependencies updated and scan for vulnerabilities

## 📄 License

This project is proprietary software of MALIEV Co. Ltd. All rights reserved.

## 🆘 Support

For support and questions:

- **Issues**: Create a GitHub issue for bugs and feature requests
- **Documentation**: Check the API documentation at `/currencies/swagger`
- **Team**: Contact the Maliev development team

## 📈 Roadmap

### Upcoming Features
- [ ] Redis caching integration
- [ ] Currency exchange rate API integration
- [ ] Historical currency data tracking
- [ ] Advanced filtering and sorting options
- [ ] Bulk operations support
- [ ] GraphQL API endpoint
- [ ] Real-time currency updates via SignalR

### Performance Improvements
- [ ] Database query optimization
- [ ] Advanced caching strategies
- [ ] Connection pooling enhancements
- [ ] Response compression

---

**Maliev Currency Service** - Part of the Maliev Co. Ltd. microservices ecosystem.
Built with ❤️ by the Maliev development team.

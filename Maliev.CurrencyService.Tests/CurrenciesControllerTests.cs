using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Currencies;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class CurrenciesControllerTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public CurrenciesControllerTests()
    {
        _currencyServiceMock = new Mock<ICurrencyService>();
        _loggerMock = new Mock<ILogger<CurrenciesController>>();
        _controller = new CurrenciesController(_currencyServiceMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task ListCurrencies_ReturnsOk()
    {
        var response = new PaginatedCurrencyResponse { Items = new List<CurrencyResponse>(), TotalCount = 0, Page = 1, PageSize = 50, TotalPages = 0 };
        _currencyServiceMock.Setup(s => s.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var result = await _controller.ListCurrencies();
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByCode_ReturnsOk()
    {
        var code = "USD";
        var response = new CurrencyResponse { Code = code, Symbol = "$", Name = "Dollar", DecimalPlaces = 2, IsActive = true, IsPrimary = false };
        _currencyServiceMock.Setup(s => s.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var result = await _controller.GetByCode(code);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateAdmin_ReturnsCreated()
    {
        var request = new Maliev.CurrencyService.Api.Models.Currencies.CreateCurrencyRequest { Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2 };
        var response = new CurrencyResponse { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true, IsPrimary = false };
        _currencyServiceMock.Setup(s => s.CreateAsync(It.IsAny<Maliev.CurrencyService.Api.Models.Currencies.CreateCurrencyRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var result = await _controller.CreateAdmin(request);
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task DeleteById_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _currencyServiceMock.Setup(s => s.DeleteByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await _controller.DeleteById(id);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Activate_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _currencyServiceMock.Setup(s => s.ActivateAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await _controller.Activate(id);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Deactivate_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _currencyServiceMock.Setup(s => s.DeactivateAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await _controller.Deactivate(id);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ReturnsOk()
    {
        var iso = "TH";
        var response = new CurrencyResponse { Code = "THB", Symbol = "฿", Name = "Baht", DecimalPlaces = 2, IsActive = true, IsPrimary = true };
        _currencyServiceMock.Setup(s => s.GetByCountryCodeAsync(iso, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var result = await _controller.GetCurrencyByCountry(iso);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateById_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var request = new Maliev.CurrencyService.Api.Models.Currencies.UpdateCurrencyRequest { Symbol = "$", Name = "Dollar", DecimalPlaces = 2 };
        var response = new CurrencyResponse { Id = id, Code = "USD", Symbol = "$", Name = "Dollar", DecimalPlaces = 2, IsActive = true, IsPrimary = false };
        _currencyServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        _currencyServiceMock.Setup(s => s.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var etag = ETagHelper.GenerateETag(response);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        var result = await _controller.UpdateById(id, request);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var code = "USD";
        _currencyServiceMock.Setup(s => s.DeleteAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await _controller.Delete(code);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetByCode_NotFound_ReturnsNotFound()
    {
        var code = "XXX";
        _currencyServiceMock.Setup(s => s.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync((CurrencyResponse?)null);
        var result = await _controller.GetByCode(code);
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateById_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new Maliev.CurrencyService.Api.Models.Currencies.UpdateCurrencyRequest();
        _currencyServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((CurrencyResponse?)null);

        // Add If-Match to pass that check
        _controller.Request.Headers.IfMatch = "\"some-etag\"";

        var result = await _controller.UpdateById(id, request);
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateById_Conflict_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var request = new Maliev.CurrencyService.Api.Models.Currencies.UpdateCurrencyRequest
        {
            Name = "Conflict",
            Symbol = "$",
            DecimalPlaces = 2
        };
        var response = new CurrencyResponse { Id = id, Code = "USD", Name = "Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, IsPrimary = false };

        _currencyServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        _currencyServiceMock.Setup(s => s.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var etag = ETagHelper.GenerateETag(response);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        var result = await _controller.UpdateById(id, request);
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, objectResult.StatusCode); // UpdateById maps it to 412
    }

    [Fact]
    public async Task UpdateById_PreconditionFailed_WhenETagMismatch()
    {
        var id = Guid.NewGuid();
        var request = new Maliev.CurrencyService.Api.Models.Currencies.UpdateCurrencyRequest();
        var response = new CurrencyResponse
        {
            Id = id,
            Code = "USD",
            Name = "Dollar",
            Symbol = "$",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = false
        };
        _currencyServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        _controller.Request.Headers.IfMatch = "\"wrong-etag\"";

        var result = await _controller.UpdateById(id, request);
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, objectResult.StatusCode);
    }
}

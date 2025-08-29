// <copyright file="GetCurrencyAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

#nullable enable

namespace Maliev.CurrencyService.Tests.Currencies
{
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Controllers;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest for GetCurrencyAsync.
    /// </summary>
    public class GetCurrencyAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<CurrenciesController>> _mockLogger;
        private readonly CurrenciesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetCurrencyAsync_UnitTest"/> class.
        /// </summary>
        public GetCurrencyAsync_UnitTest()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<CurrenciesController>>();
            _controller = new CurrenciesController(_mockCurrencyService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Currency exist, should return currency.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task CurrencyExist_ShouldReturnCurrency()
        {
            // Arrange
            int currencyId = 1;
            var currencyDto = new CurrencyDto { Id = currencyId, LongName = "MALIEV", ShortName = "MAL" };
            _mockCurrencyService.Setup(s => s.GetCurrencyByIdAsync(currencyId)).ReturnsAsync(currencyDto);

            // Act
            var actionResult = await _controller.GetCurrencyAsync(currencyId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<CurrencyDto>(okResult.Value);
            Assert.Equal(currencyId, returnValue.Id);
            _mockCurrencyService.Verify(s => s.GetCurrencyByIdAsync(currencyId), Times.Once);
        }

        /// <summary>
        /// Currency not exist, should return not found.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task CurrencyNotExist_ShouldReturnNotFound()
        {
            // Arrange
            int currencyId = int.MaxValue;
            _mockCurrencyService.Setup(s => s.GetCurrencyByIdAsync(currencyId)).ReturnsAsync((CurrencyDto?)null);

            // Act
            var actionResult = await _controller.GetCurrencyAsync(currencyId);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            _mockCurrencyService.Verify(s => s.GetCurrencyByIdAsync(currencyId), Times.Once);
        }
    }
}
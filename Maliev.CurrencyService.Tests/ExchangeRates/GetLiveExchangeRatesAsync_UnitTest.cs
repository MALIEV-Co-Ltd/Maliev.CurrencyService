// <copyright file="GetLiveExchangeRatesAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.CurrencyService.Tests.ExchangeRates
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Controllers;
    using Maliev.CurrencyService.Api.Services;
    using Maliev.CurrencyService.Data.Model;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest for GetLiveExchangeRatesAsync.
    /// </summary>
    public class GetLiveExchangeRatesAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<ExchangeRatesController>> _mockLogger;
        private readonly ExchangeRatesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetLiveExchangeRatesAsync_UnitTest"/> class.
        /// </summary>
        public GetLiveExchangeRatesAsync_UnitTest()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<ExchangeRatesController>>();
            _controller = new ExchangeRatesController(_mockCurrencyService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Get live exchange rate, should return live exchange rate.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task GetLiveExchangeRate_ShouldReturnLiveExchangeRate()
        {
            // Arrange
            string baseCurrency = "THB";
            string targetCurrency = "USD";
            var expectedRates = new OpenRatesModel
            {
                Base = baseCurrency,
                Date = DateTime.Today,
                Rates = new Dictionary<string, string> { { targetCurrency, "35.00" } }
            };

            _mockCurrencyService.Setup(s => s.GetLiveExchangeRatesAsync(baseCurrency, targetCurrency))
                                .ReturnsAsync(expectedRates);

            // Act
            var actionResult = await _controller.GetLiveExchangeRatesAsync(baseCurrency, targetCurrency);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<OpenRatesModel>(okResult.Value);
            Assert.NotNull(returnValue);
            Assert.Equal(expectedRates.Base, returnValue.Base);
            Assert.NotNull(returnValue.Rates);
            Assert.True(returnValue.Rates.ContainsKey(targetCurrency));
            Assert.Equal(expectedRates.Rates[targetCurrency], returnValue.Rates[targetCurrency]);
            _mockCurrencyService.Verify(s => s.GetLiveExchangeRatesAsync(baseCurrency, targetCurrency), Times.Once);
        }

        /// <summary>
        /// Invalid currencies, should return bad request.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Theory]
        [InlineData(null, "USD")]
        [InlineData("THB", null)]
        [InlineData("", "USD")]
        [InlineData("THB", "")]
        public async Task InvalidCurrencies_ShouldReturnBadRequest(string baseCurrency, string targetCurrency)
        {
            // Act
            var actionResult = await _controller.GetLiveExchangeRatesAsync(baseCurrency, targetCurrency);

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            _mockCurrencyService.Verify(s => s.GetLiveExchangeRatesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
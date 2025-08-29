// <copyright file="GetAllCurrenciesAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.CurrencyService.Tests.Currencies
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Controllers;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest for GetAllCurrenciesAsync.
    /// </summary>
    public class GetAllCurrenciesAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<CurrenciesController>> _mockLogger;
        private readonly CurrenciesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetAllCurrenciesAsync_UnitTest"/> class.
        /// </summary>
        public GetAllCurrenciesAsync_UnitTest()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<CurrenciesController>>();
            _controller = new CurrenciesController(_mockCurrencyService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Currencies exist, should return currencies.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task CurrencyExist_ShouldReturnCurrencies()
        {
            // Arrange
            var currencies = new List<CurrencyDto>
            {
                new CurrencyDto { Id = 1, LongName = "MALIEV1", ShortName = "MAL1" },
                new CurrencyDto { Id = 2, LongName = "MALIEV2", ShortName = "MAL2" },
                new CurrencyDto { Id = 3, LongName = "MALIEV3", ShortName = "MAL3" },
            };
            _mockCurrencyService.Setup(s => s.GetAllCurrenciesAsync()).ReturnsAsync(currencies);

            // Act
            var actionResult = await _controller.GetAllCurrenciesAsync();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnValue = Assert.IsType<List<CurrencyDto>>(okResult.Value);
            Assert.Equal(3, returnValue.Count());
            _mockCurrencyService.Verify(s => s.GetAllCurrenciesAsync(), Times.Once);
        }

        /// <summary>
        /// Currencies not exist, should return not found.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task CurrencyNotExist_ShouldReturnNotFound()
        {
            // Arrange
            _mockCurrencyService.Setup(s => s.GetAllCurrenciesAsync()).ReturnsAsync(new List<CurrencyDto>());

            // Act
            var actionResult = await _controller.GetAllCurrenciesAsync();

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            _mockCurrencyService.Verify(s => s.GetAllCurrenciesAsync(), Times.Once);
        }
    }
}
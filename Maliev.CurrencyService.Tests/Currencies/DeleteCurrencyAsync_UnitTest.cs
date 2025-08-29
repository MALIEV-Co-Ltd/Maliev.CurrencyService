// <copyright file="DeleteCurrencyAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.CurrencyService.Tests.Currencies
{
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Controllers;
    using Maliev.CurrencyService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest for DeleteCurrencyAsync.
    /// </summary>
    public class DeleteCurrencyAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<CurrenciesController>> _mockLogger;
        private readonly CurrenciesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteCurrencyAsync_UnitTest"/> class.
        /// </summary>
        public DeleteCurrencyAsync_UnitTest()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<CurrenciesController>>();
            _controller = new CurrenciesController(_mockCurrencyService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Currency exist, should return no content.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task CurrencyExist_ShouldReturnNoContent()
        {
            // Arrange
            int currencyId = 1;
            _mockCurrencyService.Setup(s => s.DeleteCurrencyAsync(currencyId)).ReturnsAsync(true);

            // Act
            var actionResult = await _controller.DeleteCurrencyAsync(currencyId);

            // Assert
            Assert.IsType<NoContentResult>(actionResult);
            _mockCurrencyService.Verify(s => s.DeleteCurrencyAsync(currencyId), Times.Once);
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
            _mockCurrencyService.Setup(s => s.DeleteCurrencyAsync(currencyId)).ReturnsAsync(false);

            // Act
            var actionResult = await _controller.DeleteCurrencyAsync(currencyId);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult);
            _mockCurrencyService.Verify(s => s.DeleteCurrencyAsync(currencyId), Times.Once);
        }
    }
}
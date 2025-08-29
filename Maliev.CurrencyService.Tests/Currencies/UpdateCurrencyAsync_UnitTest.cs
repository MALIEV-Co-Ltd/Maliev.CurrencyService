// <copyright file="UpdateCurrencyAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

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
    /// UnitTest for UpdateCurrencyAsync.
    /// </summary>
    public class UpdateCurrencyAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<CurrenciesController>> _mockLogger;
        private readonly CurrenciesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateCurrencyAsync_UnitTest"/> class.
        /// </summary>
        public UpdateCurrencyAsync_UnitTest()
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
            var request = new UpdateCurrencyRequest { LongName = "NewLong", ShortName = "NewShort" };
            _mockCurrencyService.Setup(s => s.UpdateCurrencyAsync(currencyId, It.IsAny<UpdateCurrencyRequest>())).ReturnsAsync(true);

            // Act
            var actionResult = await _controller.UpdateCurrencyAsync(currencyId, request);

            // Assert
            Assert.IsType<NoContentResult>(actionResult);
            _mockCurrencyService.Verify(s => s.UpdateCurrencyAsync(currencyId, It.IsAny<UpdateCurrencyRequest>()), Times.Once);
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
            var request = new UpdateCurrencyRequest { LongName = "NewLong", ShortName = "NewShort" };
            _mockCurrencyService.Setup(s => s.UpdateCurrencyAsync(currencyId, It.IsAny<UpdateCurrencyRequest>())).ReturnsAsync(false);

            // Act
            var actionResult = await _controller.UpdateCurrencyAsync(currencyId, request);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult);
            _mockCurrencyService.Verify(s => s.UpdateCurrencyAsync(currencyId, It.IsAny<UpdateCurrencyRequest>()), Times.Once);
        }

        /// <summary>
        /// Invalid currency data, should return bad request.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task InvalidCurrencyData_ShouldReturnBadRequest()
        {
            // Arrange
            int currencyId = 1;
            _controller.ModelState.AddModelError("ShortName", "ShortName is required"); // Simulate invalid model state

            // Act
            var actionResult = await _controller.UpdateCurrencyAsync(currencyId, new UpdateCurrencyRequest { ShortName = "", LongName = "" }); // Pass a request to trigger validation

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult);
        }
    }
}
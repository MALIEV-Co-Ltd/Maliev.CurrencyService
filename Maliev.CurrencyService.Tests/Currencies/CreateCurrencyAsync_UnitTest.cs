// <copyright file="CreateCurrencyAsync_UnitTest.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.CurrencyService.Tests.Currencies
{
    using System;
    using System.Threading.Tasks;
    using Maliev.CurrencyService.Api.Controllers;
    using Maliev.CurrencyService.Api.Models;
    using Maliev.CurrencyService.Api.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// UnitTest for CreateCurrencyAsync.
    /// </summary>
    public class CreateCurrencyAsync_UnitTest
    {
        private readonly Mock<ICurrencyService> _mockCurrencyService;
        private readonly Mock<ILogger<CurrenciesController>> _mockLogger;
        private readonly CurrenciesController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateCurrencyAsync_UnitTest"/> class.
        /// </summary>
        public CreateCurrencyAsync_UnitTest()
        {
            _mockCurrencyService = new Mock<ICurrencyService>();
            _mockLogger = new Mock<ILogger<CurrenciesController>>();
            _controller = new CurrenciesController(_mockCurrencyService.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Invalid currency, should return bad request.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task InvalidCurrency_ShouldReturnBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("ShortName", "ShortName is required"); // Simulate invalid model state

            // Act
            var actionResult = await _controller.CreateCurrencyAsync(new CreateCurrencyRequest { ShortName = "", LongName = "" }); // Pass a request to trigger validation

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }

        /// <summary>
        /// Valid currency, should return created at route.
        /// </summary>
        /// <returns>
        ///   <see cref="Task" />.
        /// </returns>
        [Fact]
        public async Task ValidCurrency_ShouldReturnCreatedAtRoute()
        {
            // Arrange
            var request = new CreateCurrencyRequest
            {
                LongName = "Long",
                ShortName = "Short",
            };
            var createdCurrencyDto = new CurrencyDto
            {
                Id = 1,
                LongName = request.LongName,
                ShortName = request.ShortName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
            };

            _mockCurrencyService.Setup(s => s.CreateCurrencyAsync(It.IsAny<CreateCurrencyRequest>()))
                                .ReturnsAsync(createdCurrencyDto);

            // Act
            var actionResult = await _controller.CreateCurrencyAsync(request);

            // Assert
            var createdAtRouteResult = Assert.IsType<CreatedAtRouteResult>(actionResult.Result);
            var returnValue = Assert.IsType<CurrencyDto>(createdAtRouteResult.Value);
            Assert.Equal(createdCurrencyDto.Id, returnValue.Id);
            Assert.Equal(createdCurrencyDto.LongName, returnValue.LongName);
            Assert.Equal(createdCurrencyDto.ShortName, returnValue.ShortName);
        }
    }
}
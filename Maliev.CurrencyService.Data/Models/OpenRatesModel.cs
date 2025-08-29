// <copyright file="OpenRatesModel.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.CurrencyService.Data.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Open Rates Model.
    /// </summary>
    public class OpenRatesModel
    {
        /// <summary>
        /// Gets or sets the base currency.
        /// </summary>
        /// <value>
        /// The base.
        /// </value>
        public required string Base { get; set; }

        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        /// <value>
        /// The date.
        /// </value>
        public DateTime Date { get; set; }

        /// <summary>
        /// Gets or sets the rates.
        /// </summary>
        /// <value>
        /// The rates.
        /// </value>
        public required Dictionary<string, string> Rates { get; set; }
    }
}
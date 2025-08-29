namespace Maliev.CurrencyService.Api.Models
{
    using System;

    /// <summary>
    /// Data Transfer Object for Currency.
    /// </summary>
    public class CurrencyDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the currency.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the short name of the currency (e.g., USD, EUR).
        /// </summary>
        public required string ShortName { get; set; }

        /// <summary>
        /// Gets or sets the long name of the currency (e.g., United States Dollar, Euro).
        /// </summary>
        public required string LongName { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the currency record was created.
        /// </summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the currency record was last modified.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }
    }
}
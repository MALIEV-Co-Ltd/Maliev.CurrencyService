namespace Maliev.CurrencyService.Api.Models
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Request DTO for creating a new currency.
    /// </summary>
    public class CreateCurrencyRequest
    {
        /// <summary>
        /// Gets or sets the short name of the currency (e.g., USD, EUR).
        /// </summary>
        [Required]
        [StringLength(10, MinimumLength = 3)]
        public required string ShortName { get; set; }

        /// <summary>
        /// Gets or sets the long name of the currency (e.g., United States Dollar, Euro).
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public required string LongName { get; set; }
    }
}
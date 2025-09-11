using System.Globalization;
using System.Text;
using Maliev.CurrencyService.Data.Entities;

namespace Maliev.CurrencyService.Data.Extensions;

public static class CurrencySeedDataExtensions
{
    public static List<Currency> LoadCurrenciesFromCsv(string csvFilePath)
    {
        var currencies = new List<Currency>();

        if (!File.Exists(csvFilePath))
        {
            return currencies;
        }

        try
        {
            // Read the CSV file with UTF-16 encoding (Little Endian)
            var lines = File.ReadAllLines(csvFilePath, Encoding.Unicode);
            
            // Skip the header row
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(';');
                if (parts.Length >= 5)
                {
                    // Parse the CSV data
                    var id = int.Parse(parts[0].Trim());
                    var shortName = parts[1].Trim();
                    var longName = parts[2].Trim();
                    
                    // Parse the dates - they're in SQL Server format
                    var createdDateStr = parts[3].Trim();
                    var modifiedDateStr = parts[4].Trim();
                    
                    DateTime.TryParse(createdDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdDate);
                    DateTime.TryParse(modifiedDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var modifiedDate);

                    // Convert to UTC for consistency
                    if (createdDate.Kind == DateTimeKind.Unspecified)
                    {
                        createdDate = DateTime.SpecifyKind(createdDate, DateTimeKind.Utc);
                    }
                    if (modifiedDate.Kind == DateTimeKind.Unspecified)
                    {
                        modifiedDate = DateTime.SpecifyKind(modifiedDate, DateTimeKind.Utc);
                    }

                    currencies.Add(new Currency
                    {
                        Id = id,
                        ShortName = shortName,
                        LongName = longName,
                        CreatedDate = createdDate,
                        ModifiedDate = modifiedDate
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty list if CSV parsing fails
            Console.WriteLine($"Error parsing currency CSV: {ex.Message}");
        }

        return currencies;
    }
}
using System.Globalization;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services;

public static class ProductCsvImporter
{
    public static List<Product> Parse(string content, ICollection<string> existingBarcodes, out int skippedDuplicates)
        => Parse(content, existingBarcodes, [], out skippedDuplicates, out _);

    public static List<Product> Parse(
        string content,
        ICollection<string> existingBarcodes,
        out int skippedDuplicates,
        out int skippedInvalid)
        => Parse(content, existingBarcodes, [], out skippedDuplicates, out skippedInvalid);

    public static List<Product> Parse(
        string content,
        ICollection<string> existingBarcodes,
        ICollection<string> existingNames,
        out int skippedDuplicates,
        out int skippedInvalid)
    {
        skippedDuplicates = 0;
        skippedInvalid = 0;
        var products = new List<Product>();
        var usedBarcodes = new HashSet<string>(existingBarcodes.Where(b => !string.IsNullOrWhiteSpace(b)), StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(existingNames.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(content.Trim().TrimStart('\uFEFF'));
        var first = reader.ReadLine();
        if (first == null)
        {
            return products;
        }

        if (!LooksLikeHeader(first))
        {
            AddLine(first, usedBarcodes, usedNames, products, ref skippedDuplicates, ref skippedInvalid);
        }

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            AddLine(line, usedBarcodes, usedNames, products, ref skippedDuplicates, ref skippedInvalid);
        }

        return products;
    }

    private static bool LooksLikeHeader(string line)
    {
        var first = Split(line).FirstOrDefault() ?? "";
        return first.Equals("Name", StringComparison.OrdinalIgnoreCase)
            || first.Equals("الاسم", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddLine(
        string line,
        HashSet<string> usedBarcodes,
        HashSet<string> usedNames,
        List<Product> products,
        ref int skippedDuplicates,
        ref int skippedInvalid)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var cols = Split(line);
        var name = Get(cols, 0);
        if (string.IsNullOrWhiteSpace(name))
        {
            skippedInvalid++;
            return;
        }

        name = name.Trim();
        if (!usedNames.Add(name))
        {
            skippedDuplicates++;
            return;
        }

        var barcode = string.IsNullOrWhiteSpace(Get(cols, 1)) ? null : Get(cols, 1).Trim();
        if (barcode != null && !usedBarcodes.Add(barcode))
        {
            skippedDuplicates++;
            usedNames.Remove(name);
            return;
        }

        var unit = Get(cols, 5).Equals("Kilo", StringComparison.OrdinalIgnoreCase) ? "Kilo" : "Piece";
        DateTime? expiry = null;
        if (DateTime.TryParse(Get(cols, 7), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            expiry = parsed.Date;
        }

        var product = new Product
        {
            Name = name,
            Barcode = barcode,
            CostPrice = ParseDecimal(Get(cols, 2)),
            SalePrice = ParseDecimal(Get(cols, 3)),
            StockQuantity = ParseDecimal(Get(cols, 4)),
            Unit = unit,
            Category = ProductCategories.Normalize(Get(cols, 6)),
            ExpiryDate = expiry,
            TrackInventory = true,
            ReorderLevel = 0,
            RowVersion = [1]
        };

        if (!InputRules.IsValidProduct(product))
        {
            skippedInvalid++;
            if (barcode != null)
            {
                usedBarcodes.Remove(barcode);
            }
            usedNames.Remove(name);
            return;
        }

        products.Add(product);
    }

    private static string[] Split(string line)
        => line.Split(',', StringSplitOptions.TrimEntries);

    private static string Get(IReadOnlyList<string> cols, int index)
        => index < cols.Count ? cols[index] : "";

    private static decimal ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
            ? n
            : 0m;
}

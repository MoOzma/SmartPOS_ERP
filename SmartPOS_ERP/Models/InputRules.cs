using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SmartPOS_ERP.Models;

public static class InputRules
{
    public const string SalePriceMustBePositive = "سعر البيع يجب أن يكون أكبر من صفر.";
    public const string CostPriceMustBePositive = "سعر التكلفة يجب أن يكون أكبر من صفر.";
    public const string AmountMustBePositive = "المبلغ يجب أن يكون أكبر من صفر.";
    public const string QuantityMustBePositive = "الكمية يجب أن تكون أكبر من صفر.";
    public const string QuantityCannotBeNegative = "الكمية لا يمكن أن تكون سالبة.";
    public const string DiscountCannotBeNegative = "الخصم لا يمكن أن يكون سالباً.";
    public const string TaxPercentRange = "النسبة يجب أن تكون بين 0 و 100.";
    public const string ReorderCannotBeNegative = "حد إعادة الطلب لا يمكن أن يكون سالباً.";
    public const string PackageCostMustBePositive = "سعر التوريد يجب أن يكون أكبر من صفر.";
    public const string UnitsPerPackageMustBePositive = "عدد الوحدات في العبوة يجب أن يكون أكبر من صفر.";
    public const string UnitInvalid = "وحدة البيع يجب أن تكون قطعة أو كيلو.";
    public const string DescriptionRequired = "الوصف مطلوب.";
    public const string SaleMustExceedCost = "سعر البيع يجب أن يكون أكبر من سعر الشراء.";
    public const string CartonPriceMustBePositive = "سعر الكرتون يجب أن يكون أكبر من صفر.";
    public const string PiecesPerCartonMustBePositive = "عدد القطع في الكرتون يجب أن يكون أكبر من صفر.";
    public const string CartonCountCannotBeNegative = "عدد الكراتين لا يمكن أن يكون سالباً.";
    public const string PieceCostTooLow = "سعر القطعة أقل من ناتج سعر الكرتون ÷ عدد القطع، وهذا يضخّم الربح.";
    public const string BarcodeTaken = "الباركود مستخدم لمنتج آخر.";
    public const string NameTaken = "اسم المنتج مستخدم بالفعل.";

    public static bool IsPositive(decimal value) => value > 0;

    public static bool IsNonNegative(decimal value) => value >= 0;

    public static bool IsTaxPercent(decimal value) => value is >= 0 and <= 100;

    public static bool IsKnownUnit(string? unit)
        => string.Equals(unit, "Piece", StringComparison.OrdinalIgnoreCase)
           || string.Equals(unit, "Kilo", StringComparison.OrdinalIgnoreCase);

    public static void ApplyProduct(ModelStateDictionary modelState, Product product)
    {
        foreach (var (key, message) in ProductErrors(product))
        {
            AddError(modelState, key, message);
        }
    }

    public static void AddError(ModelStateDictionary modelState, string key, string message)
    {
        if (modelState.TryGetValue(key, out var entry) && entry.Errors.Any(e => e.ErrorMessage == message))
        {
            return;
        }

        modelState.AddModelError(key, message);
    }

    public static IEnumerable<(string Key, string Message)> ProductErrors(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            yield return (nameof(Product.Name), "اسم المنتج مطلوب");
        }

        if (!IsKnownUnit(product.Unit))
        {
            yield return (nameof(Product.Unit), UnitInvalid);
        }

        if (!IsPositive(product.SalePrice))
        {
            yield return (nameof(Product.SalePrice), SalePriceMustBePositive);
        }

        if (!IsPositive(product.CostPrice))
        {
            yield return (nameof(Product.CostPrice), CostPriceMustBePositive);
        }

        if (SaleExceedsCost(product.SalePrice, product.CostPrice) is string markup)
        {
            yield return (nameof(Product.SalePrice), markup);
        }

        if (!IsNonNegative(product.StockQuantity))
        {
            yield return (nameof(Product.StockQuantity), QuantityCannotBeNegative);
        }

        if (product.ReorderLevel < 0)
        {
            yield return (nameof(Product.ReorderLevel), ReorderCannotBeNegative);
        }

        if (!IsTaxPercent(product.TaxRate))
        {
            yield return (nameof(Product.TaxRate), TaxPercentRange);
        }

        foreach (var carton in PieceCartonErrors(product, requireCarton: false))
        {
            yield return carton;
        }
    }

    public static bool IsValidProduct(Product product) => !ProductErrors(product).Any();

    public static string? PurchaseLine(PurchaseItemViewModel item)
    {
        if (item.PackageQuantity <= 0)
        {
            return QuantityMustBePositive;
        }

        if (item.UnitsPerPackage <= 0)
        {
            return UnitsPerPackageMustBePositive;
        }

        if (item.PackageCost is decimal cost && cost <= 0)
        {
            return PackageCostMustBePositive;
        }

        if (item.NewSalePrice is decimal sale && sale <= 0)
        {
            return SalePriceMustBePositive;
        }

        if (item.NewSalePrice is decimal newSale
            && item.PackageCost is decimal packageCost
            && item.UnitsPerPackage > 0
            && SaleExceedsCost(newSale, packageCost / item.UnitsPerPackage) is string markup)
        {
            return markup;
        }

        return null;
    }

    public static void ApplyPieceCarton(ModelStateDictionary modelState, Product product)
    {
        foreach (var (key, message) in PieceCartonErrors(product, requireCarton: true))
        {
            AddError(modelState, key, message);
        }
    }

    public static decimal? PieceCostFromCarton(Product product)
    {
        if (!string.Equals(product.Unit, "Piece", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (product.CartonPrice is not decimal cartonPrice || cartonPrice <= 0)
        {
            return null;
        }

        if (product.PiecesPerCarton is not decimal pieces || pieces <= 0)
        {
            return null;
        }

        return Math.Round(cartonPrice / pieces, 2);
    }

    public static string? PieceCostBelowCarton(Product product)
    {
        var expected = PieceCostFromCarton(product);
        if (expected is decimal unitCost && product.CostPrice > 0 && Math.Round(product.CostPrice, 2) < unitCost)
        {
            return PieceCostTooLow;
        }

        return null;
    }

    public static IEnumerable<(string Key, string Message)> PieceCartonErrors(Product product, bool requireCarton)
    {
        if (!string.Equals(product.Unit, "Piece", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var hasCarton = (product.CartonCount ?? 0) != 0
            || (product.CartonPrice ?? 0) != 0
            || (product.PiecesPerCarton ?? 0) != 0;
        if (!requireCarton && !hasCarton)
        {
            yield break;
        }

        if ((product.CartonCount ?? 0) < 0)
        {
            yield return (nameof(Product.CartonCount), CartonCountCannotBeNegative);
        }

        if (product.CartonPrice is not decimal cartonPrice || cartonPrice <= 0)
        {
            yield return (nameof(Product.CartonPrice), CartonPriceMustBePositive);
        }

        if (product.PiecesPerCarton is not decimal pieces || pieces <= 0)
        {
            yield return (nameof(Product.PiecesPerCarton), PiecesPerCartonMustBePositive);
        }

        if (PieceCostBelowCarton(product) is string below)
        {
            yield return (nameof(Product.CostPrice), below);
        }
    }

    public static string? SaleExceedsCost(decimal sale, decimal cost)
        => sale > 0 && cost > 0 && sale <= cost ? SaleMustExceedCost : null;
}

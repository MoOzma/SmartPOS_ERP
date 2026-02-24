public class ProductMovementViewModel
{
    public DateTime Date { get; set; }
    public string Type { get; set; }      // صادر أو وارد
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }

    public string Reference { get; set; } // رقم الفاتورة
}
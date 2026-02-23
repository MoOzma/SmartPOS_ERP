using System;
using System.Collections.Generic;

namespace SmartPOS_ERP.Models
{
    public class PurchaseViewModel
    {
        public int SupplierId { get; set; } // معرف المورد المختار
        public DateTime PurchaseDate { get; set; } // تاريخ الفاتورة

        // قائمة بالمنتجات التي تم إضافتها في الجدول داخل الصفحة
        public List<PurchaseItemViewModel> Items { get; set; } = new List<PurchaseItemViewModel>();
    }

    public class PurchaseItemViewModel
    {
        public int ProductId { get; set; }
        public decimal PackageQuantity { get; set; } // الكمية بالعلبة
        public decimal UnitsPerPackage { get; set; }  // معامل التحويل
        public decimal PackageCost { get; set; }      // سعر شراء العلبة
    }
}
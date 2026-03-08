using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
namespace SmartPOS_ERP.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        //I7
        public OrderController(ApplicationDbContext context)
        {
            _context = context;
            ///;;;;;
        }
        public async Task<IActionResult> Index()
        {
            // جلب الفواتير مرتبة من الأحدث للأقدم مع تفاصيل المنتجات
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReturn(int orderId, int productId, int returnQty)
        {
            // 1. جلب بيانات المنتج وتفاصيل الطلب
            var product = await _context.Products.FindAsync(productId);
            var orderDetail = await _context.OrderDetails
                .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ProductId == productId);

            if (orderDetail == null || returnQty > orderDetail.Quantity)
            {
                return Json(new { success = false, message = "الكمية المرتجعة أكبر من المباعة!" });
            }

            // 2. تحديث المخزون (إعادة المنتج للمخزن)
            product.StockQuantity += returnQty;

            // 3. إنشاء سجل المرتجع
            var salesReturn = new SalesReturn
            {
                OrderId = orderId,
                ProductId = productId,
                Quantity = returnQty,
                RefundAmount = returnQty * orderDetail.UnitPrice,
                ReturnDate = DateTime.Now
            };

            // 4. تحديث تفاصيل الطلب (تقليل الكمية المباعة)
            orderDetail.Quantity -= returnQty;

            // إذا تم إرجاع الكمية بالكامل، يمكن حذف السطر أو تركه بطلب صفر
            if (orderDetail.Quantity == 0)
            {
                // _context.OrderDetails.Remove(orderDetail); // اختياري
            }

            _context.SalesReturns.Add(salesReturn);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تمت عملية الارتجاع وتحديث المخزن بنجاح" });
        }
    }

}

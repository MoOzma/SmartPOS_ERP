using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Services
{
    public class StockLedgerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private int? _openShiftId;
        private bool _openShiftResolved;

        public StockLedgerService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public void ApplyDelta(Product product, decimal delta, string movementType, string reason, string? reference)
        {
            var before = product.StockQuantity;
            var after = before + delta;
            product.StockQuantity = after;
            Append(product.Id, before, after, delta, movementType, reason, reference);
        }

        public void RecordSnapshot(int productId, decimal before, decimal after, string movementType, string reason, string? reference)
        {
            Append(productId, before, after, after - before, movementType, reason, reference);
        }

        private void Append(int productId, decimal before, decimal after, decimal change, string movementType, string reason, string? reference)
        {
            var userName = _httpContextAccessor.HttpContext?.User.Identity?.Name;
            _context.StockLedgers.Add(new StockLedger
            {
                ProductId = productId,
                OccurredAt = DateTime.Now,
                MovementType = movementType,
                QuantityBefore = before,
                QuantityAfter = after,
                QuantityChange = change,
                Reason = reason,
                Reference = reference,
                UserName = userName,
                ShiftId = CurrentOpenShiftId(userName)
            });
        }

        private int? CurrentOpenShiftId(string? userName)
        {
            if (_openShiftResolved)
            {
                return _openShiftId;
            }

            _openShiftResolved = true;
            _openShiftId = _context.GetOpenShiftId(userName);
            return _openShiftId;
        }
    }
}

using Azure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SeniorDeveloperAdayaTes.Data;
using SeniorDeveloperAdayaTes.Models;
using System.Security.Cryptography;
using System.Text;
using static SeniorDeveloperAdayaTes.Models.OrderModels;

namespace SeniorDeveloperAdayaTes.Services
{
    public class OrderService
    {
        private readonly AppDbContexts _db;
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(30);

        public OrderService(AppDbContexts db)
        {
            _db = db;
        }

        public async Task<OrderResponse> CreateAsync(CreateOrderRequest request)
        {
            var items = request.Items
            .GroupBy(x => x.ProductId)
            .Select(x => new CreateOrderItemRequest
            {
                ProductId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            })
                .OrderBy(x => x.ProductId)
                .ToList();
            var orderKey = GenerateOrderKey(request, items);
            var now = DateTime.UtcNow;
            var expiredBefore = now.Subtract(DuplicateWindow);

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM dbo.IdempotencyKeys
            WHERE [Key] = {orderKey} AND CreatedAtUtc < {expiredBefore}");

            var existingKey = await _db.IdempotencyKeys
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Key == orderKey);

            if (existingKey?.OrderId != null)
                return await GetByIdAsync(existingKey.OrderId.Value);

            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var keyRecord = new IdempotencyKey
                {
                    Key = orderKey,
                    CreatedAtUtc = now
                };
                _db.IdempotencyKeys.Add(keyRecord);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsDuplicateKey(ex))
                {
                    throw new ApiException(409, "DUPLICATE_ORDER", "Order yang sama sedang atau sedang di proses");

                }
                var productIds = items.Select(x => x.ProductId).ToList();

                var product = await _db.products
                    .AsNoTracking()
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                if (product.Count != productIds.Count)
                    throw new ApiException(404, "PRODUCT_NOT_FOUND", "Ada Product yang tidak di temukan");

                foreach (var item in items)
                {
                    var success = await DecreaseStockAsync(item.ProductId, item.Quantity);

                    if (!success)
                        throw new ApiException(
                            409,
                            "INSUFFICIENT_STOCK",
                            $"Stock Product {item.ProductId} tidak mencukupi.");
                }
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = request.CustomerId,
                    ShippingAddress = request.ShippingAddress.Trim(),
                    Status = OrderStatus.Pending,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    Items = items.Select(item => new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = product[item.ProductId].Name,
                        Quantity = item.Quantity,
                        UnitPrice = product[item.ProductId].Price
                    }).ToList()
                };
                _db.orders.Add(order);
                await _db.SaveChangesAsync();

                keyRecord.OrderId = order.Id;
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ToResponse(order);





            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<OrderResponse> GetByIdAsync(Guid id)
        {
            var order = await _db.orders
                .AsNoTracking()
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == id);

            if (order == null)
                throw new ApiException(404, "ORDER_NOT_FOUND", "Order tidak ditemukan.");

            return ToResponse(order);
        }
        public async Task<PagedResponse<OrderResponse>> GetListAsync(
       OrderStatus? status,
       long? customerId,
       int page,
       int pageSize)
        {
            if (page < 1)
                throw new ApiException(400, "INVALID_PAGE", "Page minimal 1.");

            if (pageSize < 1 || pageSize > 100)
                throw new ApiException(400, "INVALID_PAGE_SIZE", "PageSize harus 1 sampai 100.");

            var query = _db.orders
                .AsNoTracking()
                .Include(x => x.Items)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(x => x.Status == status.Value);

            if (customerId.HasValue)
                query = query.Where(x => x.CustomerId == customerId.Value);

            var totalData = await query.CountAsync();

            var orders = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<OrderResponse>
            {
                Data = orders.Select(ToResponse).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalData = totalData,
                TotalPages = (int)Math.Ceiling(totalData / (double)pageSize)
            };
        }
        public async Task<OrderResponse> UpdateStatusAsync(Guid id, OrderStatus newStatus)
        {
            if (newStatus == OrderStatus.Cancelled)
                return await CancelAsync(id);

            var order = await _db.orders
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == id);

            if (order == null)
                throw new ApiException(404, "ORDER_NOT_FOUND", "Order tidak ditemukan.");

            if (!IsValidStatusChange(order.Status, newStatus))
                throw new ApiException(
                    422,
                    "INVALID_STATUS_TRANSITION",
                    $"Status {order.Status} tidak dapat diubah menjadi {newStatus}.");

            order.Status = newStatus;
            order.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ApiException(
                    409,
                    "ORDER_CONCURRENCY_CONFLICT",
                    "Order sudah diubah oleh admin lain. Silakan ambil data terbaru.");
            }

            return ToResponse(order);
        }
        public async Task<OrderResponse> CancelAsync(Guid id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var order = await _db.orders
                    .Include(x => x.Items)
                    .SingleOrDefaultAsync(x => x.Id == id);

                if (order == null)
                    throw new ApiException(404, "ORDER_NOT_FOUND", "Order tidak ditemukan.");

                if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                    throw new ApiException(
                        422,
                        "ORDER_CANNOT_BE_CANCELLED",
                        $"Order dengan status {order.Status} tidak dapat dibatalkan.");

                foreach (var item in order.Items)
                    await ReturnStockAsync(item.ProductId, item.Quantity);

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAtUtc = DateTime.UtcNow;

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new ApiException(
                        409,
                        "ORDER_CONCURRENCY_CONFLICT",
                        "Order sudah diubah oleh admin lain.");
                }

                await transaction.CommitAsync();

                return ToResponse(order);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private async Task<bool> DecreaseStockAsync(int productId, int quantity)
        {
            var affectedRows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.Products
            SET StockQuantity = StockQuantity - {quantity}
            WHERE Id = {productId}
              AND StockQuantity >= {quantity}");

            return affectedRows > 0;
        }

        private async Task ReturnStockAsync(int productId, int quantity)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE dbo.Products
            SET StockQuantity = StockQuantity + {quantity}
            WHERE Id = {productId}");
        }
        private static string GenerateOrderKey(
        CreateOrderRequest request,
        List<CreateOrderItemRequest> items)
        {
            var itemText = string.Join(";", items.Select(x => $"{x.ProductId}:{x.Quantity}"));
            var text = $"{request.CustomerId}|{request.ShippingAddress.Trim().ToUpperInvariant()}|{itemText}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

            return Convert.ToHexString(hash);
        }

        private static bool IsValidStatusChange(OrderStatus current, OrderStatus next)
        {
            return current switch
            {
                OrderStatus.Pending => next == OrderStatus.Confirmed || next == OrderStatus.Cancelled,
                OrderStatus.Confirmed => next == OrderStatus.Shipped || next == OrderStatus.Cancelled,
                OrderStatus.Shipped => next == OrderStatus.Delivered,
                _ => false
            };
        }

        private static bool IsDuplicateKey(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sqlException
                && (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

        private static OrderResponse ToResponse(Order order)
        {
            var items = order.Items.Select(x => new OrderItemResponse
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Subtotal = x.Quantity * x.UnitPrice
            }).ToList();

            return new OrderResponse
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                ShippingAddress = order.ShippingAddress,
                Status = order.Status,
                CreatedAtUtc = order.CreatedAtUtc,
                UpdatedAtUtc = order.UpdatedAtUtc,
                TotalAmount = items.Sum(x => x.Subtotal),
                Items = items
            };
        }
    }
}
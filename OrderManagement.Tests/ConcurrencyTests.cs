using Microsoft.EntityFrameworkCore;
using SeniorDeveloperAdayaTes.Data;
using SeniorDeveloperAdayaTes.Models;
using SeniorDeveloperAdayaTes.Services;
using System;
using System.Text.Json;
using Xunit;
using Assert = Xunit.Assert;


namespace OrderManagement.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Stock15_TwoOrdersOf10_OnlyOneSucceeds()
    {
        var options = new DbContextOptionsBuilder<AppDbContexts>()
            .UseSqlServer(GetConnectionString())
            .Options;

        int productId;

        await using (var setup = new AppDbContexts(options))
        {
           
            await setup.Database.ExecuteSqlRawAsync(@"
                DELETE FROM dbo.IdempotencyKeys
                WHERE OrderId IN
                (
                    SELECT Id FROM dbo.Orders
                    WHERE CustomerId IN (900001, 900002)
                );

                DELETE FROM dbo.Orders
                WHERE CustomerId IN (900001, 900002);");

            var product = await setup.products.SingleAsync(x => x.Name == "Product X");
            product.StockQuantity = 15;
            await setup.SaveChangesAsync();
            productId = product.Id;
        }

        
        var taskA = CreateOrder(options, 900001, productId);
        var taskB = CreateOrder(options, 900002, productId);

        var results = await Task.WhenAll(taskA, taskB);

        await using var check = new AppDbContexts(options);

        var finalStock = await check.products
            .Where(x => x.Id == productId)
            .Select(x => x.StockQuantity)
            .SingleAsync();

        var orderCount = await check.orders
            .CountAsync(x => x.CustomerId == 900001 || x.CustomerId == 900002);

        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(5, finalStock);
        Assert.Equal(1, orderCount);
    }

    private static async Task<bool> CreateOrder(
        DbContextOptions<AppDbContexts> options,
        long customerId,
        int productId)
    {
        await using var db = new AppDbContexts(options);
        var service = new OrderService(db);

        try
        {
            await service.CreateAsync(new CreateOrderRequest
            {
                CustomerId = customerId,
                ShippingAddress = "Concurrency Test",
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = productId, Quantity = 10 }
                }
            });

            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            return false;
        }
    }

    private static string GetConnectionString()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);

        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()
            ?? throw new InvalidOperationException("DefaultConnection tidak ditemukan.");
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeniorDeveloperAdayaTes.Data;
using SeniorDeveloperAdayaTes.Models;
using SeniorDeveloperAdayaTes.Services;
using System.Text.Json.Serialization;

namespace SeniorDeveloperAdayaTes
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages();

            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters
                        .Add(new JsonStringEnumConverter()));

            builder.Services.AddDbContext<AppDbContexts>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<OrderService>();

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors
                                .Select(e => e.ErrorMessage)
                                .ToArray());

                    return new BadRequestObjectResult(new
                    {
                        code = "VALIDATION_ERROR",
                        message = "Data request tidak valid.",
                        details = errors
                    });
                };
            });

            var app = builder.Build();

            // Handle error API
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (ApiException ex)
                {
                    context.Response.StatusCode = ex.StatusCode;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsJsonAsync(new
                    {
                        code = ex.Code,
                        message = ex.Message,
                        details = ex.Details
                    });
                }
                catch (Exception)
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsJsonAsync(new
                    {
                        code = "INTERNAL_SERVER_ERROR",
                        message = "Terjadi kesalahan pada server."
                    });
                }
            });

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();
            app.MapControllers();

            app.Run();
        }
    }
}
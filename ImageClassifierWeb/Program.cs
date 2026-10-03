using ImageClassifierWeb;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(o =>
    o.MultipartBodyLengthLimit = Limits.MaxImageBytes + 1024 * 1024);
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = Limits.MaxImageBytes + 1024 * 1024);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Внутрішня помилка сервера.");
    }));
}

app.UseRouting();

app.MapGet("/health", () => Results.Ok("OK"));
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// Прогріваємо модель у фоні, щоб перший запит не чекав завантаження TensorFlow.
_ = Task.Run(() =>
{
    try { _ = MLModel1.PredictEngine.Value; }
    catch (Exception ex) { Console.WriteLine($"Model warm-up failed: {ex}"); }
});

app.Run();

namespace ImageClassifierWeb
{
    public static class Limits
    {
        public const int MaxImageBytes = 10 * 1024 * 1024;
    }
}

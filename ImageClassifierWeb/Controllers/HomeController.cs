using System.Diagnostics;
using ImageClassifierWeb.Models;
using ImageClassifierWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageClassifierWeb.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new ResultViewModel());

    [HttpPost]
    [RequestSizeLimit(Limits.MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> Index(IFormFile? file, string? url)
    {
        var vm = new ResultViewModel();

        try
        {
            byte[] bytes;

            if (file is { Length: > 0 })
            {
                if (file.Length > Limits.MaxImageBytes)
                    throw new InvalidOperationException("Зображення завелике (максимум 10 МБ).");

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
                vm.Source = "завантажений файл";
            }
            else if (ImageDownloader.IsHttpUrl(url?.Trim()))
            {
                bytes = await ImageDownloader.DownloadAsync(url!.Trim());
                vm.Source = "посилання";
            }
            else
            {
                throw new InvalidOperationException(
                    "Завантажте файл із зображенням або вкажіть пряме посилання (http:// чи https://).");
            }

            if (bytes.Length == 0)
                throw new InvalidOperationException("Зображення порожнє.");

            var sw = Stopwatch.StartNew();
            var results = await Task.Run(() => Classifier.Classify(bytes));
            sw.Stop();

            if (results.Count == 0)
                throw new InvalidOperationException("Модель не змогла визначити об'єкт.");

            vm.Top = results.Take(3).ToList();
            vm.ElapsedMs = sw.ElapsedMilliseconds;
            vm.SizeBytes = bytes.Length;
            vm.ImageDataUrl = "data:" + GuessMime(bytes) + ";base64," + Convert.ToBase64String(bytes);
        }
        catch (InvalidOperationException ex)
        {
            vm.Error = ex.Message;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            vm.Error = "Не вдалося обробити зображення. Спробуйте інший файл (png, jpg, jpeg).";
        }

        return View(vm);
    }

    private static string GuessMime(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0x89 && b[1] == 0x50) return "image/png";
        if (b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8) return "image/jpeg";
        if (b.Length > 3 && b[0] == 0x47 && b[1] == 0x49) return "image/gif";
        return "application/octet-stream";
    }
}

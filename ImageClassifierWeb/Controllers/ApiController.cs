using System.Diagnostics;
using ImageClassifierWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageClassifierWeb.Controllers;

/// <summary>
/// Web API: POST /api/classify
///   multipart/form-data: file=&lt;зображення&gt;  АБО  url=&lt;пряме посилання&gt;
/// </summary>
[ApiController]
public class ApiController : ControllerBase
{
    [HttpPost("api/classify")]
    [RequestSizeLimit(Limits.MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> Classify([FromForm] IFormFile? file, [FromForm] string? url)
    {
        try
        {
            byte[] bytes;

            if (file is { Length: > 0 })
            {
                if (file.Length > Limits.MaxImageBytes)
                    return BadRequest(new { error = "Зображення завелике (максимум 10 МБ)." });

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
            }
            else if (ImageDownloader.IsHttpUrl(url?.Trim()))
            {
                bytes = await ImageDownloader.DownloadAsync(url!.Trim());
            }
            else
            {
                return BadRequest(new { error = "Передайте file або url." });
            }

            var sw = Stopwatch.StartNew();
            var results = await Task.Run(() => Classifier.Classify(bytes));
            sw.Stop();

            if (results.Count == 0)
                return UnprocessableEntity(new { error = "Модель не змогла визначити об'єкт." });

            var best = results[0];
            return Ok(new
            {
                label = best.Key,
                confidence = best.Value,
                confidencePercent = Math.Round(best.Value * 100, 1),
                elapsedMs = sw.ElapsedMilliseconds,
                top3 = results.Take(3).Select(r => new
                {
                    label = r.Key,
                    confidence = r.Value,
                    confidencePercent = Math.Round(r.Value * 100, 1)
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

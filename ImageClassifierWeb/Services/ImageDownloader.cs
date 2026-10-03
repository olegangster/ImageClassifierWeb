using System.Net;
using System.Net.Sockets;

namespace ImageClassifierWeb.Services;

public static class ImageDownloader
{
    private const int MaxBytes = 10 * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    public static bool IsHttpUrl(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static async Task<byte[]> DownloadAsync(string url)
    {
        if (!IsHttpUrl(url))
        {
            throw new InvalidOperationException(
                "Вкажіть коректне посилання, що починається з http:// або https://");
        }

        try
        {
            using var response = await Http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Сервер повернув помилку {(int)response.StatusCode}.");
            }

            string mediaType = response.Content.Headers.ContentType?.MediaType ?? "";

            if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "За цим посиланням знаходиться не зображення.");
            }

            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                throw new InvalidOperationException(
                    "Зображення завелике (максимум 10 МБ).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memory = new MemoryStream();

            var buffer = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                memory.Write(buffer, 0, read);

                if (memory.Length > MaxBytes)
                {
                    throw new InvalidOperationException(
                        "Зображення завелике (максимум 10 МБ).");
                }
            }

            return memory.ToArray();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(
                "Не вдалося завантажити зображення за цим посиланням.");
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException(
                "Час очікування завантаження вичерпано.");
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            MaxAutomaticRedirections = 3,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(
                    context.DnsEndPoint.Host,
                    cancellationToken);

                var address = addresses.FirstOrDefault(a => !IsPrivate(a))
                    ?? throw new HttpRequestException("Адреса недоступна.");

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(
                        new IPEndPoint(address, context.DnsEndPoint.Port),
                        cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ImageClassifier/1.0");

        return client;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.Equals(IPAddress.IPv6Any)
                || ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || ip.IsIPv6UniqueLocal;
        }

        var b = ip.GetAddressBytes();

        return b[0] == 0
            || b[0] == 10
            || b[0] == 127
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }
}

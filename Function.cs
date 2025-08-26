using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading;

namespace PWABuilder.SafeUrl;

public class Function
{
    private readonly ILogger<Function> log;
    private readonly HttpClient http;
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0 PWABuilderHttpAgent";

    public Function(ILogger<Function> logger, HttpClient httpClient)
    {
        log = logger;
        http = httpClient;
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    [Function("GetSafeUrl")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.com");
        // User agent is now set globally in the constructor

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var url = query["url"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            log.LogError("No valid URI specified. URL was {url}", url);
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (bool.TryParse(query["checkExistsOnly"], out var checkExistsOnly) && checkExistsOnly)
        {
            return await CheckExists(uri, req, cts.Token);
        }

        try
        {
            using var getMsg = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Version = new Version(2, 0)
            };
            var getResult = await http.SendAsync(getMsg, cts.Token);
            if (!getResult.IsSuccessStatusCode)
            {
                var errorResponse = req.CreateResponse(getResult.StatusCode);
                return errorResponse;
            }

            var contentType = getResult.Content.Headers.ContentType;
            var contentLength = getResult.Content.Headers.ContentLength;
            var maxSize = 1024 * 1024 * 10; // 10MB
            if (contentLength != null)
            {
                if (contentLength > maxSize)
                {
                    log.LogError("Content length must be 10MB or less");
                    var unsupportedResponse = req.CreateResponse(HttpStatusCode.UnsupportedMediaType);
                    return unsupportedResponse;
                }
            }

            var imgStream = await getResult.Content.ReadAsStreamAsync();
            var limitedStream = new LimitedReadStream(imgStream, maxSize, contentType?.MediaType);
            var fileResponse = req.CreateResponse(HttpStatusCode.OK);
            fileResponse.Headers.Add("Content-Type", !string.IsNullOrWhiteSpace(contentType?.MediaType) ? contentType.MediaType : "image/png");
            await limitedStream.CopyToAsync(fileResponse.Body);
            return fileResponse;
        }
        catch (OperationCanceledException error)
        {
            log.LogError(error, "GET request to {url} timed out.", uri);
            return req.CreateResponse(HttpStatusCode.GatewayTimeout);
        }
    }

    private async Task<HttpResponseData> CheckExists(Uri uri, HttpRequestData req, CancellationToken token)
    {
        try
        {
            using var headMsg = new HttpRequestMessage(HttpMethod.Head, uri)
            {
                Version = new Version(2, 0)
            };
            var headResult = await http.SendAsync(headMsg, token);
            // If HEAD not supported, try GET
            if (headResult.StatusCode == HttpStatusCode.MethodNotAllowed || headResult.StatusCode == HttpStatusCode.NotImplemented)
            {
                log.LogWarning("HEAD not supported for {uri}, falling back to GET.", uri);
                using var getMsg = new HttpRequestMessage(HttpMethod.Get, uri)
                {
                    Version = new Version(2, 0)
                };
                var getResult = await http.SendAsync(getMsg, HttpCompletionOption.ResponseHeadersRead, token);
                var response = req.CreateResponse(getResult.StatusCode);
                return response;
            }
            else
            {
                var response = req.CreateResponse(headResult.StatusCode);
                return response;
            }
        }
        catch (OperationCanceledException)
        {
            log.LogError("HEAD/GET request timed out.");
            var timeoutResponse = req.CreateResponse(HttpStatusCode.GatewayTimeout);
            return timeoutResponse;
        }
    }
}
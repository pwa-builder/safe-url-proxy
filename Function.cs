using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;

namespace PWABuilder.SafeImage
{
    public static class Function
    {
        private static readonly HttpClient http = CreateHttp();
        
        private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36 Edg/94.0.992.50";

        [FunctionName("GetSafeUrl")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var url = req.Query["url"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                log.LogError("No valid URI specified. URL was {url}", url);
                return new BadRequestResult();
            }

            // See if we're asked to simply check the existence of the image.
            bool.TryParse(req.Query["checkExistsOnly"], out var checkExistsOnly);
            if (checkExistsOnly)
            {
                // Sending HTTP HEAD checks for the existence of a resource without downloading it.
                using var headMsg = new HttpRequestMessage(HttpMethod.Head, uri)
                {
                    Version = new Version(2, 0)
                };
                var headResult = await http.SendAsync(headMsg);
                return new StatusCodeResult((int)headResult.StatusCode);
            }

            using var getMsg = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Version = new Version(2, 0)
            };
            var getResult = await http.SendAsync(getMsg);

            //getResult.EnsureSuccessStatusCode();
            if (!getResult.IsSuccessStatusCode)
            {
                return new StatusCodeResult((int)getResult.StatusCode);
            }
            
            var contentType = getResult.Content.Headers.ContentType;
            var contentLength = getResult.Content.Headers.ContentLength;
            if (contentLength > 20000000)
            {
                log.LogError("Content length must be 20MB or less");
                return new UnsupportedMediaTypeResult();
            }

            var imgStream = await getResult.Content.ReadAsStreamAsync();
            return new FileStreamResult(imgStream, contentType.MediaType);
        }

        private static HttpClient CreateHttp()
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            return http;
        }
    }
}

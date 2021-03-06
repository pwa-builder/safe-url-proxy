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
        private static readonly HttpClient http = new HttpClient();

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
    }
}

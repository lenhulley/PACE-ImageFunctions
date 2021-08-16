// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

// Learn how to locally debug an Event Grid-triggered function:
//    https://aka.ms/AA30pjh

// Use for local testing:
//   https://{ID}.ngrok.io/runtime/webhooks/EventGrid?functionName=Thumbnail

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace ImageFunctions
{
    public static class Thumbnail
    {
        //private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }


        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            extension = extension.Replace(".", "");

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }

        [FunctionName("Thumbnail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                //string imgURL = req.Query["imgURL"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                log.LogInformation("Full message body received: " + requestBody);

                var queryString = HttpUtility.ParseQueryString(requestBody);

                string imgURL = queryString["imgURL"].ToString();

                string responseMessage = string.IsNullOrEmpty(imgURL)
                    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                    : $"The image URL is: {imgURL}. " +
                    $"This HTTP triggered function executed successfully.";


                var extension = Path.GetExtension(imgURL);
                var encoder = GetEncoder(extension);


                if (encoder != null)
                {
                    var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH"));

                    var blobName = GetBlobNameFromUrl(imgURL);

                    var webClient = new WebClient();
                    byte[] imageBytes = webClient.DownloadData(imgURL);

                    using (var output = new MemoryStream())
                    {
                        using (Image<Rgba32> image = (Image<Rgba32>)Image.Load(imageBytes))
                        {
                            var divisor = image.Width / thumbnailWidth;
                            var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                            image.Mutate(x => x.Resize(thumbnailWidth, height));
                            image.Save(output, encoder);

                           
                            byte[] bytes = output.ToArray();

                            return new FileContentResult(bytes, "image/png");
                            //output.Position = 0;
                            // return new OkObjectResult(bytes);
                        }
                    }

                }

                else
                {
                    log.LogInformation($"No encoder support for: {imgURL}");
                    return new BadRequestObjectResult("Error");
                }
            }
            catch (Exception ex)
            {
                string error = ex.ToString();
                return new BadRequestObjectResult(error);
            }
        }
    }
}

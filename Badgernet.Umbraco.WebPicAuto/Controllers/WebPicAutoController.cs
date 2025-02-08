using Badgernet.WebPicAuto.Helpers;
using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;



namespace Badgernet.WebPicAuto.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class WebPicAutoController(
        IWebPicSettingProvider settingsProvider,
        ILogger<WebPicAutoController> logger,
        IMediaHelper mediaHelper,
        IFileManager fileManager,
        IImageProcessor imageProcessor,
        IUmbracoContextAccessor contextAccessor)
        : UmbracoAuthorizedJsonController
    {
        private WebPicSettings? _currentSettings;


        public ActionResult<WebPicSettings> GetSettings()
        {
            _currentSettings ??= settingsProvider.GetFromFile();
            _currentSettings ??= new WebPicSettings();//Create default settings

            return _currentSettings;
        }

        public IActionResult SetSettings([FromBody] WebPicSettings settings)
        {
            if (settings == null) return BadRequest(new { message = "Bad Request" });

            try
            {
                settingsProvider.PersistToFile(settings);
                return Ok(new { message = "Settings were saved." });
            }
            catch (Exception e)
            {
                logger.LogError("Error when saving wpa settings: {0}", e.Message);
                throw;
            }
        }

        
        // Checks media gallery for images that could potentially be optimized
        public ActionResult<dynamic> CheckMedia()
        {
            var allImages = GetAllImages();
            if (allImages == null)
            {
                logger.LogWarning("No existing images found");
                return NoContent();
            }

            _currentSettings ??= settingsProvider.GetFromFile();

            var optimizeCandidates = allImages.Where(img =>
                    (img.Width > _currentSettings.WpaTargetWidth || img.Height > _currentSettings.WpaTargetHeight) ||
                    (img.Extension != "webp" && img.Extension != "svg"))
                    .Select(img => new { id = img.Id, path = img.Path, size = $"{img.Width}x{img.Height}", extension = img.Extension });

            var toResizeCount = allImages.Count(img =>
                img.Width > _currentSettings.WpaTargetWidth || img.Height > _currentSettings.WpaTargetHeight);

            var toConvertCount = allImages.Count(img =>
                    img.Extension != "webp" && img.Extension != "svg");

            var result = new
            {
                toConvertCount,
                toResizeCount,
                optimizeCandidates
            };

            return Ok(result);
        }

        [HttpPost]
        public string ProcessExistingImages(JObject requestJson)
        {
            if (!requestJson.ContainsKey("ids") || !requestJson.ContainsKey("resize") ||
                !requestJson.ContainsKey("convert")) return "Bad Request";

            var imageIds = requestJson.Value<JArray>("ids");
            var resize = requestJson.Value<bool>("resize");
            var convert = requestJson.Value<bool>("convert");

            //Read current WebPic Settings from file. 
            var wpaSettings = settingsProvider.GetFromFile();
            var targetWidth = wpaSettings.WpaTargetWidth;
            var targetHeight = wpaSettings.WpaTargetHeight;

            if (imageIds == null || !imageIds.Any()) return "Need to select atleast one image first";

            foreach (var idToken in imageIds)
            {
                var imageId = idToken.Value<int>();
                var media = mediaHelper.GetMediaById(imageId);
                if (media == null)
                {
                    logger.LogError($"Could not find media with id: {imageId}");
                    continue;
                }

                var mediaPath = mediaHelper.GetRelativePath(media);
                var newMediaPath = fileManager.GetFreePath(mediaPath);
                var originalResolution = mediaHelper.GetUmbResolution(media);
                var newResolution = imageProcessor.CalculateResolution(originalResolution,
                    new Size(targetWidth, targetHeight), !wpaSettings.WpaIgnoreAspectRatio);
                var newFilename = Path.GetFileName(newMediaPath);

                //READ FILE INTO A STREAM THAT NEEDS TO BE MANUALLY DISPOSED
                var imageStream = fileManager.ReadFile(mediaPath);

                if (imageStream == null)
                {
                    logger.LogError("Image with id: {imageId} could not be read.", imageId);
                    continue;
                }

                //Image will be saved under this path if processing succeeds
                var finalSavingPath = string.Empty;

                try
                {
                    if (resize)
                    {
                        if (originalResolution.Width > targetWidth || originalResolution.Height > targetHeight)
                        {
                            using var resizedImageStream = imageProcessor.Resize(imageStream, newResolution);

                            if (resizedImageStream != null)
                            {
                                //Save size difference stats
                                var bytesSaved = imageStream.Length - resizedImageStream.Length;
                                wpaSettings.WpaBytesSavedResizing += bytesSaved;
                                wpaSettings.WpaResizerCounter++;

                                mediaHelper.SetUmbFilename(media, newFilename);
                                mediaHelper.SetUmbResolution(media, newResolution);

                                //Delete original image
                                if (!wpaSettings.WpaKeepOriginals)
                                {
                                    fileManager.DeleteFile(mediaPath);
                                }

                                // Reassign path
                                mediaPath = newMediaPath;

                                //Copy resized image to image stream
                                imageStream.ClearAndReassignStream(resizedImageStream);

                                finalSavingPath = mediaPath;
                            }
                            else
                            {
                                logger.LogError($"Failed to resize image with id: {media.Id}");
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Image with id: {media.Id} does not need resizing.");
                        }
                    }

                    if (convert)
                    {
                        if (!mediaPath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) &&
                            !mediaPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                        {
                            newMediaPath = Path.ChangeExtension(newMediaPath, ".webp");

                            var convertMode = ConvertMode.lossy;

                            if (wpaSettings.WpaConvertMode == "lossless") convertMode = ConvertMode.lossless;

                            using (var convertedImageStream = imageProcessor.ConvertToWebp(imageStream, convertMode,
                                       wpaSettings.WpaConvertQuality))
                            {
                                if (convertedImageStream != null)
                                {
                                    mediaHelper.SetUmbFilename(media, newFilename);
                                    mediaHelper.SetUmbExtension(media, ".webp");
                                    mediaHelper.SetUmbBytes(media, convertedImageStream.Length);

                                    if (!wpaSettings.WpaKeepOriginals)
                                    {
                                        //Delete original image (before extension change)
                                        fileManager.DeleteFile(mediaPath);
                                    }

                                    // Calculate bytes saved converting the image
                                    var bytesSaved = convertedImageStream.Length - imageStream.Length;
                                    wpaSettings.WpaBytesSavedConverting += bytesSaved;
                                    wpaSettings.WpaConverterCounter++;

                                    //Reassign image stream
                                    imageStream.ClearAndReassignStream(convertedImageStream);

                                    finalSavingPath = newMediaPath;

                                }
                                else
                                {
                                    logger.LogError($"Failed to convert image with id: {media.Id}");
                                }
                            }
                        }
                    }
                    
                    //If finalSavingPath is empty, there was no work done
                    if (finalSavingPath != string.Empty)
                    {
                        var writtenToDisk = false;
                        try
                        {
                            //Write image stream to file system  
                            fileManager.WriteFile(finalSavingPath, imageStream);
                            writtenToDisk = true;
                        }
                        catch
                        {
                            logger.LogError("Image with id: {id} could not be saved to file system.", media.Id);
                        }

                        if (writtenToDisk)
                        {
                            //Save processed media back to database
                            mediaHelper.SaveMedia(media);
                        }

                        settingsProvider.PersistToFile(wpaSettings);

                        //Dispose the stream
                        imageStream.Dispose();

                    }
                }
                catch (Exception ex)
                {
                    return "Error processing image";
                }
                
            }

            return "Success";
        }

        private IEnumerable<ImageInfo>? GetAllImages()
        {
            if (contextAccessor .TryGetUmbracoContext(out var context) == false)
                return null;
      
            if (context.Content == null)
                return null;

            var mediaRoot = context.Media!.GetAtRoot();

            var images = mediaRoot.DescendantsOrSelf<IPublishedContent>()
                .OfTypes("Image")
                .Select(i => new ImageInfo()
                {
                    Id =i.Id,
                    File = (ImageCropperValue?) (i.GetProperty("UmbracoFile")?.GetValue() ?? null),
                    Width = (int) (i.GetProperty("UmbracoWidth")?.GetValue() ?? 0),
                    Height = (int) (i.GetProperty("UmbracoHeight")?.GetValue() ?? 0),
                    Extension = (string) (i.GetProperty("UmbracoExtension")?.GetValue() ?? string.Empty)
                });

            return images;
        }

        private void DeleteFile(string path)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch 
                {
                    logger.LogError($"Could not delete file: {path}");
                }

            }
        }

    }
    

    public class ImageInfo()
    {
        public int Id { get; init; }
        public ImageCropperValue? File { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public string Extension { get; init; }
        public string? Path => File?.Src;
    }

}

using Badgernet.WebPicAuto.Helpers;
using Badgernet.WebPicAuto.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            if (requestJson == null) return "No data recieved";
            
            if (!requestJson.ContainsKey("ids") || !requestJson.ContainsKey("resize") || !requestJson.ContainsKey("convert")) return "Bad Request";

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

                var imagePath = mediaHelper.GetRelativePath(media);

                var originalSize = new Size();
                try
                {
                    originalSize.Width = int.Parse(media.GetValue<string>("umbracoWidth")!);
                    originalSize.Height = int.Parse(media.GetValue<string>("umbracoHeight")!);
                }
                catch
                {
                    logger.LogError($"Could not read media size: {imageId}");
                    continue; //Skip if dimensions cannot be parsed 
                }

                var newPath = _mediaHelper.GenerateAlternativePath(media);
                var newFilename = Path.GetFileName(newPath);


                try
                {
                    if (resize)
                    {
                        if (originalSize.Width > targetWidth || originalSize.Height > targetHeight)
                        {
                            var newSize = _mediaHelper.ResizeImageFile(imagePath, newPath, new Size(targetWidth, targetHeight), wpaSettings.WpaIgnoreAspectRatio);
                            if (newSize != null)
                            {
                                //Save size difference stats
                                var bytesSaved = _mediaHelper.FileSizeDiff(imagePath, newPath);
                                wpaSettings.WpaBytesSavedResizing += bytesSaved;
                                wpaSettings.WpaResizerCounter++;

                                _mediaHelper.ChangeFilename(media, newFilename);

                                //Get new file size
                                FileInfo newFile = new(newPath);
                                media.SetValue("umbracoBytes", newFile.Length);

                                //Delete original image
                                if (!wpaSettings.WpaKeepOriginals)
                                {
                                    DeleteFile(imagePath);
                                }

                                imagePath = newPath;
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
                        if (!imagePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) && !imagePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                        {
                            newPath = Path.ChangeExtension(newPath, ".webp");

                            if (_mediaHelper.ConvertImageFile(imagePath, newPath, wpaSettings.WpaConvertMode, wpaSettings.WpaConvertQuality))
                            {
                                //Calculate file size difference
                                var bytesSaved = _mediaHelper.FileSizeDiff(imagePath, newPath);
                                wpaSettings.WpaBytesSavedConverting += bytesSaved;
                                wpaSettings.WpaConverterCounter++;

                                _mediaHelper.ChangeFilename(media, newFilename);
                                _mediaHelper.ChangeExtension(media, ".webp");

                                //Get new file size
                                FileInfo newImageFile = new(newPath);
                                media.SetValue("umbracoBytes", newImageFile.Length);

                                //Delete original image
                                if (!wpaSettings.WpaKeepOriginals)
                                {
                                    DeleteFile(imagePath);
                                }
                            }
                            else
                            {
                                logger.LogError($"Error converting media with id: {imageId}");
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Image with id: {imageId} does not need converting.");
                        }
                    }

                    settingsProvider.PersistToFile(wpaSettings);
                    _mediaHelper.SaveMedia(media);
                }
                catch (Exception ex)
                {
                    logger.LogError($"There was a problem processing image: {ex.Message}");
                    return "Error processing image";
                }

            }

            return "Sucess";
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

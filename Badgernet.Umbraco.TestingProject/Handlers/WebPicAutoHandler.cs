using Badgernet.WebPicAuto.Settings;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Size = SixLabors.ImageSharp.Size;
using File = System.IO.File;
using Umbraco.Cms.Core.Scoping;
using System.Text.Json.Nodes;
using Badgernet.WebPicAuto.Helpers;
using Microsoft.Extensions.Logging;


namespace Badgernet.WebPicAuto.Handlers
{
    public class WebPicAutoHandler(IWebPicSettingProvider wpaSettingsProvider,
                                   //IWebPicHelper webPicHelper,
                                   ICoreScopeProvider scopeProvider,
                                   IMediaHelper mediaHelper,
                                   IFileManager fileManager,
                                   IImageProcessor imageProcessor,
                                   ILogger<WebPicAutoHandler> logger) : INotificationHandler<MediaSavingNotification>
    {
        private readonly IWebPicSettingProvider _settingsProvider = wpaSettingsProvider;
        //private readonly IWebPicHelper _mediaHelper = webPicHelper;
        private readonly ICoreScopeProvider _scopeProvider = scopeProvider;
        private readonly ILogger<WebPicAutoHandler> _logger = logger;

        public void Handle(MediaSavingNotification notification)
        {
            var wpaSettings = _settingsProvider.GetFromFile();
            
            bool resizingEnabled = wpaSettings.WpaEnableResizing;
            bool convertingEnabled = wpaSettings.WpaEnableConverting;
            int convertQuality = wpaSettings.WpaConvertQuality;
            bool ignoreAspectRatio = wpaSettings.WpaIgnoreAspectRatio;
            int targetWidth = wpaSettings.WpaTargetWidth;
            int targetHeight = wpaSettings.WpaTargetHeight;
            bool keepOriginals = wpaSettings.WpaKeepOriginals;
            string convertMode = wpaSettings.WpaConvertMode;
            string ignoreKeyword = wpaSettings.WpaIgnoreKeyword;

            //Prevent Options being out of bounds 
            if (targetHeight < 1) targetHeight = 1;
            if (targetWidth < 1) targetWidth = 1;
            if (convertQuality < 1) convertQuality = 1;
            if (convertQuality > 100) convertQuality = 100;


            foreach(var media in notification.SavedEntities)
            {
                if (media == null) continue;

                //Skip if not an image
                if (string.IsNullOrEmpty(media.ContentType.Alias) || !media.ContentType.Alias.Equals("image", StringComparison.CurrentCultureIgnoreCase)) continue;  
                
                //Skip any not-new images
                if (media.Id > 0) continue;
                
                string originalFilepath = mediaHelper.GetRelativePath(media);
                string alternativeFilepath = fileManager.GetFreePath(originalFilepath);
                Size originalSize = new();

                //Skip if paths not good
                if (string.IsNullOrEmpty(originalFilepath) || string.IsNullOrEmpty(alternativeFilepath)) continue;

                //Skip if image name contains "ignoreKeyword"
                if (Path.GetFileNameWithoutExtension(originalFilepath).Contains(ignoreKeyword,StringComparison.CurrentCultureIgnoreCase)) 
                {
                    // alternativeFilepath = originalFilepath.Replace(ignoreKeyword, string.Empty);
                    // File.Move(originalFilepath, alternativeFilepath, true);

                    // var jsonString = media.GetValue<string>("umbracoFile");

                    // if (jsonString == null) continue;

                    // var propNode = JsonNode.Parse((string)jsonString);
                    // string? path = propNode!["src"]!.GetValue<string>();
                    // path = path.Replace(ignoreKeyword, string.Empty);

                    // propNode["src"] = path;

                    // media.SetValue("umbracoFile", propNode.ToJsonString());
                    // if(media.Name != null)
                    // {
                    //     media.Name = media.Name.Replace(ignoreKeyword, string.Empty,StringComparison.CurrentCultureIgnoreCase);
                    // }

                    continue;
                }
                
                using var scope = _scopeProvider.CreateCoreScope(autoComplete: true);
                using var _ = scope.Notifications.Suppress();

                //Read resolution      
                try
                {
                    originalSize.Width = int.Parse(media.GetValue<string>("umbracoWidth")!);
                    originalSize.Height = int.Parse(media.GetValue<string>("umbracoHeight")!);
                }
                catch
                {
                    continue; //Skip if resolution cannot be parsed 
                }

                //Override appsettings targetSize if provided in image filename
                var parsedTargetSize = ParseSizeFromFilename(Path.GetFileNameWithoutExtension(originalFilepath));
                if(parsedTargetSize != null)
                {
                    targetWidth = parsedTargetSize.Value.Width;
                    targetHeight = parsedTargetSize.Value.Height;
                }

                //READ FILE INTO A STREAM THAT NEEDS TO BE MANUALLY DISPOSED
                var imageStream = fileManager.ReadFile(originalFilepath);
                var finalSavingPath = string.Empty;

                //Skip if image can not be read 
                if(imageStream == null) 
                {
                    _logger.LogError("Could not read file: {originalFilepath}", originalFilepath);
                    continue;
                }

                //Image resizing part
                var wasResizedFlag = false;
                var needsDownsizing = originalSize.Width > targetWidth || originalSize.Height > targetHeight;
                if(needsDownsizing && resizingEnabled)
                {
                    var targetSize = new Size(targetWidth, targetHeight);
                    var newSize = imageProcessor.CalculateResolution(originalSize, targetSize, !ignoreAspectRatio);

                    using var convertedImageStream = imageProcessor.Resize(imageStream, newSize);

                    if(convertedImageStream == null){
                        _logger.LogError("Could not convert image {originalFilepath}",originalFilepath);
                        imageStream.Dispose();
                        continue;
                    }

                    //Calculate file size difference
                    var bytesSaved = fileManager.CompareFileSize(originalFilepath, alternativeFilepath);
                    wpaSettings.WpaBytesSavedResizing += bytesSaved;
                    wpaSettings.WpaResizerCounter++;

                    //Adjust media properties
                    var newFilename = Path.GetFileName(alternativeFilepath);
                    mediaHelper.SetUmbFilename(media, newFilename);
                    mediaHelper.SetUmbResolution(media, newSize);

                    //Save new file size
                    mediaHelper.SetUmbBytes(media,convertedImageStream.Length);
                    wasResizedFlag = true; 

                    //Reassign imageStream
                    imageStream.Position = 0;
                    imageStream.SetLength(0);
                    convertedImageStream.CopyTo(imageStream);
                    imageStream.Position = 0;
                    
                    //Reassign where to save the image 
                    finalSavingPath = alternativeFilepath;

                }

                //Image converting part
                var wasConvertedFlag = false;
                if(convertingEnabled && !originalFilepath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    var sourceFilePath = string.Empty;
                    var pathWithOldExtension = string.Empty;

                    //Assign sourcePath depending on if image was resized previously
                    sourceFilePath = wasResizedFlag ? alternativeFilepath : originalFilepath;

                    pathWithOldExtension = alternativeFilepath;
                    alternativeFilepath = Path.ChangeExtension(alternativeFilepath, ".webp");

                    var convertType = ConvertMode.lossy;
                    if (convertMode == "lossless") convertType = ConvertMode.lossless;
                    
                    using var convertedImageStream = imageProcessor.ConvertToWebp(imageStream, convertType, convertQuality);

                    if(convertedImageStream != null)
                    {
                        fileManager.DeleteFile(pathWithOldExtension);

                        //Adjust medias src property
                        if(!wasResizedFlag)
                        {
                            var newFilename = Path.GetFileNameWithoutExtension(alternativeFilepath);
                            mediaHelper.SetUmbFilename(media, newFilename);
                        }

                        mediaHelper.SetUmbExtension(media, ".webp");
                        mediaHelper.SetUmbBytes(media, convertedImageStream.Length);


                        //Reassign where to save the image and the image itself
                        finalSavingPath = alternativeFilepath;
                        imageStream.Position = 0;
                        imageStream.SetLength(0);
                        convertedImageStream.CopyTo(imageStream);
                        imageStream.Position = 0;

                        wasConvertedFlag = true;
                    }
                    
                }

                //Finally writing modified image back to file
                if(finalSavingPath != string.Empty)
                {
                    fileManager.WriteFile(finalSavingPath, imageStream);
                    imageStream.Dispose();
                }


                //Deleting original files
                if (!keepOriginals && wasResizedFlag || wasConvertedFlag)
                {
                    fileManager.DeleteFile(originalFilepath);
                }
            }

            //Write settings to file to preserve saved bytes values   
            _settingsProvider.PersistToFile(wpaSettings);
        }

        private Size? ParseSizeFromFilename(string fileName)
        {
            if (!fileName.StartsWith("wparesize_")) return null;
            if (fileName.Length < 11) return null;

            try
            {
                var size = new Size(int.MaxValue, int.MaxValue);

                var buffer = string.Empty;
                for (var i = 10; i < fileName.Length; i++)
                {
                    if (fileName[i] == '_')
                    {
                        if (size.Width == int.MaxValue)
                        {
                            size.Width = int.Parse(buffer);
                            buffer = string.Empty;
                        }
                        else
                        {
                            size.Height = int.Parse(buffer);
                            return size;
                        }
                    }
                    else
                    {
                        buffer += fileName[i];
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError("WebPicAuto: error: {0}", e.Message);
                return null;
            }
        }
    }
}

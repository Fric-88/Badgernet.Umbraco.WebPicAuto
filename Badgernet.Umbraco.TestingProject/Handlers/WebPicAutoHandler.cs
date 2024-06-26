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
                                   IWebPicHelper webPicHelper,
                                   ICoreScopeProvider scopeProvider,
                                   ILogger<WebPicAutoHandler> logger) : INotificationHandler<MediaSavingNotification>
    {
        private readonly IWebPicSettingProvider _settingsProvider = wpaSettingsProvider;
        private readonly IWebPicHelper _mediaHelper = webPicHelper;
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
                
                
                string originalFilepath = _mediaHelper.GetFullPath(media);
                string alternativeFilepath = _mediaHelper.GenerateAlternativePath(media);
                Size originalSize = new();

                //Skip if paths not good
                if (string.IsNullOrEmpty(originalFilepath) || string.IsNullOrEmpty(alternativeFilepath)) continue;

                //Skip any not-new images
                if (media.Id > 0) continue;

                //Skip if image name contains "ignoreKeyword"
                if (Path.GetFileNameWithoutExtension(originalFilepath).Contains(ignoreKeyword,StringComparison.CurrentCultureIgnoreCase)) 
                {
                    alternativeFilepath = originalFilepath.Replace(ignoreKeyword, string.Empty);
                    File.Move(originalFilepath, alternativeFilepath, true);

                    var jsonString = media.GetValue<string>("umbracoFile");

                    if (jsonString == null) continue;

                    var propNode = JsonNode.Parse((string)jsonString);
                    string? path = propNode!["src"]!.GetValue<string>();
                    path = path.Replace(ignoreKeyword, string.Empty);

                    propNode["src"] = path;

                    media.SetValue("umbracoFile", propNode.ToJsonString());
                    if(media.Name != null)
                    {
                        media.Name = media.Name.Replace(ignoreKeyword, string.Empty,StringComparison.CurrentCultureIgnoreCase);
                    }

                    continue;
                }
                
                using var scope = _scopeProvider.CreateCoreScope(autoComplete: true);
                using var _ = scope.Notifications.Suppress();

                try
                {
                    originalSize.Width = int.Parse(media.GetValue<string>("umbracoWidth")!);
                    originalSize.Height = int.Parse(media.GetValue<string>("umbracoHeight")!);
                }
                catch
                {
                    continue; //Skip if dimensions cannot be parsed 
                }

                //Override appsettings targetSize if provided in image filename
                var parsedTargetSize = ParseSizeFromFilename(Path.GetFileNameWithoutExtension(originalFilepath));
                if(parsedTargetSize != null)
                {
                    targetWidth = parsedTargetSize.Value.Width;
                    targetHeight = parsedTargetSize.Value.Height;
                }

                //Image resizing part
                var wasResizedFlag = false;
                var needsResizing = originalSize.Width > targetWidth || originalSize.Height > targetHeight;
                if(needsResizing && resizingEnabled)
                {
                    var newSize = _mediaHelper.ResizeImageFile(originalFilepath, alternativeFilepath, new Size(targetWidth, targetHeight), ignoreAspectRatio);

                    if(newSize != null)
                    {
                        //Calculate file size difference
                        var bytesSaved = _mediaHelper.FileSizeDiff(originalFilepath, alternativeFilepath);
                        wpaSettings.WpaBytesSavedResizing += bytesSaved;
                        wpaSettings.WpaResizerCounter++;

                        //Adjust media properties
                        var newFilename = Path.GetFileName(alternativeFilepath);
                        _mediaHelper.ChangeFilename(media, newFilename);
                        media.SetValue("umbracoWidth", newSize.Value.Width);
                        media.SetValue("umbracoHeight", newSize.Value.Height);

                        //Save new file size
                        FileInfo targetImg = new(alternativeFilepath);
                        media.SetValue("umbracoBytes", targetImg.Length);

                        wasResizedFlag = true;
                    }
                }

                //Image converting part
                var wasConvertedFlag = false;
                if(convertingEnabled && !originalFilepath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    var sourceFilePath = string.Empty;
                    var toDeletePath = string.Empty;

                    sourceFilePath = wasResizedFlag ? alternativeFilepath : originalFilepath;

                    toDeletePath = alternativeFilepath;
                    alternativeFilepath = Path.ChangeExtension(alternativeFilepath, ".webp");

                    if(_mediaHelper.ConvertImageFile(sourceFilePath, alternativeFilepath, convertMode, convertQuality))
                    {
                        //Calculate file size difference
                        var bytesSaved = _mediaHelper.FileSizeDiff(sourceFilePath, alternativeFilepath);
                        wpaSettings.WpaBytesSavedConverting += bytesSaved;
                        wpaSettings.WpaConverterCounter++;


                        TryDeleteFile(toDeletePath);

                        //Adjust medias src property
                        if(!wasResizedFlag)
                        {
                            var newFilename = Path.GetFileNameWithoutExtension(alternativeFilepath);

                            _mediaHelper.ChangeFilename(media, newFilename);
                            _mediaHelper.ChangeExtension(media, ".webp");
                        }
                        else
                        {
                            _mediaHelper.ChangeExtension(media, ".webp");
                        }

                        //Save new file size
                        FileInfo targetImg = new(alternativeFilepath);
                        media.SetValue("umbracoBytes", targetImg.Length);

                        wasConvertedFlag = true;
                    }
                }

                //Deleting original files
                if (!keepOriginals && wasResizedFlag || wasConvertedFlag)
                {
                    TryDeleteFile(originalFilepath);
                }
            }

            //Write settings to file to preserve saved bytes values   
            _settingsProvider.PersistToFile(wpaSettings);
        }

        private bool TryDeleteFile(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                return true;
            }
            catch(Exception e)
            {
                _logger.LogError("WebPicAuto: error while deleting file: {0}", e.Message);
                return false;
            }
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

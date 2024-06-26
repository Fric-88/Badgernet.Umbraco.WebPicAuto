using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using System.Runtime;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using File = System.IO.File;
using SixLabors.ImageSharp.Processing;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace Badgernet.WebPicAuto.Helpers
{
    public class WebPicHelper : IWebPicHelper
    {
        private readonly MediaUrlGeneratorCollection _mediaUrlGeneratorCollection;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IMediaService _mediaService;
        private readonly ILogger<WebPicHelper> _logger;


        public WebPicHelper (IWebHostEnvironment hostEnvironment, MediaUrlGeneratorCollection mediaUrlGeneratorCollection, IMediaService mediaService, ILogger<WebPicHelper> logger)
        {
            _hostEnvironment = hostEnvironment;
            _mediaUrlGeneratorCollection = mediaUrlGeneratorCollection;
            _mediaService = mediaService;
            _logger = logger;

        }

        public IMedia? GetMediaById(int id)
        {
            return _mediaService.GetById(id);
        }

        //Returns path of the Image file 
        public string GetFullPath(IMedia media)
        {
            var mediaPath = media.GetUrl("umbracoFile", _mediaUrlGeneratorCollection);
            var webRootPath = _hostEnvironment.WebRootPath.Replace("\\", "/");
            if (string.IsNullOrEmpty(mediaPath) || string.IsNullOrEmpty(webRootPath)) return string.Empty;

            return webRootPath + mediaPath;
        }

        public void ChangeExtension(IMedia media, string extension)
        {
            var umbracoFileJson = media.GetValue("umbracoFile");
            if (umbracoFileJson != null)
            {
                var umbracoFile = JsonNode.Parse((string)umbracoFileJson);
                var srcProp = umbracoFile!["src"]!.GetValue<string>();
                umbracoFile["src"] = Path.ChangeExtension(srcProp, extension);
                media.SetValue("umbracoFile", umbracoFile.ToJsonString());
                media.SetValue("umbracoExtension", extension);
            }
        }

        public void ChangeFilename(IMedia media, string filename)
        {
            var umbracoFileJson = media.GetValue("umbracoFile");
            if (umbracoFileJson != null)
            {
                var umbracoFile = JsonNode.Parse((string)umbracoFileJson);
                var srcProp = umbracoFile!["src"]!.GetValue<string>();
                var directory = Path.GetDirectoryName(srcProp)!;
                var path = Path.Combine(directory, filename);
                path = path.Replace('\\', '/');
                umbracoFile["src"] = path;

                media.SetValue("umbracoFile", umbracoFile.ToJsonString());
            }
        }

        public object? GetValue(IMedia media, string propName)
        {
            return media.GetValue<object>(propName);
        }

        public void SetValue(IMedia media, string propName, object value)
        {
            media.SetValue(propName, value);
        }

        public bool ConvertImageFile(string sourcePath, string targetPath, string convertMode, int convertQuality)
        {
            var encoder = convertMode switch
            {
                "lossy" => new WebpEncoder()
                {
                    Quality = convertQuality,
                    FileFormat = WebpFileFormatType.Lossy
                },
                "lossless" => new WebpEncoder()
                {
                    Quality = convertQuality,
                    FileFormat = WebpFileFormatType.Lossless
                },
                _ => new WebpEncoder()
                {
                    Quality = convertQuality,
                    FileFormat = WebpFileFormatType.Lossy
                }
            };

            try
            {
                var img = Image.Load(sourcePath);
                img.Save(targetPath, encoder!);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("WebPicAuto: error converting image: {0}", e.Message);
                return false;
            }
        }

        public Size? ResizeImageFile(string sourcePath, string targetPath, Size targetSize, bool ignoreAspectRatio)
        {
            try
            {
                using var img = Image.Load(sourcePath);
                var newSize = CalculateNewSize(img.Size, targetSize, ignoreAspectRatio);

                img.Mutate(x =>
                {
                    x.Resize(newSize.Width, newSize.Height);
                });
                img.Mutate(x => x.AutoOrient());
                img.Save(targetPath);

                return img.Size;
            }
            catch (Exception e)
            {
                _logger.LogError("WebPicAuto: error resizing image: {0}", e.Message);
                return null;
            }
        }

        public long FileSizeDiff(string sourcePath, string targetPath)
        {
            //Calculate file size diff and save it
            if (File.Exists(sourcePath) && File.Exists(targetPath))
            {
                FileInfo originalImg = new(sourcePath);
                FileInfo convertedImg = new(targetPath);

                return originalImg.Length - convertedImg.Length;
            }

            return 0;
        }

        public Size CalculateNewSize(Size originalSize, Size sizeLimit, bool ignoreAspectRatio = false)
        {
            var newWidth = originalSize.Width;
            var newHeight = originalSize.Height;

            //Upscaling not intended -> return original size
            if (originalSize.Width <= sizeLimit.Width && originalSize.Height <= sizeLimit.Height)
                return new Size(newWidth, newHeight);

            if (ignoreAspectRatio)
            {
                return sizeLimit;
            }

            var aspectRatio = (double)originalSize.Width / (double)originalSize.Height;

            if (aspectRatio > 1)
            {
                newWidth = sizeLimit.Width;
                newHeight = (int)(newWidth / aspectRatio);
                if (newHeight > sizeLimit.Height)
                {
                    newHeight = sizeLimit.Height;
                    newWidth = (int)(newHeight * aspectRatio);
                }
            }
            else
            {
                newHeight = sizeLimit.Height;
                newWidth = (int)(newHeight * aspectRatio);

                if (newWidth > sizeLimit.Width)
                {
                    newHeight = (int)(newWidth / aspectRatio);
                    newWidth = sizeLimit.Width;
                }
            }
            return new Size(newWidth, newHeight);
        }

        //Generates available path in the same directory
        public string GenerateAlternativePath(IMedia media)
        {
            string originalPath = GetFullPath(media);
            
            string newPath;

            FileInfo fileInfo = new(originalPath);

            do
            {
                var newFilename = NewUniqueName();
                newPath = $"{fileInfo.DirectoryName}\\{newFilename}{fileInfo.Extension}";
            }
            while (File.Exists(newPath) || File.Exists(Path.ChangeExtension(newPath,"webp")));

            //Return cleaned up path 
            return newPath.Replace("\\", "/");
        }

        public void SaveMedia(IMedia media)
        {
            _mediaService.Save(media);
        }

        private string NewUniqueName()
        {
            return Guid.NewGuid().ToString().Replace("-", "");
        }
    }
}

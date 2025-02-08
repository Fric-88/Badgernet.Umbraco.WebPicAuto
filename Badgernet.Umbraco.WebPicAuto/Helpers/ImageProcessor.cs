using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Badgernet.WebPicAuto.Helpers;

public class ImageProcessor(ILogger<ImageProcessor> logger) : IImageProcessor
{
        public MemoryStream? Resize(MemoryStream imageStream, Size targetResolution)
    {
        try
        {
            using var img = Image.Load(imageStream);
            var format = img.Metadata.DecodedImageFormat ?? throw new Exception("No image Format found");

            img.Mutate(x =>
            {
                x.Resize(targetResolution.Width, targetResolution.Height);
            });

            var resizedStream = new MemoryStream();
            img.Save(resizedStream, format);
            resizedStream.Position = 0;

            return resizedStream;
        }
        catch (Exception e)
        {
            logger.LogError("Error resizing image file: {Message}", e.Message);
            return null;
        }
    }
    public MemoryStream? ConvertToWebp(MemoryStream imageStream, ConvertMode convertMode, int convertQuality)
    {
        var encoder = new WebpEncoder
            {
                Quality = convertQuality,
                FileFormat = convertMode == ConvertMode.lossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
            };

        try
        {
            using var img = Image.Load(imageStream);
            var converted = new MemoryStream();
            
            img.Save(converted, encoder);
            converted.Position = 0;

            return converted;
        }
        catch (Exception e)
        {
            logger.LogError("Error converting image file: {Message}", e.Message);
            return null;
        }

    }



    public Size CalculateResolution(Size originalResolution, Size targetResolution, bool preserveAspectRatio = true)
        {
            var newWidth = originalResolution.Width;
            var newHeight = originalResolution.Height;

            if (!preserveAspectRatio)
            {
                return targetResolution;
            }

            var aspectRatio = (double)originalResolution.Width / (double)originalResolution.Height;

            if (aspectRatio > 1)
            {
                newWidth = targetResolution.Width;
                newHeight = (int)(newWidth / aspectRatio);
                
                if (newHeight > targetResolution.Height)
                {
                    newHeight = targetResolution.Height;
                    newWidth = (int)(newHeight * aspectRatio);
                }
            }
            else
            {
                newHeight = targetResolution.Height;
                newWidth = (int)(newHeight * aspectRatio);

                if (newWidth > targetResolution.Width)
                {
                    newHeight = (int)(newWidth / aspectRatio);
                    newWidth = targetResolution.Width;
                }
            }
            return new Size(newWidth, newHeight);
        }


}
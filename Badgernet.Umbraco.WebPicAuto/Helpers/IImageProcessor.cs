using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Badgernet.WebPicAuto.Helpers;

public interface IImageProcessor
{
    MemoryStream? Resize(MemoryStream imageStream, Size targetTResolution);
    MemoryStream? ConvertToWebp(MemoryStream imageStream, ConvertMode convertMode, int convertQuality);
    Size CalculateResolution(Size originalResolution, Size targetResolution, bool preserveAspectRatio = true);
}

public enum ConvertMode
{
    lossy,
    lossless
} 
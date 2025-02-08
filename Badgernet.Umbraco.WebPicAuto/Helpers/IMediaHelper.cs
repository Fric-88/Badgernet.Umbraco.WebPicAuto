using SixLabors.ImageSharp;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Badgernet.WebPicAuto.Helpers;

public interface IMediaHelper
{
    IEnumerable<IPublishedContent> GetAllMedia();
    IMedia? GetMediaById(int id);
    IEnumerable<IMedia> GetMediaByIds(int[] ids);
    IEnumerable<IPublishedContent> GetMediaByType(string type);
    void SaveMedia(IMedia media);
    string GetRelativePath(IMedia media);
    Size GetUmbResolution(IMedia media);
    void SetUmbResolution(IMedia media, Size size);
    string GetUmbExtension(IMedia media);
    void SetUmbExtension(IMedia media, string extension);
    long GetUmbBytes(IMedia media);
    void SetUmbBytes(IMedia media, long value);
    void SetUmbFilename(IMedia media, string filename);
}
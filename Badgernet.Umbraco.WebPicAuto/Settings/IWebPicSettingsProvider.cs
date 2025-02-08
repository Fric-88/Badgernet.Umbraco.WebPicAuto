using Badgernet.WebPicAuto.Settings;

namespace Badgernet.WebPicAuto.Settings
{
    public interface IWebPicSettingProvider
    {
        WebPicSettings GetFromFile();
        bool PersistToFile(WebPicSettings settings);
    }
}

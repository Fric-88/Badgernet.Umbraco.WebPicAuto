using Badgernet.WebPicAuto.Handlers;
using Badgernet.WebPicAuto.Helpers;
using Badgernet.WebPicAuto.Settings;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace Badgernet.WebPicAuto.Composers
{
    internal class WebPicAutoComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {

            builder.AddNotificationHandler<MediaSavingNotification, WebPicAutoHandler>();
            builder.Services.AddSingleton<IWebPicSettingProvider>(x =>
            {
                var dir = System.Environment.CurrentDirectory;
                var settingsPath = dir + "/App_Plugins/Badgernet.Umbraco.WebPicAuto/Backoffice/WpaSettings.json";
                return new WebPicSettingsProvider(settingsPath);
            });
            
            builder.Services.AddSingleton<IMediaHelper, MediaHelper>();
            builder.Services.AddSingleton<IFileManager, FileManager>();
            builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
        }



    }
}

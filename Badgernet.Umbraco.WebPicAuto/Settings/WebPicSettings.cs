
namespace Badgernet.WebPicAuto.Settings
{
    public class WebPicSettings
    {
        public bool WpaEnableResizing { get; set; } = false;
        public bool WpaEnableConverting { get; set; } = true;
        public string WpaConvertMode { get; set; } = "lossy";
        public int WpaConvertQuality { get; set; } = 80;
        public bool WpaIgnoreAspectRatio { get; set; } = false;
        public int WpaTargetWidth { get; set; } = 1920;
        public int WpaTargetHeight { get; set; } = 1080;
        public bool WpaKeepOriginals { get; set; } = false;
        public string WpaIgnoreKeyword {get;set;} = "wpaignore";
        public int WpaResizerCounter { get; set; } = 0;
        public int WpaConverterCounter { get; set; } = 0;
        public long WpaBytesSavedResizing { get; set; } = 0;
        public long WpaBytesSavedConverting{ get; set; } = 0;

    }
}

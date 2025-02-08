
using System.Text;
using System.Text.Json;


namespace Badgernet.WebPicAuto.Settings;


public class WebPicSettingsProvider : IWebPicSettingProvider
{
    private readonly string _settingsPath;
    public WebPicSettingsProvider(string filePath)
    {
        _settingsPath = filePath;
    } 
    
    public WebPicSettings GetFromFile()
    {
        var jsonString = string.Empty;
        var fileLock = new object();

        lock (fileLock)
        {
            try
            {
                using var fStream = File.Open(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var streamReader = new StreamReader(fStream,Encoding.UTF8);
                jsonString = streamReader.ReadToEnd();
            }
            catch
            {
                //LOG
            }
        }
        
        WebPicSettings settings;

        try
        {
            settings = JsonSerializer.Deserialize<WebPicSettings>(jsonString);
        }
        catch
        {
            return new WebPicSettings(); //Return Defaults 
        }
        
        return settings;

    }

    public bool PersistToFile(WebPicSettings settings)
    {
        var fileLock = new object();
        var jsonString = JsonSerializer.Serialize(settings);

        lock (fileLock)
        {
            try
            {
                using var fStream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var streamWriter = new StreamWriter(fStream, Encoding.UTF8);
                streamWriter.Write(jsonString);
                return true;
            }
            catch 
            {
                //LOG 
                return false;
            }
        }
    }
}
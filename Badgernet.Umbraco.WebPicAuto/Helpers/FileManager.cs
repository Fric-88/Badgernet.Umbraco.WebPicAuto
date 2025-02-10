using Umbraco.Cms.Core.IO;

namespace Badgernet.WebPicAuto.Helpers;

public class FileManager(MediaFileManager mediaFileManager): IFileManager
{
    private readonly IFileSystem _fileSystem = mediaFileManager.FileSystem;
    public bool DeleteFile(string relativePath)
    {
        _fileSystem.DeleteFile(relativePath);
        return true;
    }

    public bool FileExists(string relativePath)
    {
        return _fileSystem.FileExists(relativePath);
    }

    public MemoryStream? ReadFile(string relativePath)
    {
        if(_fileSystem.FileExists(relativePath))
        {
            using var fileStream =  _fileSystem.OpenFile(relativePath);
            var memStream = new MemoryStream();
            fileStream.CopyTo(memStream);
            memStream.Position = 0;
            return memStream;

        }
        return null;
    }

    public bool WriteFile(string relativePath, Stream fileStream)
    {
        _fileSystem.AddFile(relativePath, fileStream, true);
        return true;
    }

    public string GetFreePath(string relativePath, string targetExtension = "" )
    {   
        //Extract extension from path if not provided
        if(targetExtension == "")
        {
            targetExtension = Path.GetExtension(relativePath);
        }

        ArgumentNullException.ThrowIfNull("Cannot determine file extension from {relativePath}", nameof(relativePath));

        var directory = Path.GetDirectoryName(relativePath) ?? string.Empty;    

        string newPath;

        do
        {
            var newFilename =  Guid.NewGuid().ToString().Replace("-", "");//Generate a name from a guid
            newPath = Path.Combine(directory, Path.ChangeExtension(newFilename, targetExtension));
        }
        while (FileExists(newPath));

        //Return cleaned up path 
        return newPath.Replace("\\", "/");
    }


    public long CompareFileSize(string referenceRelativePath, string relativePath)
    {
        if(FileExists(referenceRelativePath) && FileExists(relativePath))
        {
            var referenceFileSize = _fileSystem.GetSize(referenceRelativePath);
            var fileSize = _fileSystem.GetSize(relativePath);
            return referenceFileSize - fileSize;
        }

        return 0;
    }
    
}
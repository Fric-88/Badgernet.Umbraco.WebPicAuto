namespace Badgernet.WebPicAuto.Helpers;

public static class ExtensionMethods
{
    public static void ClearAndReassignStream(this MemoryStream stream, MemoryStream sourceStream)
    {
        stream.Position = 0;
        stream.SetLength(0);
        sourceStream.CopyTo(stream);
        stream.Position = 0;
    }
    
}
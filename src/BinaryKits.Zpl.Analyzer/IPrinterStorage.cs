namespace BinaryKits.Zpl.Analyzer
{
    public interface IPrinterStorage
    {
        void AddFile(char storageDevice, string fileName, byte[] data);
        byte[] GetFile(char storageDevice, string fileName);
    }
}

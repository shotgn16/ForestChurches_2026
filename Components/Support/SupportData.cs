using ICSharpCode.SharpZipLib.Zip;

namespace ForestChurches.Components.Support
{
    public class SupportData
    {
        private static ZipEntry _ZipEntry;
        private static string _path;

        public static void CreateSupportFile()
        {
            createSupportDirectory();
            _path = $"support/SupportData-{DateTime.UtcNow:yyyyMMd}.zip";

            string[] filenames = Directory.GetFiles("logs");
            using (ZipOutputStream output = new ZipOutputStream(File.Create(_path)))
            {
                output.SetLevel(9);
                byte[] buffer = new byte[8124];
                foreach (string file in filenames)
                {
                    _ZipEntry = new ZipEntry(Path.GetFileName(file));
                    _ZipEntry.DateTime = DateTime.UtcNow;
                    output.PutNextEntry(_ZipEntry);

                    using (FileStream fileStream = File.OpenRead(file))
                    {
                        int sourceBytes;
                        do {
                            sourceBytes = fileStream.Read(buffer, 0, buffer.Length);
                            output.Write(buffer, 0, sourceBytes);
                        } while (sourceBytes > 0);
                    }
                }

                output.Finish();
                output.Close();
            }
        }

        public static void LogError(Exception ex)
        {
            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string path = Path.Combine(directoryPath, $"Error-{DateTime.UtcNow:yyyyMMdd}.log");
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLine($"[{DateTime.UtcNow}] {ex.Message}");
                writer.WriteLine($"[{DateTime.UtcNow}] {ex.StackTrace}");
                writer.WriteLine($"[{DateTime.UtcNow}] {ex.InnerException}");
                writer.WriteLine($"[{DateTime.UtcNow}] {ex.Source}");
                writer.WriteLine($"[{DateTime.UtcNow}] {ex.TargetSite}");
            }
        }

        private static void createSupportDirectory()
        {
            var path = Directory.GetCurrentDirectory();

            if (!Directory.Exists(path + "/support")) {
                Directory.CreateDirectory(path + "/support");
            }
        }
    }
}

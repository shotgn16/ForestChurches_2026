using ForestChurches.Components.Storage;
using ForestChurches.Data;
using ForestChurches.Components.Users;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ForestChurches.Tools
{
    public static class ImageMigrationUtility
    {
        /// <summary>
        /// Migrate images stored in byte[] fields (ImageArray) to blob storage and save the blob name in ImageBlobName.
        /// This method will null out ImageArray after successful upload.
        /// </summary>
        public static async Task<(int eventsMigrated, int usersMigrated)> MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ForestChurchesContext>();
            var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

            int eventsMigrated = 0;
            int usersMigrated = 0;

            // Migrate Events
            logger.LogInformation("Starting migration of event images...");
            var events = db.Events.Where(e => e.ImageArray != null && (e.ImageBlobName == null || e.ImageBlobName == "")).ToList();
            foreach (var ev in events)
            {
                try
                {
                    var data = ev.ImageArray;
                    if (data == null || data.Length == 0) continue;
                    var ext = GetImageExtension(data);
                    var contentType = GetContentTypeFromExt(ext);
                    var blobName = $"images/{Guid.NewGuid()}{ext}";
                    await blob.UploadAsync(data, blobName, contentType, null, ct);
                    ev.ImageBlobName = blobName;
                    ev.ImageArray = null; // free DB
                    eventsMigrated++;

                    if (eventsMigrated % 50 == 0)
                    {
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to migrate event image id={EventId}", ev.ID);
                }
            }
            await db.SaveChangesAsync(ct);

            // Migrate Users (ChurchAccount)
            logger.LogInformation("Starting migration of user images...");
            var users = db.ChurchAccount.Where(u => u.ImageArray != null && (u.ImageBlobName == null || u.ImageBlobName == "")).ToList();
            foreach (var user in users)
            {
                try
                {
                    var data = user.ImageArray;
                    if (data == null || data.Length == 0) continue;
                    var ext = GetImageExtension(data);
                    var contentType = GetContentTypeFromExt(ext);
                    var blobName = $"images/{Guid.NewGuid()}{ext}";
                    await blob.UploadAsync(data, blobName, contentType, null, ct);
                    user.ImageBlobName = blobName;
                    user.ImageArray = null;
                    usersMigrated++;

                    if (usersMigrated % 50 == 0)
                    {
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to migrate user image id={UserId}", user.Id);
                }
            }
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Migration complete. Events migrated: {Events}, Users migrated: {Users}", eventsMigrated, usersMigrated);
            return (eventsMigrated, usersMigrated);
        }

        private static string GetImageExtension(byte[] data)
        {
            if (data == null || data.Length < 4) return ".bin";
            // PNG
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            // JPG/JPEG
            if (data[0] == 0xFF && data[1] == 0xD8) return ".jpg";
            // GIF
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return ".gif";
            // WebP - "RIFF" + "WEBP"
            if (data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return ".webp";
            return ".bin";
        }

        private static string GetContentTypeFromExt(string ext)
        {
            return ext.ToLower() switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream",
            };
        }
    }
}

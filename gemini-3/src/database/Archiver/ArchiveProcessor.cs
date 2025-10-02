namespace Archiver
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Reflection;
    using DataAccess;         // for GeminiDbContext
    using DataAccess.Data;    // for ArchiveHelper
    using DataAccess.Models;  // ← bring in the EF Archive model

    public static class ArchiveProcessor
    {
        /// <summary>
        /// Export binary files and merge JSON via ArchiveHelper.
        /// </summary>
        public static void StageArchive(string inputFolder, string stagingFolder, GeminiDbContext db)
        {
            if (Directory.Exists(stagingFolder))
                Directory.Delete(stagingFolder, recursive: true);
            Directory.CreateDirectory(stagingFolder);

            // copy binaries
            foreach (var file in Directory.GetFiles(inputFolder))
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, Path.Combine(stagingFolder, Path.GetFileName(file)), overwrite: true);
            }

            // load manifest
            var manifestPath = Path.Combine(inputFolder, "mar.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Missing manifest 'mar.json'", manifestPath);
            File.Copy(manifestPath, Path.Combine(stagingFolder, "mar.json"), overwrite: true);

            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ManifestRoot>(manifestJson)
                           ?? throw new InvalidOperationException("Cannot parse mar.json");

            // fetch the DB archive (this now returns DataAccess.Models.Archive)
            var valveId = int.Parse(Path.GetFileName(inputFolder).Split('_')[0]);
            var archive = db.GetArchiveByValve(valveId)
                          ?? throw new InvalidOperationException($"Archive not found for valve {valveId}");

            // stage each JSON using the manifest
            foreach (var kv in manifest.TextContent)
            {
                var key       = kv.Value.KeyName;       // e.g. "votes"
                var rawName   = kv.Value.FileName;      // e.g. "ieefszy0.co4.json"
                var destName  = $"{key}.json";          // e.g. "votes.json"
                var destPath  = Path.Combine(stagingFolder, destName);

                // when you reflect on it, you’re now using the DataAccess.Models.Archive
                var prop = archive.GetType()
                                  .GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                string? dbJson = prop?.GetValue(archive) as string;

                if (!string.IsNullOrEmpty(dbJson) && dbJson != "{}")
                {
                    Console.WriteLine($"[StageArchive] DB ⇒ {destName}");
                    File.WriteAllText(destPath, dbJson);
                }
                else
                {
                    Console.WriteLine($"[StageArchive] Disk ⇒ {rawName} → {destName}");
                    var srcPath = Path.Combine(inputFolder, rawName);
                    if (File.Exists(srcPath))
                        File.Copy(srcPath, destPath, overwrite: true);
                    else
                        File.WriteAllText(destPath, "{}");
                }
            }
        }
    }
}

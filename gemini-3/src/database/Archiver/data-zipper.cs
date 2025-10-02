using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using DataAccess;
using DataAccess.Models;

namespace Archiver
{
    public static class EncryptFiles
    {
        public static void Encrypt(int valveId, string stagingFolder, string outputRoot, string password)
        {
            // 1) Create a temp folder (copy from staging so we don't modify it)
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);
            foreach (var file in Directory.GetFiles(stagingFolder))
            {
                var dest = Path.Combine(tempRoot, Path.GetFileName(file));
                // AES-encrypt each file exactly as before
                var content = File.ReadAllText(file);
                FileWithEncryption.WriteAllText(dest, content);
            }

            // 2) Build and encrypt MasterRecord (mar.json)
            var master = new MasterRecord {
                ArchiveVersion         = 1,
                OriginDatabaseRevision = 1,
                OriginSoftwareVersion  = 1,
                TimeStamp              = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Key                    = Guid.NewGuid().ToString()
            };

            // Rename each encrypted file to human-key + “.json”, but leave numeric names untouched
            foreach (var f in Directory.GetFiles(tempRoot))
            {
                var fn = Path.GetFileName(f);
                if (string.Equals(fn, "mar.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var nameOnly = Path.GetFileNameWithoutExtension(fn);
                // 1) Numeric‐only filenames: keep as-is, no extension
                if (nameOnly.All(char.IsDigit))
                {
                    master.TextContent[nameOnly] = new FileRecord {
                        KeyName     = nameOnly,
                        FileName    = nameOnly,
                        EntityCount = 1
                    };
                    continue;
                }

                // 2) Non-numeric: treat as JSON blob, force “.json” extension
                var key        = nameOnly;
                var targetName = Path.ChangeExtension(fn, ".json");
                var targetPath = Path.Combine(tempRoot, targetName);
                if (!fn.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    File.Move(f, targetPath, overwrite: true);
                master.TextContent[key] = new FileRecord {
                    KeyName     = key,
                    FileName    = targetName,
                    EntityCount = 1
                };
            }

            // write the manifest (overwrites any original mar.json copy)
            var marJson = JsonConvert.SerializeObject(master, Formatting.Indented);
            FileWithEncryption.WriteAllText(Path.Combine(tempRoot, "mar.json"), marJson);

            // 3) Determine unique output path
            Directory.CreateDirectory(outputRoot);
            var baseName = $"archive-{valveId}.vitda";
            var outFile  = Path.Combine(outputRoot, baseName);
            int i = 1;
            while (File.Exists(outFile))
                outFile = Path.Combine(outputRoot, $"archive-{valveId}({i++}).vitda");

            // 4) ZipCrypto-protect the entire tempRoot
            var zip = new FastZip(new FastZipEvents()) { Password = password };
            zip.CreateZip(outFile, tempRoot, recurse: true, fileFilter: null);

            // 5) Cleanup
            Directory.Delete(tempRoot, recursive: true);

            Console.WriteLine($"[ZIP LOG] Wrote {outFile}");
        }
    }
}
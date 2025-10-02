using System;
using System.IO;
using System.Linq;

namespace Archiver
{
    public static class ExportBinary
    {
        public static void Export(string inputFolder, string stagingFolder)
        {
            // Validate input directory
            if (!Directory.Exists(inputFolder))
                throw new DirectoryNotFoundException($"Input folder not found: {inputFolder}");

            // Create staging directory
            Directory.CreateDirectory(stagingFolder);
            Console.WriteLine($"[EXPORT] Staging folder: {stagingFolder}");

            // Find binary files (non-JSON)
            var binaryFiles = Directory.GetFiles(inputFolder)
                .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!binaryFiles.Any())
            {
                Console.WriteLine("[EXPORT] No binary files found to export.");
                return;
            }

            // Copy each file
            foreach (var filePath in binaryFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var destPath = Path.Combine(stagingFolder, fileName);
                File.Copy(filePath, destPath, overwrite: true);
                Console.WriteLine($"[EXPORT] Copied {fileName} to staging.");
            }

            Console.WriteLine("[EXPORT] Binary export completed.");
        }
    }
}
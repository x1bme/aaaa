using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

namespace Archiver
{
    public class DecryptFiles
    {
        // Now accepts a target output directory for the flattened files
        public static void Decrypt(string[] args, string baseOutputDir)
        {
            // derive project root from bin folder (go up 3 levels)
            var baseDir     = AppContext.BaseDirectory;
            var projectDir  = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            Console.WriteLine($"[LOG] Resolved projectDir = {projectDir}");
            string inputFolder  = Path.Combine(projectDir, "Archived-data");
            string outputFolder = Path.Combine(projectDir, "disassembledFiles");
            // ensure folders exist
            Directory.CreateDirectory(inputFolder);
            Directory.CreateDirectory(outputFolder);
            string password     = "LVFZNC3N5SKW";

            try
            {
                // use args if they specify existing file paths, otherwise scan default folder
                string[] archiveFiles;
                if (args != null && args.Length > 0 && args.All(p => File.Exists(p)))
                {
                    archiveFiles = args;
                }
                else
                {
                    archiveFiles = Directory.GetFiles(inputFolder, "*.vitda");
                    if (archiveFiles.Length == 0)
                    {
                        Console.WriteLine($"No .vitda files found in '{inputFolder}'");
                        return;
                    }
                    Console.WriteLine($"Found {archiveFiles.Length} archive(s) in '{inputFolder}':");
                    foreach (var f in archiveFiles)
                        Console.WriteLine($"  - {Path.GetFileName(f)}");
                }

                foreach (string filePath in archiveFiles)
                {
                    var archiveName = Path.GetFileNameWithoutExtension(filePath);
                    var tempOutputFolder = Path.Combine(Path.GetTempPath(), archiveName);

                    Directory.CreateDirectory(tempOutputFolder);

                    // Extract the encrypted zip file
                    var zip = new FastZip(new FastZipEvents()) { Password = password };
                    zip.ExtractZip(filePath, tempOutputFolder, string.Empty);

                    // Decrypt and write files out into the provided baseOutputDir
                    var archiveOutput = Path.Combine(baseOutputDir, archiveName);
                    Directory.CreateDirectory(archiveOutput);
                    foreach (var tempFilePath in Directory.GetFiles(tempOutputFolder, "*.*", SearchOption.AllDirectories))
                    {
                        var fileNameOnly = Path.GetFileName(tempFilePath);
                        var outputFile   = Path.Combine(archiveOutput, fileNameOnly);
                        File.WriteAllText(outputFile, FileWithEncryption.ReadAllText(tempFilePath));
                    }

                    var allFiles = Directory.GetFiles(tempOutputFolder, "*.*", SearchOption.AllDirectories);
                    Console.WriteLine("Decrypt output files:\n  " + string.Join("\n  ", allFiles));

                    Directory.Delete(tempOutputFolder, true);
                    Console.WriteLine($"✅ Decrypted & unpacked '{archiveName}.vitda' to '{baseOutputDir}\\{archiveName}'");
                }

                Console.WriteLine("All done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during processing: {ex}");
            }
        }
    }
}
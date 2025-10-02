using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using Archiver;
using DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using DataAccess;
using MySqlConnector;

namespace ArchiveImporter
{
    internal class Program
    {
        // Debounce timers per folder
        private static readonly ConcurrentDictionary<string, Timer> _timers = new();
        private static int _debounceMs = 2000;
        private static string _inputRoot = string.Empty;
        private static string _outputRoot = string.Empty;

        static void Main(string[] args)
        {
            // Load config from env vars
            _inputRoot = Environment.GetEnvironmentVariable("INPUT_ROOT")
                ?? throw new InvalidOperationException("INPUT_ROOT not set");
            _outputRoot = Environment.GetEnvironmentVariable("OUTPUT_ROOT")
                ?? throw new InvalidOperationException("OUTPUT_ROOT not set");
            _debounceMs = int.TryParse(Environment.GetEnvironmentVariable("DEBOUNCE_MS"), out var d) ? d : 2000;

            // One-shot mode: process first directory alphabetically
            bool runOnce = args.Contains("--once");
            if (runOnce)
            {
                var firstDir = Directory.GetDirectories(_inputRoot)
                    .Where(p =>
                    {
                        var name = Path.GetFileName(p);
                        return !string.IsNullOrEmpty(name) && !name.StartsWith("_");
                    })
                    .OrderBy(p => Path.GetFileName(p))
                    .FirstOrDefault();
                if (firstDir != null)
                {
                    Console.WriteLine($"--once: Processing first folder {firstDir}");
                    try
                    {
                        ProcessArchive(firstDir);
                        Console.WriteLine("--once: Success");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"--once: Failure processing {firstDir}: {ex}");
                    }
                }
                else
                {
                    Console.WriteLine("--once: No folder found to process.");
                }

                return;
            }

            // Continue into watch mode
            Console.WriteLine($"Watching {_inputRoot} for incoming archives...");

            using var watcher = new FileSystemWatcher(_inputRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
            };
            
            watcher.Created += OnCreatedOrRenamed;
            watcher.Renamed += OnCreatedOrRenamed;
            watcher.Changed += OnCreatedOrRenamed;
            watcher.Error   += (s,e) => Console.WriteLine($"Watcher ERROR: {e.GetException()}");
            Console.WriteLine($"(dbg) Watching {_inputRoot}, IncludeSubdirs={watcher.IncludeSubdirectories}, NotifyFilter={watcher.NotifyFilter}");
            watcher.EnableRaisingEvents = true;

            Console.WriteLine("ArchiveImporter is now watching for new archives. Press Ctrl+C to exit.");
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
        }

        private static void OnCreatedOrRenamed(object sender, FileSystemEventArgs e)
        {
            var dirPath = Directory.Exists(e.FullPath)
                ? e.FullPath
                : Path.GetDirectoryName(e.FullPath);
            if (dirPath == null || !dirPath.StartsWith(_inputRoot)) return;
            // ignore hidden or processed folders
            var folderName = Path.GetFileName(dirPath);
            if (string.IsNullOrEmpty(folderName) || folderName.StartsWith("_")) return;
            Console.WriteLine($"(dbg) Event {e.ChangeType} on {e.FullPath}, targeting folder {dirPath}");
            // Debounce
            _timers.AddOrUpdate(dirPath,
                key => new Timer(_ => ProcessArchive(key), null, _debounceMs, Timeout.Infinite),
                (_, timer) => { timer.Change(_debounceMs, Timeout.Infinite); return timer; });
        }

        private static void ProcessArchive(string folderPath)
        {
            var staging = Path.Combine(folderPath, "_staging");

            // setup DbContext manually
            var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection not set");
            var optionsBuilder = new DbContextOptionsBuilder<GeminiDbContext>();
            optionsBuilder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            using var db = new GeminiDbContext(optionsBuilder.Options);

            ArchiveProcessor.StageArchive(folderPath, staging, db);

            Console.WriteLine("--once: Staged files for zipping:");
            foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
            {
                Console.WriteLine("  " + Path.GetRelativePath(staging, file));
            }

            int valveId = int.Parse(Path.GetFileName(folderPath).Split('_')[0]);
            string password = Environment.GetEnvironmentVariable("ARCHIVE_PASSWORD")
                              ?? throw new InvalidOperationException("ARCHIVE_PASSWORD not set");
            Console.WriteLine($"Packaging staged files into archive-{valveId}.vitda…");
            EncryptFiles.Encrypt(valveId, staging, _outputRoot, password);

            var archiveName = $"archive-{valveId}.vitda";
            var archivePath = Path.Combine(_outputRoot, archiveName);
            Console.WriteLine($"--once: Created archive ➞ {archivePath}");

        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
        }
    }
}

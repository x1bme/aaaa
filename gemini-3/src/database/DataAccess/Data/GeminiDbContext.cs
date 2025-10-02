using Microsoft.EntityFrameworkCore;
using DataAccess.Models;
using DataAccess.Data;
using System.Text;

namespace DataAccess
{
    public class GeminiDbContext : DbContext
    {
        public GeminiDbContext(DbContextOptions<GeminiDbContext> options)
            : base(options)
        {
        }

        // Define DbSets for your models
        public DbSet<Valve> Valves { get; set; }
        public DbSet<Test> Tests { get; set; }
        public DbSet<Dau> Daus { get; set; }
        public DbSet<Archive> Archives { get; set; }
        public DbSet<TestBlob> TestBlobs { get; set; }
        public DbSet<AcquisitionDataset> AcquisitionDatasets { get; set; } = null!;
        public DbSet<AcquisitionChannel> AcquisitionChannels { get; set; } = null!;
        public DbSet<DataChunkBlob> DataChunkBlobs { get; set; } = null!;

        /// <summary>
        /// Retrieves an Archive record by its ValveId, including its JSON payloads.
        /// </summary>
        public Archive? GetArchiveByValve(int valveId)
        {
            return Archives
                .AsNoTracking()
                .FirstOrDefault(a => a.ValveId == valveId);
        }

        /// <summary>
        /// Gets the latest test for a valve
        /// </summary>
        public Test? GetLatestTestForValve(int valveId)
        {
            return Tests
                .AsNoTracking()
                .Where(t => t.ValveId == valveId)
                .OrderByDescending(t => t.DataAcquisitionDate)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets a TestBlob by TestId
        /// </summary>
        public TestBlob? GetTestBlobByTestId(int testId)
        {
            return TestBlobs
                .AsNoTracking()
                .FirstOrDefault(tb => tb.TestId == testId);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // The rest of your configuration...
            ValveData.ConfigureValveEntities(builder);
            ArchiveHelper.Configure(builder);
            
            // Configure Test entity
            builder.Entity<Test>()
                .HasOne(t => t.Valve)
                .WithMany()
                .HasForeignKey(t => t.ValveId);
            
            // Configure TestBlob entity with one-to-one relationship
            builder.Entity<TestBlob>()
                .HasOne(tb => tb.Test)
                .WithOne()  // Changed from WithMany to WithOne
                .HasForeignKey<TestBlob>(tb => tb.TestId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Add seed data for Valve with ID 15
            builder.Entity<Valve>().HasData(
                new Valve
                {
                    Id = 15,
                    Name = "valve-15",
                    Location = "Seeded Test Location",
                    InstallationDate = DateTime.UtcNow,
                    IsActive = true,
                    AtvId = null,
                    RemoteId = null
                }
            );
            
            // Add a test record for the valve
            builder.Entity<Test>().HasData(
                new Test
                {
                    TestId = 1000,  // Use an ID unlikely to conflict
                    ValveId = 15,   // Reference the seeded valve
                    DataAcquisitionDate = DateTime.UtcNow
                }
            );
            
            // Add a TestBlob record linked to the test with empty blob data
            builder.Entity<TestBlob>().HasData(
                new
                {
                    TestBlobId = 2000,
                    TestId = 1000,  // Link to the seeded test
                    BlobData = new byte[0]  // Empty byte array instead of sample data
                }
            );
        }
    }
}
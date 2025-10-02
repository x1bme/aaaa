using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class AcquisitionDataset
    {
        [Key]
        public string Id { get; set; } = null!; // Use the dataset_id/acquisition_id from proto

        public string DeviceId { get; set; } = null!;
        
        public uint SampleRateHz { get; set; }
        
        public uint NumChannels { get; set; }
        
        public DateTime StartTimeUtc { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
        
        public long TotalChunksReceived { get; set; }
        
        public long TotalSamplesReceived { get; set; }
        
        // Navigation properties
        public virtual ICollection<AcquisitionChannel> Channels { get; set; } = new List<AcquisitionChannel>();
        
        public virtual ICollection<DataChunkBlob> DataChunks { get; set; } = new List<DataChunkBlob>();
    }
}
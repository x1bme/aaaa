using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class DataChunkBlob
    {
        [Key]
        public int Id { get; set; }
        
        public string AcquisitionDatasetId { get; set; } = null!;
        
        public uint SequenceNumber { get; set; }
        
        public uint? ChannelIndex { get; set; } // Null if this contains mixed channel data
        
        [Column(TypeName = "longblob")]
        public byte[] BinaryData { get; set; } = null!;
        
        public int SampleCount { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        [ForeignKey("AcquisitionDatasetId")]
        public virtual AcquisitionDataset Dataset { get; set; } = null!;
    }
}
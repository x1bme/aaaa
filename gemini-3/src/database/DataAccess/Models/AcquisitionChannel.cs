using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class AcquisitionChannel
    {
        [Key]
        public int Id { get; set; }
        
        public string AcquisitionDatasetId { get; set; } = null!;
        
        public uint ChannelIndex { get; set; }
        
        public string? SensorId { get; set; }
        
        public string? ChannelInput { get; set; }
        
        public uint SampleRateHz { get; set; }
        
        // Navigation property
        [ForeignKey("AcquisitionDatasetId")]
        public virtual AcquisitionDataset Dataset { get; set; } = null!;
    }
}
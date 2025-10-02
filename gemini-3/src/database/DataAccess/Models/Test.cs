using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class Test
    {
        [Key]
        public int TestId { get; set; }
        
        public DateTime DataAcquisitionDate { get; set; }
        
        public int ValveId { get; set; }  // Changed from Guid to int
        
        [ForeignKey("ValveId")]
        public virtual Valve Valve { get; set; }
    }
}
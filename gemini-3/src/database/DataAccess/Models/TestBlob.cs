using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class TestBlob
    {
        [Key]
        public int TestBlobId { get; set; }
        
        public int TestId { get; set; }
        
        [ForeignKey("TestId")]
        public virtual Test Test { get; set; }
        
        // Using MySQL's MEDIUMBLOB type which can store up to 16MB
        [Column(TypeName = "mediumblob")]
        public byte[] BlobData { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models
{
    public class Archive
    {
        [Key]
        public int ArchiveId { get; set; }

        public int ValveId { get; set; }

        [ForeignKey(nameof(ValveId))]
        public Valve Valve { get; set; } = null!;

        [Required, Column(TypeName = "json")]
        public string Providers { get; set; } = null!;

        [Required, Column("MOV", TypeName = "json")]
        public string Mov { get; set; } = null!;

        [Required, Column("MathItems", TypeName = "json")]
        public string MathItems { get; set; } = null!;

        [Required, Column(TypeName = "json")]
        public string Criteria { get; set; } = null!;

        [Required, Column(TypeName = "json")]
        public string Actuators { get; set; } = null!;

        [Required, Column(TypeName = "json")]
        public string ValveImages { get; set; } = null!;

        [Required, Column(TypeName = "json")]
        public string Sensors { get; set; } = null!;

        // Optional JSON payloads
        [Column(TypeName = "json")]
        public string? Votes { get; set; }
        [Column(TypeName = "json")]
        public string? Mvv { get; set; }
        [Column(TypeName = "json")]
        public string? Sp { get; set; }
        [Column("bfslT", TypeName = "json")]
        public string? BfslT { get; set; }
        [Column(TypeName = "json")]
        public string? Bfsl { get; set; }
        [Column(TypeName = "json")]
        public string? Markers { get; set; }
        [Column(TypeName = "json")]
        public string? Hierarchy { get; set; }
        [Column(TypeName = "json")]
        public string? Tests { get; set; }
        [Column(TypeName = "json")]
        public string? Channels { get; set; }
        [Column(TypeName = "json")]
        public string? Signals { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
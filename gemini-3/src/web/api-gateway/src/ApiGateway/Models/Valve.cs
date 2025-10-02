using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace ApiGateway.Models
{
    public class Valve
    {
        [Key]
        public int Id {get; set;}
        [Required]
        [MaxLength(100)]
        public string Name {get; set;}
        [MaxLength(200)]
        public string Location {get; set;}
        public DateTime InstallationDate {get; set;}
        public bool IsActive {get; set;}
        public Dau Atv {get; set;}
        public int? AtvId {get; set;}
        public Dau Remote {get; set;}
        public int? RemoteId {get; set;}

        public List<ValveConfiguration> Configurations {get; set;}
        public List <ValveLog> Logs {get; set;}
    }

    public class ValveConfiguration
    {
        [Key]
        public int Id {get; set;}
        public int ValveId {get; set;}
        [Required]
        [MaxLength(50)]
        public string ConfigurationType {get; set;}
        [Required]
        [MaxLength(255)]
        public string ConfigurationValue {get; set;}
        public DateTime CreatedAt {get; set;}
        public DateTime? UpdatedAt {get; set;}
    }

    public class ValveLog
    {
        [Key]
        public int Id {get; set;}
        public int ValveId {get; set;}
        public LogType LogType {get; set;}
        [Required]
        [MaxLength(500)]
        public string Message {get; set;}
        public DateTime TimeStamp {get; set;}
    }

    public enum LogType
    {
        Info,
        Warning,
        Error,
        Maintenance
    }
}
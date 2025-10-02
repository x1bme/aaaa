using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace ApiGateway.Models
{
    public class Dau
    {
        [Key]
        public int Id {get; set;}
        public int? ValveId {get; set;}
        [MaxLength(50)]
        public string SerialNumber {get; set;}
        [MaxLength(200)]
        public string Location {get; set;}

        [Required]
        [MaxLength(45)]
        public string DauIPAddress {get; set;}
        public bool HeartBeatAlerts {get; set;}
        public DateTime LastHeartbeat {get; set;}
        public DateTime LastCalibration {get; set;}
        public DateTime NextCalibration {get; set;}
        public DauStatus Status {get; set;}
        public int TcpPort {get; set;}
        public int UdpPort {get; set;}
    }

    public enum DauStatus
    {
        Operational,
        NeedsCalibration,
        NeedsMaintenance,
        Offline
    }
}
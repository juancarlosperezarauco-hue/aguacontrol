using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AquaControl.Domain;

[Table("RutasCampo")] public class FieldRoute:Entity {
 public int OperatorId {get;set;} public int CreatedById {get;set;}
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public bool Active {get;set;}=true;
 [MaxLength(20)] public string Kind {get;set;}="CORTE";
 [MaxLength(20)] public string TravelMode {get;set;}="VEHICULO";
 public double StartLatitude {get;set;} public double StartLongitude {get;set;}
 public double DistanceMeters {get;set;}
 [MaxLength(64)] public string NetworkHash {get;set;}="";
 public string PathJson {get;set;}="[]";
}
[Table("ParadasRuta")] public class RouteStop:Entity {
 public int RouteId {get;set;} public int OrderId {get;set;} public int AssignmentId {get;set;}
 public int Sequence {get;set;} public double DistanceMeters {get;set;}
 public double AccessMeters {get;set;}
}
[Table("VisitasCampo")] public class FieldVisit:Entity {
 public Guid RequestId {get;set;} public int OrderId {get;set;} public int UserId {get;set;} public int ShiftId {get;set;}
 public int? MeterId {get;set;} public int? EvidenceId {get;set;}
 [MaxLength(80)] public string ObservedSerial {get;set;}="";
 public decimal? Reading {get;set;}
 [MaxLength(20)] public string Outcome {get;set;}="REALIZADO";
 [MaxLength(2000)] public string Observation {get;set;}="";
 public double Latitude {get;set;} public double Longitude {get;set;} public double Accuracy {get;set;}
 public double DistanceMeters {get;set;} public DateTime CapturedAt {get;set;} public DateTime ReceivedAt {get;set;}=DateTime.UtcNow;
}
[Table("PosicionesCampo")] public class FieldTrack:Entity {
 public int ShiftId {get;set;} public double Latitude {get;set;} public double Longitude {get;set;}
 public double Accuracy {get;set;} public DateTime CapturedAt {get;set;} public DateTime ReceivedAt {get;set;}=DateTime.UtcNow;
}

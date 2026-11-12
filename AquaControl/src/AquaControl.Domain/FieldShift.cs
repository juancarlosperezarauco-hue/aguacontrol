using System.ComponentModel.DataAnnotations.Schema;
namespace AquaControl.Domain;

[Table("JornadasCampo")]
public class FieldShift:Entity {
 public int UserId {get;set;}
 public DateTime StartedAt {get;set;}=DateTime.UtcNow;
 public DateTime? EndedAt {get;set;}
 public double? Latitude {get;set;}
 public double? Longitude {get;set;}
 public double? Accuracy {get;set;}
 public DateTime? CapturedAt {get;set;}
 public DateTime? ReceivedAt {get;set;}
}

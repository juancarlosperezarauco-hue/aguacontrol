using AquaControl.Application;
using AquaControl.Domain;
using AquaControl.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Text.Json;
int count=0;
void Check(bool result,string label){if(!result)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);count++;}
void Reject(Action action,string label){try{action();}catch(BusinessException){Check(true,label);return;}throw new Exception("FAIL: "+label);}
StreetLine Line(params double[][] coords)=>new(coords.Select(c=>new Coordinate(c[0],c[1])).ToArray());
var u=new StreetGraph([Line([0,0],[0,.001],[.001,.001],[.001,0])]);
var plan=u.Plan(new(0,0,0),[new(1,0,.001)],"VEHICULO");
Check(plan.DistanceMeters>330&&plan.Path.Length==4,"Ruta en U respeta calles y no atraviesa la manzana");
var edge=new StreetGraph([Line([0,0],[.001,0])]);
var snapped=edge.Plan(new(0,0,.0002),[new(1,0,.0008)],"VEHICULO");
Check(snapped.DistanceMeters is >66 and <68,"Origen y destino se insertan en el mismo segmento sin rodeo por extremos");
var directed=new StreetGraph([Line([0,0],[.001,0]) with{OneWay="1"}]);
Reject(()=>directed.Plan(new(0,0,.001),[new(1,0,0)],"VEHICULO"),"Respeta sentido único vehicular");
Check(directed.Plan(new(0,0,.001),[new(1,0,0)],"A_PIE").DistanceMeters>100,"Modo a pie admite recorrido inverso");
var intersection=new StreetGraph([Line([0,0],[.001,0]),Line([.0005,-.001],[.0005,.001])]);
Check(intersection.Plan(new(0,0,0),[new(1,.001,.0005)],"VEHICULO").DistanceMeters is >160 and <170,"Noda intersecciones interiores de las vías");
var disconnected=new StreetGraph([Line([0,0],[.001,0]),Line([.003,0],[.004,0])]);
Reject(()=>disconnected.Plan(new(0,0,0),[new(1,0,.004)],"VEHICULO"),"Red desconectada no se une con una línea ficticia");
Reject(()=>edge.Plan(new(0,0,0),[new(1,.01,.01)],"VEHICULO"),"Rechaza medidor alejado de la vía más de 150 metros");
var bridge=new StreetGraph([Line([0,0],[.001,0]) with{Bridge="1"},Line([.0005,-.001],[.0005,.001])]);
Reject(()=>bridge.Plan(new(0,0,0),[new(1,.001,.0005)],"VEHICULO"),"No convierte cruce bajo puente en una intersección");
var ordered=intersection.Plan(new(0,0,0),[new(9,.001,.0005),new(2,0,.0005)],"VEHICULO");
Check(ordered.Stops[0].OrderId==2&&ordered.Stops[1].Sequence==2,"Ordena y numera por distancia vial");
if(args.Contains("--real-network")){
 var options=new DbContextOptionsBuilder<AquaDb>().UseSqlServer("Server=np:\\\\.\\pipe\\sql\\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true",o=>o.UseNetTopologySuite()).Options;
 await using var db=new AquaDb(options);
 // Read-only diagnostic against the original imported network, without modifying clients/orders.
 var road=await db.Set<Road>().AsNoTracking().Where(r=>r.Geom!=null&&r.Geom.SRID==4326).OrderBy(r=>r.Id).FirstAsync();
 var c=road.Geom!.Coordinates;var start=c[0];var target=c[c.Length/2];
 var actual=await new StreetNetwork(db).Plan(new(0,start.Y,start.X),[new(1,target.Y,target.X)],"VEHICULO");
 Check(actual.DistanceMeters>0&&actual.Path.Length>1&&actual.NetworkHash.Length==64,"Calcula sobre la capa Vías SQL Server real y genera huella de red");
 Console.WriteLine($"Red real: {actual.Path.Length} vértices, {actual.DistanceMeters:F0} m, hash {actual.NetworkHash[..12]}.");
}
Console.WriteLine($"Pruebas de alcance correctas: {count}.");

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AquaControl.Application;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Infrastructure;
public partial class GeoService(AquaDb db){
 static readonly GeometryFactory Factory=new(new PrecisionModel(),4326);
 static readonly Dictionary<string,string> Files=new(){["codes"]="Exp_CodigoFijo_4326",["lots"]="Exp_MapaBase_LOTES_4326",["blocks"]="Exp_MapaBase_MZA_4326",["roads"]="Exp_MapaBase_VIAS_4326"};
 static IEnumerable<(Dictionary<string,string> Fields,byte[] Shape)> Read(string stem){
  foreach(var ext in new[]{".shp",".shx",".dbf",".prj"})Require(File.Exists(stem+ext),"Falta archivo "+ext);
  var prj=File.ReadAllText(stem+".prj");Require(prj.Contains("WGS_1984")&&prj.Contains("Degree")&&!prj.Contains("PROJCS"),"Se requiere WGS84 geográfico; no se asigna SRID silenciosamente.");
  var cp=Directory.GetFiles(Path.GetDirectoryName(stem)!).FirstOrDefault(f=>Path.GetFileName(f).Equals(Path.GetFileName(stem)+".cpg",StringComparison.OrdinalIgnoreCase));Require(cp!=null&&File.ReadAllText(cp).Trim().Equals("UTF-8",StringComparison.OrdinalIgnoreCase),"Codificación UTF-8 requerida.");
  var d=File.ReadAllBytes(stem+".dbf");var shx=File.ReadAllBytes(stem+".shx");using var shp=File.OpenRead(stem+".shp");using var reader=new BinaryReader(shp);var header=reader.ReadBytes(100);Require(BinaryPrimitives.ReadInt32BigEndian(header)==9994,"SHP inválido.");Require(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(24))*2L==shp.Length,"Longitud SHP inválida.");
  int count=BitConverter.ToInt32(d,4),hl=BitConverter.ToUInt16(d,8),rl=BitConverter.ToUInt16(d,10);Require((shx.Length-100)/8==count,"Conteo SHX/DBF no coincide.");var fields=new List<(string Name,int Length)>();for(int p=32;d[p]!=13;p+=32)fields.Add((Encoding.ASCII.GetString(d,p,11).TrimEnd('\0'),d[p+16]));var utf8=new UTF8Encoding(false,true);
  for(int i=0;i<count;i++){var off=shp.Position;var h=reader.ReadBytes(8);Require(h.Length==8,"SHP truncado.");var length=BinaryPrimitives.ReadInt32BigEndian(h.AsSpan(4));Require(BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(100+i*8))*2L==off&&BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(104+i*8))==length,"Índice SHX inconsistente.");var shape=reader.ReadBytes(length*2);Require(shape.Length==length*2,"Registro SHP truncado.");int p=hl+i*rl;Require(d[p]!=42,"Registro DBF borrado: revisión requerida.");p++;var row=new Dictionary<string,string>();foreach(var f in fields){row[f.Name]=utf8.GetString(d,p,f.Length).Trim();p+=f.Length;}yield return (row,shape);}
 }
 static Geometry Geometry(byte[] bytes){using var stream=new MemoryStream(bytes);using var r=new BinaryReader(stream);int type=r.ReadInt32();if(type is 1 or 11){var x=r.ReadDouble();var y=r.ReadDouble();var z=type==11?r.ReadDouble():double.NaN;return Factory.CreatePoint(new CoordinateZ(x,y,z));}Require(type is 3 or 5 or 13 or 15,"Geometría no soportada.");stream.Position=36;var parts=r.ReadInt32();var count=r.ReadInt32();var starts=Enumerable.Range(0,parts).Select(_=>r.ReadInt32()).Append(count).ToArray();var coords=new Coordinate[count];for(int i=0;i<count;i++)coords[i]=new CoordinateZ(r.ReadDouble(),r.ReadDouble(),double.NaN);if(type is 13 or 15){r.ReadDouble();r.ReadDouble();for(int i=0;i<count;i++)coords[i].Z=r.ReadDouble();}
  if(type is 3 or 13)return Factory.CreateMultiLineString(Enumerable.Range(0,parts).Select(i=>Factory.CreateLineString(coords[starts[i]..starts[i+1]])).ToArray());
  var rings=Enumerable.Range(0,parts).Select(i=>Factory.CreateLinearRing(coords[starts[i]..starts[i+1]])).ToArray();var polygons=rings.Select(x=>Factory.CreatePolygon(x)).ToArray();var depth=new int[parts];for(int i=0;i<parts;i++)for(int j=0;j<parts;j++)if(i!=j&&polygons[j].Area>polygons[i].Area&&polygons[j].Covers(polygons[i].InteriorPoint))depth[i]++;
  var result=new List<Polygon>();for(int i=0;i<parts;i++)if(depth[i]%2==0){var holes=Enumerable.Range(0,parts).Where(j=>depth[j]==depth[i]+1&&polygons[i].Covers(polygons[j].InteriorPoint)).Select(j=>rings[j]).ToArray();result.Add(Factory.CreatePolygon(rings[i],holes));}return Factory.CreateMultiPolygon(result.ToArray());
 }
 public async Task Import(string folder){
  db.Database.SetCommandTimeout(300);
  foreach(var (layer,file) in Files){var stem=Path.Combine(folder,file);using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);foreach(var ext in new[]{".shp",".shx",".dbf",".prj"})sha.AppendData(File.ReadAllBytes(stem+ext));var hash=Convert.ToHexString(sha.GetHashAndReset());if(await db.Set<GeoImport>().AnyAsync(x=>x.Layer==layer&&x.Hash==hash)){Console.WriteLine(layer+": ya importado");continue;}Require(!await db.Set<GeoImport>().AnyAsync(x=>x.Layer==layer),"La capa ya tiene otra versión: reconciliar antes de reemplazar.");
   using var tx=await db.Database.BeginTransactionAsync();var import=new GeoImport{Layer=layer,Hash=hash};db.Add(import);await db.SaveChangesAsync();int ordinal=0,accepted=0,rejected=0;
   foreach(var (f,shape) in Read(stem)){ordinal++;var json=JsonSerializer.Serialize(f);Geometry g;try{g=Geometry(shape);Require(!g.IsEmpty&&g.IsValid,"Geometría vacía o inválida; no se reparó automáticamente.");Require(g.Coordinates.All(c=>double.IsFinite(c.X)&&double.IsFinite(c.Y)&&Math.Abs(c.X)<=180&&Math.Abs(c.Y)<=90),"Coordenadas fuera de rango.");}catch(Exception ex) when(ex is not OutOfMemoryException){db.Add(new GeoIssue{ImportId=import.Id,Ordinal=ordinal,Reason=ex.Message[..Math.Min(900,ex.Message.Length)],OriginalJson=json});rejected++;continue;}
    string? S(string key)=>f.GetValueOrDefault(key);int? I(string key)=>int.TryParse(S(key),out var v)?v:null;
    if(layer=="codes"){db.Add(new FixedCode{ImportId=import.Id,Ordinal=ordinal,CodF_SQL=I("CodF_SQL"),CodF_SIG=S("CodF_SIG"),CodFijo=I("CodFijo"),Nombre=S("Nombre"),Geom=g,Longitud=g.Coordinate.X,Latitud=g.Coordinate.Y,OriginalJson=json});if(double.TryParse(S("Latid"),CultureInfo.InvariantCulture,out var lat)&&double.TryParse(S("Longi"),CultureInfo.InvariantCulture,out var lon)&&(Math.Abs(lat-g.Coordinate.Y)>0.00001||Math.Abs(lon-g.Coordinate.X)>0.00001))db.Add(new GeoIssue{ImportId=import.Id,Ordinal=ordinal,Reason="Atributos Longi/Latid difieren del SHP. Consulta usa geometría; atributos preservados.",OriginalJson=json});}
    else if(layer=="lots")db.Add(new Lot{ImportId=import.Id,Ordinal=ordinal,IdOrigen=I("Id"),NroLote=S("NroLote"),Geom=g,OriginalJson=json});
    else if(layer=="blocks")db.Add(new Block{ImportId=import.Id,Ordinal=ordinal,IdOrigen=I("Id"),UV_MZA=S("UV_MZA"),UV=S("UV"),MZA=S("MZA"),Geom=g,OriginalJson=json});
    else db.Add(new Road{ImportId=import.Id,Ordinal=ordinal,OBJECTID=I("OBJECTID"),Nombre=S("Nombre")??S("name"),TipoVia=S("type"),OSMID=S("OSMID")??S("osm_id"),Geom=g,OriginalJson=json});accepted++;
    if(ordinal%300==0){await db.SaveChangesAsync();db.ChangeTracker.Clear();Console.WriteLine($"{layer}: {ordinal}");}
   }
   await db.SaveChangesAsync();var tracked=await db.Set<GeoImport>().SingleAsync(x=>x.Id==import.Id);tracked.Count=accepted;tracked.Rejected=rejected;await db.SaveChangesAsync();await tx.CommitAsync();db.ChangeTracker.Clear();Console.WriteLine($"{layer}: {accepted} aceptados, {rejected} rechazados.");
  }
  var blocks=await db.Set<Block>().AsNoTracking().ToListAsync();
  var blockTree=new NetTopologySuite.Index.Strtree.STRtree<Block>();
  foreach(var block in blocks)if(block.Geom!=null)blockTree.Insert(block.Geom.EnvelopeInternal,block);
  var lots=await db.Set<Lot>().ToListAsync();
  foreach(var lot in lots.Where(x=>x.IdManzana==null&&x.Geom!=null)){
   var point=lot.Geom!.InteriorPoint;var matches=blockTree.Query(point.EnvelopeInternal).Where(x=>x.Geom!.Intersects(point)).Take(2).ToList();if(matches.Count==1)lot.IdManzana=matches[0].Id;
  }
  await db.SaveChangesAsync();db.ChangeTracker.Clear();
  var lotTree=new NetTopologySuite.Index.Strtree.STRtree<Lot>();foreach(var lot in lots)if(lot.Geom!=null)lotTree.Insert(lot.Geom.EnvelopeInternal,lot);
  var codes=await db.Set<FixedCode>().Where(x=>x.IdLote==null).ToListAsync();foreach(var code in codes.Where(x=>x.Geom!=null)){var matches=lotTree.Query(code.Geom!.EnvelopeInternal).Where(x=>x.Geom!.Intersects(code.Geom)).Take(2).ToList();if(matches.Count==1)code.IdLote=matches[0].Id;}
  await db.SaveChangesAsync();db.ChangeTracker.Clear();
  // Identifiers below come exclusively from this fixed allowlist, never request data.
#pragma warning disable EF1002
  foreach(var table in new[]{"CodigosFijos","Lotes","Manzanas","Vias"})await db.Database.ExecuteSqlRawAsync($"IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='SIX_{table}_Geom' AND object_id=OBJECT_ID('dbo.{table}')) CREATE SPATIAL INDEX SIX_{table}_Geom ON dbo.{table}(Geom) USING GEOMETRY_AUTO_GRID WITH(BOUNDING_BOX=(-61.1,-16.5,-60.8,-16.2));");
#pragma warning restore EF1002
  Console.WriteLine("Asociaciones e indices espaciales completados.");
 }
 static object Geo(Geometry g){static double[] XY(Coordinate c)=>[c.X,c.Y];static object Rings(Polygon p)=>new[]{p.ExteriorRing.Coordinates.Select(XY).ToArray()}.Concat(Enumerable.Range(0,p.NumInteriorRings).Select(i=>p.GetInteriorRingN(i).Coordinates.Select(XY).ToArray())).ToArray();return g switch { Point p=>new{type="Point",coordinates=(object)XY(p.Coordinate)},LineString l=>new{type="LineString",coordinates=(object)l.Coordinates.Select(XY).ToArray()},Polygon p=>new{type="Polygon",coordinates=Rings(p)},MultiPolygon m=>new{type="MultiPolygon",coordinates=(object)Enumerable.Range(0,m.NumGeometries).Select(i=>Rings((Polygon)m.GetGeometryN(i))).ToArray()},MultiLineString m=>new{type="MultiLineString",coordinates=(object)Enumerable.Range(0,m.NumGeometries).Select(i=>m.GetGeometryN(i).Coordinates.Select(XY).ToArray()).ToArray()},_=>throw new BusinessException("Geometría de salida no soportada.")};}
 public async Task<object> Layer(Actor a,string layer,double west,double south,double east,double north){a.Require("geo.read");Require(west>=-180&&east<=180&&south>=-90&&north<=90&&west<east&&south<north,"Extensión inválida.");var box=Factory.ToGeometry(new Envelope(west,east,south,north));var features=new List<object>();const int limit=1000;
  void Add(int id,Geometry? g,object props){if(g!=null)features.Add(new{type="Feature",id,geometry=Geo(g),properties=props});}
  switch(layer){case "codes":foreach(var x in await db.Set<FixedCode>().AsNoTracking().Where(x=>x.Geom!=null&&x.Geom.Intersects(box)).OrderBy(x=>x.Id).Take(limit).ToListAsync())Add(x.Id,x.Geom,new{label=x.CodFijo?.ToString(),x.IdLote});break;
   case "lots":foreach(var x in await db.Set<Lot>().AsNoTracking().Where(x=>x.Geom!=null&&x.Geom.Intersects(box)).OrderBy(x=>x.Id).Take(limit).ToListAsync())Add(x.Id,x.Geom,new{label=x.NroLote,x.IdManzana});break;
   case "blocks":foreach(var x in await db.Set<Block>().AsNoTracking().Where(x=>x.Geom!=null&&x.Geom.Intersects(box)).OrderBy(x=>x.Id).Take(limit).ToListAsync())Add(x.Id,x.Geom,new{label=x.UV_MZA});break;
   case "roads":foreach(var x in await db.Set<Road>().AsNoTracking().Where(x=>x.Geom!=null&&x.Geom.Intersects(box)).OrderBy(x=>x.Id).Take(limit).ToListAsync())Add(x.Id,x.Geom,new{label=x.Nombre});break;
   default:throw new BusinessException("Capa desconocida.");}
  return new{type="FeatureCollection",features,truncated=features.Count==limit};
 }
}

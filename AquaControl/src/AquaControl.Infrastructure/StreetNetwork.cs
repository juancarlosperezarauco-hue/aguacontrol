using AquaControl.Application;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Noding;
using NetTopologySuite.Noding.Snapround;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Infrastructure;

public class StreetNetwork(AquaDb db):IStreetNetwork {
 public async Task<StreetPlan> Plan(RoutePoint start,RoutePoint[] stops,string mode){
  var roads=await db.Set<Road>().AsNoTracking().Where(x=>x.Geom!=null).OrderBy(x=>x.Id).ToListAsync();
  Require(roads.Count>0,"Importe la capa Vías antes de calcular recorridos.");
  Require(roads.All(x=>x.Geom!.SRID==4326&&x.Geom.IsValid),"Existen vías inválidas o con SRID distinto de 4326. Revise el diagnóstico SIG.");
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',roads.Select(r=>$"{r.Id}:{Convert.ToHexString(r.Geom!.AsBinary())}:{r.OriginalJson}")))));
  return new StreetGraph(roads.SelectMany(r=>Lines(r.Geom!).Select(line=>new StreetLine(line.Coordinates,Tag(r.OriginalJson,"oneway"),Tag(r.OriginalJson,"bridge"),Tag(r.OriginalJson,"tunnel")))).ToArray()).Plan(start,stops,mode,hash);
 }
 static string Tag(string json,string key){try{using var d=JsonDocument.Parse(json);return d.RootElement.TryGetProperty(key,out var v)?v.ToString():"";}catch{return "";}}
 static IEnumerable<LineString> Lines(Geometry g){if(g is LineString l)yield return l;else if(g is GeometryCollection m)for(int i=0;i<m.NumGeometries;i++)foreach(var line in Lines(m.GetGeometryN(i)))yield return line;}
}
public record StreetLine(Coordinate[] Coordinates,string OneWay="0",string Bridge="0",string Tunnel="0");

// Coordinates are projected locally to metres before noding. Each edge retains its source direction.
// Grade-separated interiors stay in separate graphs; bridge/tunnel endpoints join the surface graph.
public class StreetGraph(StreetLine[] source) {
 record Segment(Coordinate A,Coordinate B,string Direction,string Level);
 record Arc(int To,double Cost);
 const double Earth=6371008.8;
 public StreetPlan Plan(RoutePoint start,RoutePoint[] stops,string mode,string hash="TEST"){
  Require(stops.Length is >0 and <=50,"Seleccione entre 1 y 50 paradas.");
  Require(source.Length>0,"Red vial vacía.");
  var lon0=start.Longitude;var lat0=start.Latitude;var cos=Math.Cos(lat0*Math.PI/180);
  Require(Math.Abs(cos)>.01,"La proyección local no admite puntos próximos a los polos.");
  Coordinate Project(Coordinate c)=>new((c.X-lon0)*Math.PI/180*Earth*cos,(c.Y-lat0)*Math.PI/180*Earth);
  double[] Wgs(Coordinate c)=>[lon0+c.X/Earth/cos*180/Math.PI,lat0+c.Y/Earth*180/Math.PI];
  var segments=new List<Segment>();var endpoints=new HashSet<(long,long)>();
  (long,long) Key(Coordinate c)=>((long)Math.Round(c.X*10),(long)Math.Round(c.Y*10));
  foreach(var group in source.GroupBy(s=>s.Bridge is "1" or "yes"?"bridge":s.Tunnel is "1" or "yes"?"tunnel":"ground")){
   var strings=new List<ISegmentString>();
   foreach(var road in group){
    var coords=road.Coordinates.Select(Project).ToArray();if(coords.Length<2)continue;
    endpoints.Add(Key(coords[0]));endpoints.Add(Key(coords[^1]));
    strings.Add(new NodedSegmentString(coords,road.OneWay));
   }
   var noder=new SnapRoundingNoder(new PrecisionModel(10));noder.ComputeNodes(strings);
   foreach(var line in noder.GetNodedSubstrings())for(int i=1;i<line.Coordinates.Length;i++){
    var a=line.Coordinates[i-1];var b=line.Coordinates[i];if(a.Distance(b)>.01)segments.Add(new(a,b,line.Context?.ToString()??"0",group.Key));
   }
  }
  Require(segments.Count>0,"Las vías no contienen segmentos utilizables.");
  var coordinates=new List<Coordinate>();var adjacency=new List<List<Arc>>();var nodes=new Dictionary<(long,long,string),int>();
  int Node(Coordinate c,string level){var key=Key(c);var nodeKey=(key.Item1,key.Item2,endpoints.Contains(key)?"ground":level);if(nodes.TryGetValue(nodeKey,out var found))return found;var id=coordinates.Count;coordinates.Add(c);adjacency.Add([]);nodes[nodeKey]=id;return id;}
  var all=new[]{start}.Concat(stops).ToArray();var attachments=new (int segment,double t,Coordinate point,double distance)[all.Length];
  for(int p=0;p<all.Length;p++){
   var point=Project(new(all[p].Longitude,all[p].Latitude));double best=double.PositiveInfinity;int edge=-1;double at=0;Coordinate? closest=null;
   for(int i=0;i<segments.Count;i++){var s=segments[i];double dx=s.B.X-s.A.X,dy=s.B.Y-s.A.Y;double t=Math.Clamp(((point.X-s.A.X)*dx+(point.Y-s.A.Y)*dy)/(dx*dx+dy*dy),0,1);var q=new Coordinate(s.A.X+t*dx,s.A.Y+t*dy);var distance=q.Distance(point);if(distance<best){best=distance;edge=i;at=t;closest=q;}}
   Require(best<=150,$"{(p==0?"Punto de partida":"Orden "+all[p].Id)}: la vía más próxima está a {best:F0} m. Verifique la ubicación (máximo 150 m de acceso al predio).");
   attachments[p]=(edge,at,closest!,best);
  }
  var pointNodes=new int[all.Length];
  for(int e=0;e<segments.Count;e++){
   var s=segments[e];var inserts=new List<(double t,Coordinate point,int p)>{(0,s.A,-1),(1,s.B,-1)};
   for(int p=0;p<attachments.Length;p++)if(attachments[p].segment==e)inserts.Add((attachments[p].t,attachments[p].point,p));
   inserts.Sort((a,b)=>a.t.CompareTo(b.t));
   for(int i=0;i<inserts.Count;i++){
    var v=Node(inserts[i].point,s.Level);if(inserts[i].p>=0)pointNodes[inserts[i].p]=v;if(i==0)continue;
    var u=Node(inserts[i-1].point,s.Level);var length=coordinates[u].Distance(coordinates[v]);if(u==v)continue;
    if(mode=="A_PIE"||s.Direction is not ("-1" or "reverse"))adjacency[u].Add(new(v,length));
    if(mode=="A_PIE"||s.Direction is not ("1" or "yes" or "true"))adjacency[v].Add(new(u,length));
   }
  }
  (double[] dist,int[] previous) Dijkstra(int origin){
   var dist=Enumerable.Repeat(double.PositiveInfinity,coordinates.Count).ToArray();var previous=Enumerable.Repeat(-1,coordinates.Count).ToArray();var queue=new PriorityQueue<int,double>();dist[origin]=0;queue.Enqueue(origin,0);
   while(queue.TryDequeue(out int u,out double weight)){
    if(weight>dist[u])continue;foreach(var arc in adjacency[u]){var next=weight+arc.Cost;if(next<dist[arc.To]){dist[arc.To]=next;previous[arc.To]=u;queue.Enqueue(arc.To,next);}}
   }return(dist,previous);
  }
  var remaining=Enumerable.Range(1,stops.Length).ToList();var planned=new List<StreetStop>();var path=new List<double[]>();int current=pointNodes[0];double total=0;
  path.Add(Wgs(coordinates[current]));
  while(remaining.Count>0){
   var (dist,prev)=Dijkstra(current);int target=remaining.OrderBy(p=>dist[pointNodes[p]]).ThenBy(p=>all[p].Id).First();var dest=pointNodes[target];
   Require(double.IsFinite(dist[dest]),$"No hay recorrido vial hacia la orden {all[target].Id}. La red está desconectada o el sentido de circulación impide llegar. No se dibujó un atajo entre lotes.");
   var leg=new List<int>();for(int node=dest;node!=current;node=prev[node]){Require(node>=0,"Trayecto vial incompleto.");leg.Add(node);}leg.Reverse();path.AddRange(leg.Select(n=>Wgs(coordinates[n])));
   total+=dist[dest];planned.Add(new(all[target].Id,all[target].Latitude,all[target].Longitude,planned.Count+1,dist[dest],attachments[target].distance));current=dest;remaining.Remove(target);
  }
  return new(hash,path.ToArray(),planned.ToArray(),total,["Dijkstra calcula cada tramo sobre Vías. El orden de visitas es una heurística por cercanía vial; no garantiza el mínimo global.","El trazado termina en la vía más cercana. El acceso final al medidor se muestra por separado; no es una vía vehicular."]);
 }
}

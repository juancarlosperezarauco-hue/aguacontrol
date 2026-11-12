using AquaControl.Application;
using NetTopologySuite.Geometries;
namespace AquaControl.Infrastructure;
public static class GeographicMetrics {
 public static object? Measure(Geometry? geometry){
  if(geometry==null||geometry.IsEmpty)return null;
  double length=0,area=0;var sides=new List<double>();
  void Line(Coordinate[] c){for(int i=1;i<c.Length;i++){var distance=AquaService.DistanceMeters(c[i-1].Y,c[i-1].X,c[i].Y,c[i].X);length+=distance;if(sides.Count<200)sides.Add(Math.Round(distance,2));}}
  double Ring(Coordinate[] c){if(c.Length<3)return 0;var latitude=c.Average(p=>p.Y)*Math.PI/180;double factor=6371008.8*Math.PI/180;double a=0;for(int i=1;i<c.Length;i++)a+=(c[i-1].X-c[0].X)*(c[i].Y-c[0].Y)-(c[i].X-c[0].X)*(c[i-1].Y-c[0].Y);return Math.Abs(a)*factor*factor*Math.Cos(latitude)/2;}
  void Walk(Geometry g){switch(g){case Polygon p:Line(p.ExteriorRing.Coordinates);area+=Ring(p.ExteriorRing.Coordinates);for(int h=0;h<p.NumInteriorRings;h++){Line(p.GetInteriorRingN(h).Coordinates);area-=Ring(p.GetInteriorRingN(h).Coordinates);}break;case LineString l:Line(l.Coordinates);break;case GeometryCollection collection:for(int i=0;i<collection.NumGeometries;i++)Walk(collection.GetGeometryN(i));break;}}
  Walk(geometry);return new{perimeterMeters=Math.Round(length,2),areaSquareMeters=Math.Round(area,2),segmentLengthsMeters=sides,approximate=true,method="Distancias geodésicas esféricas y superficie con proyección local; referencia operativa, no levantamiento catastral."};
 }
}

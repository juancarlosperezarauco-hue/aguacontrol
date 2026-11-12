using Microsoft.EntityFrameworkCore;
namespace AquaControl.Infrastructure;
public static class SchemaReadiness {
 public static async Task Verify(AquaDb db){
  var ready=await db.Database.SqlQueryRaw<int>("""
   SELECT CASE WHEN OBJECT_ID('dbo.JornadasCampo','U') IS NOT NULL
    AND OBJECT_ID('dbo.VisitasCampo','U') IS NOT NULL
    AND OBJECT_ID('dbo.PosicionesCampo','U') IS NOT NULL
    AND OBJECT_ID('dbo.ParadasRuta','U') IS NOT NULL
    AND COL_LENGTH('dbo.RutasCampo','RequestId')=16
    AND COL_LENGTH('dbo.RutasCampo','SourceHash')=128
    AND COL_LENGTH('dbo.Evidencias','MeterId') IS NOT NULL
    THEN 1 ELSE 0 END AS [Value]
   """).SingleAsync();
  if(ready!=1)throw new InvalidOperationException("La base requiere actualización. Conserve un respaldo y ejecute scripts/upgrade-scope.ps1 (incrementos 15, 16 y 17) antes de iniciar esta versión.");
 }
}

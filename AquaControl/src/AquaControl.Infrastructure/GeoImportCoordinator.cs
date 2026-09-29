using System.Collections.Concurrent;
using System.Text;
using AquaControl.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AquaControl.Infrastructure;

public sealed record GeoJobView(Guid Id,string Status,string Mode,string Layer,int Processed,int Total,int Accepted,int Rejected,int Omitted,int Warnings,string Stage,DateTime StartedAt,DateTime? FinishedAt,string? Error,bool CanCancel);

public sealed class GeoImportCoordinator(IServiceScopeFactory scopes)
{
    sealed record StagedSource(string Folder,int UserId);
    sealed class Job
    {
        readonly object gate=new();
        readonly Dictionary<string,GeoProgress> layers=new(StringComparer.OrdinalIgnoreCase);
        public Guid Id {get;}=Guid.NewGuid(); public CancellationTokenSource Cancellation {get;}=new();
        public string Status {get;set;}="EN_COLA"; public string Mode {get;init;}="APPEND"; public string Layer {get;set;}=""; public int Total {get;init;} public string Stage {get;set;}="Esperando"; public DateTime StartedAt {get;}=DateTime.UtcNow; public DateTime? FinishedAt {get;set;} public string? Error {get;set;}
        public void Report(GeoProgress p){lock(gate){Layer=p.Layer;Stage=p.Stage;layers[p.Layer]=p;}}
        public GeoJobView View(){lock(gate){var values=layers.Values.ToList();return new(Id,Status,Mode,Layer,values.Sum(x=>x.Processed),Total,values.Sum(x=>x.Accepted),values.Sum(x=>x.Rejected),values.Sum(x=>x.Omitted),values.Sum(x=>x.Warnings),Stage,StartedAt,FinishedAt,Error,Status is "EN_COLA" or "IMPORTANDO");}}
        public byte[] Summary(string format){lock(gate){var rows=layers.Values.OrderBy(x=>x.Layer).ToList();if(format.Equals("txt",StringComparison.OrdinalIgnoreCase))return Encoding.UTF8.GetBytes($"AquaControl - resumen de migración SIG{Environment.NewLine}Id: {Id}{Environment.NewLine}Estado: {Status}{Environment.NewLine}Modo: {Mode}{Environment.NewLine}Inicio UTC: {StartedAt:O}{Environment.NewLine}Fin UTC: {FinishedAt:O}{Environment.NewLine}{Environment.NewLine}"+string.Join(Environment.NewLine,rows.Select(x=>$"{x.Layer}: procesados={x.Processed}; aceptados={x.Accepted}; rechazados={x.Rejected}; omitidos={x.Omitted}; advertencias={x.Warnings}; estado={x.Stage}")));var csv=new StringBuilder("capa,procesados,aceptados,rechazados,omitidos,advertencias,estado\r\n");foreach(var x in rows)csv.Append($"{x.Layer},{x.Processed},{x.Accepted},{x.Rejected},{x.Omitted},{x.Warnings},\"{x.Stage.Replace("\"","\"\"")}\"\r\n");return Encoding.UTF8.GetBytes(csv.ToString());}}
    }
    readonly ConcurrentDictionary<Guid,Job> jobs=new(); readonly ConcurrentDictionary<Guid,StagedSource> stagedSources=new(); readonly SemaphoreSlim startGate=new(1,1);
    public GeoJobView? Current()=>jobs.Values.OrderByDescending(x=>x.StartedAt).FirstOrDefault()?.View();
    public byte[] Summary(Guid id,string format)=>jobs.TryGetValue(id,out var job)?job.Summary(format):throw new InvalidOperationException("Importación no encontrada.");
    public Guid RegisterStagedSource(string folder,int userId){var id=Guid.NewGuid();stagedSources[id]=new(folder,userId);_ = Task.Delay(TimeSpan.FromHours(1)).ContinueWith(_=>{if(stagedSources.TryRemove(id,out var stale))try{Directory.Delete(stale.Folder,true);}catch{}});return id;}
    public (string Folder,bool DeleteAfter) Source(Guid? id,int userId,string fallback){if(id is not { } sourceId)return(fallback,false);if(!stagedSources.TryRemove(sourceId,out var source)||source.UserId!=userId)throw new InvalidOperationException("La selección SIG no existe, venció o pertenece a otro usuario.");return(source.Folder,true);}
    public async Task<GeoJobView> Start(string folder,string? requestedMode,int userId,bool deleteAfter=false)
    {
        var mode=(requestedMode??"APPEND").Trim().ToUpperInvariant();if(mode is not ("APPEND" or "REPLACE"))throw new InvalidOperationException("Modo inválido. Use APPEND o REPLACE.");await startGate.WaitAsync();
        try{if(jobs.Values.Any(x=>x.View().CanCancel))throw new InvalidOperationException("Ya existe una importación SIG en curso.");await using var previewScope=scopes.CreateAsyncScope();var geo=previewScope.ServiceProvider.GetRequiredService<GeoService>();var preview=await geo.Preview(folder);if(!preview.Valid)throw new InvalidOperationException("La fuente SIG tiene errores. Corrija la previsualización antes de importar.");var job=new Job{Mode=mode,Total=preview.TotalRecords};jobs[job.Id]=job;_ = Task.Run(async()=>{await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AquaDb>();var service=scope.ServiceProvider.GetRequiredService<GeoService>();try{job.Status="IMPORTANDO";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.iniciar",Resource=job.Id.ToString(),Detail=$"Importación SIG {mode} iniciada desde consola."});await db.SaveChangesAsync();await service.Import(folder,mode,userId,job.Report,job.Cancellation.Token);job.Status="COMPLETADA";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.completar",Resource=job.Id.ToString(),Detail="Importación SIG completada."});await db.SaveChangesAsync();}catch(OperationCanceledException){job.Status="CANCELADA";job.Stage="Cancelada: la capa en curso fue revertida.";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.cancelar",Resource=job.Id.ToString(),Detail="Cancelada por el usuario; se preservan capas ya completadas."});await db.SaveChangesAsync();}catch(Exception ex){job.Status="ERROR";job.Error=ex.Message;db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.error",Resource=job.Id.ToString(),Detail=ex.Message[..Math.Min(1900,ex.Message.Length)]});await db.SaveChangesAsync();}finally{job.FinishedAt=DateTime.UtcNow;if(deleteAfter)try{Directory.Delete(folder,true);}catch{}}});return job.View();}finally{startGate.Release();}
    }
    public GeoJobView Cancel(Guid id){if(!jobs.TryGetValue(id,out var job))throw new InvalidOperationException("Importación no encontrada.");if(!job.View().CanCancel)throw new InvalidOperationException("La importación ya finalizó.");job.Cancellation.Cancel();return job.View();}
}

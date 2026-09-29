using System.Collections.Concurrent;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AquaControl.Infrastructure;

public sealed record GeoJobView(Guid Id,string Status,string Layer,int Processed,int Accepted,int Rejected,string Stage,DateTime StartedAt,DateTime? FinishedAt,string? Error,bool CanCancel);

public sealed class GeoImportCoordinator(IServiceScopeFactory scopes)
{
    sealed class Job
    {
        readonly object gate=new();
        public Guid Id {get;}=Guid.NewGuid(); public CancellationTokenSource Cancellation {get;}=new();
        public string Status {get;set;}="EN_COLA"; public string Layer {get;set;}=""; public int Processed {get;set;} public int Accepted {get;set;} public int Rejected {get;set;} public string Stage {get;set;}="Esperando"; public DateTime StartedAt {get;}=DateTime.UtcNow; public DateTime? FinishedAt {get;set;} public string? Error {get;set;}
        public void Report(GeoProgress p){lock(gate){Layer=p.Layer;Processed=p.Processed;Accepted=p.Accepted;Rejected=p.Rejected;Stage=p.Stage;}}
        public GeoJobView View(){lock(gate)return new(Id,Status,Layer,Processed,Accepted,Rejected,Stage,StartedAt,FinishedAt,Error,Status is "EN_COLA" or "IMPORTANDO");}
    }
    readonly ConcurrentDictionary<Guid,Job> jobs=new();
    public GeoJobView? Current()=>jobs.Values.OrderByDescending(x=>x.StartedAt).FirstOrDefault()?.View();
    public GeoJobView Start(string folder,int userId)
    {
        if(jobs.Values.Any(x=>x.View().CanCancel))throw new InvalidOperationException("Ya existe una importación SIG en curso.");
        var job=new Job();jobs[job.Id]=job;_ = Task.Run(async()=>{
            await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AquaDb>();var geo=scope.ServiceProvider.GetRequiredService<GeoService>();
            try{
                job.Status="IMPORTANDO";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.iniciar",Resource=job.Id.ToString(),Detail="Importación iniciada desde consola SIG."});await db.SaveChangesAsync();
                await geo.Import(folder,job.Report,job.Cancellation.Token);
                job.Status="COMPLETADA";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.completar",Resource=job.Id.ToString(),Detail="Importación SIG completada."});await db.SaveChangesAsync();
            }catch(OperationCanceledException){job.Status="CANCELADA";job.Stage="Cancelada: la capa en curso fue revertida.";db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.cancelar",Resource=job.Id.ToString(),Detail="Cancelada por el usuario; se preservan capas ya completadas."});await db.SaveChangesAsync();}
            catch(Exception ex){job.Status="ERROR";job.Error=ex.Message;db.Set<Audit>().Add(new Audit{UserId=userId,Action="sig.importar.error",Resource=job.Id.ToString(),Detail=ex.Message[..Math.Min(1900,ex.Message.Length)]});await db.SaveChangesAsync();}
            finally{job.FinishedAt=DateTime.UtcNow;}
        });
        return job.View();
    }
    public GeoJobView Cancel(Guid id)
    {
        if(!jobs.TryGetValue(id,out var job))throw new InvalidOperationException("Importación no encontrada.");
        if(!job.View().CanCancel)throw new InvalidOperationException("La importación ya finalizó.");
        job.Cancellation.Cancel();return job.View();
    }
}

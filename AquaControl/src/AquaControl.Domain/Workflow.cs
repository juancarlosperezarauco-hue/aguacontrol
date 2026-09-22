namespace AquaControl.Domain;

public sealed class BusinessException(string message) : Exception(message);
public static class Workflow
{
    public static readonly Dictionary<string,string[]> Transitions = new()
    {
        ["PENDIENTE"]=["ASIGNADA","REPROGRAMADA","CANCELADA"],
        ["ASIGNADA"]=["EN_CAMINO","REPROGRAMADA","CANCELADA"],
        ["EN_CAMINO"]=["EN_EJECUCION","NO_REALIZADA","REPROGRAMADA","CANCELADA"],
        ["EN_EJECUCION"]=["FINALIZADA","NO_REALIZADA","CANCELADA"],
        ["FINALIZADA"]=["VERIFICADA","EN_EJECUCION"],
        ["VERIFICADA"]=["CERRADA"],
        ["NO_REALIZADA"]=["REPROGRAMADA","CANCELADA"],
        ["REPROGRAMADA"]=["PENDIENTE","ASIGNADA","CANCELADA"],
        ["CANCELADA"]=[],["CERRADA"]=[]
    };
    public static void Check(string from,string to)
    { if(!Transitions.TryGetValue(from,out var allowed)||!allowed.Contains(to)) throw new BusinessException($"Transición inválida: {from} → {to}."); }
    public static void Require(bool condition,string message) { if(!condition) throw new BusinessException(message); }
    public static decimal Money(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero);
}

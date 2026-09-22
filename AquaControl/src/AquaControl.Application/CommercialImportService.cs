using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace AquaControl.Application;

public interface IGeoReference { Task<int?> FixedCodeId(string code); }

public sealed class CommercialImportService(IData db, AquaService service, IGeoReference geo)
{
    static readonly string[] Required = ["01_clientes.csv","02_cuentas.csv","03_conexiones.csv","04_medidores.csv","05_instalaciones_medidor.csv","06_tarifas.csv","07_contratos.csv"];
    public sealed record Result(bool Valid, Dictionary<string,int> Rows, List<string> Errors, int Imported = 0);
    sealed record Row(Dictionary<string,string> Values, int Number) { public string this[string key] => Values.GetValueOrDefault(key, "").Trim(); }

    public async Task<Result> Validate(IReadOnlyDictionary<string,string> files)
    {
        var errors=new List<string>(); var rows=Parse(files,errors); await ValidateRows(rows,errors);
        return new(errors.Count==0,rows.ToDictionary(x=>x.Key,x=>x.Value.Count),errors);
    }
    public async Task<Result> Execute(Actor actor,IReadOnlyDictionary<string,string> files)
    {
        actor.Require("customers.write");actor.Require("billing.write");var result=await Validate(files);if(!result.Valid)return result;
        var rows=Parse(files,[]);await using var tx=await db.Database.BeginTransactionAsync();
        try {
            var clients=new Dictionary<string,Client>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in rows["01_clientes.csv"]){var x=new Client{Document=r["documento"],Name=r["nombre"],Phone=r["telefono"],Email=r["email"],Address=r["direccion"],Active=Bool(r["activo"])};db.Set<Client>().Add(x);clients.Add(x.Document,x);} await db.SaveChangesAsync();
            var accounts=new Dictionary<string,Account>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in rows["02_cuentas.csv"]){var x=new Account{Number=r["numero_cuenta"],Active=Bool(r["activo"])};db.Set<Account>().Add(x);accounts.Add(x.Number,x);}
            var tariffs=new Dictionary<string,Tariff>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in rows["06_tarifas.csv"]){var x=new Tariff{Name=r["nombre"],Currency=Default(r["moneda"],"BOB").ToUpperInvariant(),FixedCharge=Dec(r,"cargo_fijo"),UnitPrice=Dec(r,"precio_m3"),Start=Date(r,"fecha_inicio"),Active=Bool(r["activa"])};db.Set<Tariff>().Add(x);tariffs.Add(x.Name,x);}await db.SaveChangesAsync();
            var connections=new Dictionary<string,Connection>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in rows["03_conexiones.csv"]){var x=new Connection{Code=r["codigo_conexion"],FixedCodeId=await FixedId(r["codigo_fijo"]),SectorId=await SectorId(r["id_sector"]),RoadId=Int(r["id_via"]),Address=r["direccion"],Longitude=Double(r,"longitud"),Latitude=Double(r,"latitud"),Status=Default(r["estado"],"ACTIVA").ToUpperInvariant()};db.Set<Connection>().Add(x);connections.Add(x.Code,x);}
            var meters=new Dictionary<string,Meter>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in rows["04_medidores.csv"]){var x=new Meter{Serial=r["serie"],Model=r["modelo"],Active=Bool(r["activo"])};db.Set<Meter>().Add(x);meters.Add(x.Serial,x);}await db.SaveChangesAsync();
            foreach(var r in rows["05_instalaciones_medidor.csv"])db.Set<MeterInstallation>().Add(new(){ConnectionId=connections[r["codigo_conexion"]].Id,MeterId=meters[r["serie"]].Id,InitialReading=Dec(r,"lectura_inicial"),Start=Date(r,"fecha_instalacion")});
            foreach(var r in rows["07_contratos.csv"])db.Set<Contract>().Add(new(){ClientId=clients[r["documento"]].Id,AccountId=accounts[r["numero_cuenta"]].Id,ConnectionId=connections[r["codigo_conexion"]].Id,TariffId=tariffs[r["tarifa"]].Id,Start=Date(r,"fecha_inicio"),End=NullableDate(r["fecha_fin"])});
            await db.SaveChangesAsync();service.Audit(actor,"carga.comercial","padrón",$"Clientes {clients.Count}; cuentas {accounts.Count}; conexiones {connections.Count}; medidores {meters.Count}.");await db.SaveChangesAsync();await tx.CommitAsync();return result with{Imported=clients.Count+accounts.Count+connections.Count+meters.Count};
        } catch {await tx.RollbackAsync();throw;}
    }
    Dictionary<string,List<Row>> Parse(IReadOnlyDictionary<string,string> files,List<string> errors){var output=new Dictionary<string,List<Row>>(StringComparer.OrdinalIgnoreCase);foreach(var file in Required){if(!files.TryGetValue(file,out var source)){errors.Add($"Falta el archivo {file}.");continue;}var lines=source.Replace("\r\n","\n").Replace('\r','\n').Split('\n',StringSplitOptions.RemoveEmptyEntries);if(lines.Length==0){errors.Add($"{file} está vacío.");continue;}var heads=Csv(lines[0]).Select(x=>x.Trim().Trim('\ufeff')).ToArray();var list=new List<Row>();for(var n=1;n<lines.Length;n++){var values=Csv(lines[n]);if(values.Count!=heads.Length){errors.Add($"{file}, fila {n+1}: columnas inválidas.");continue;}list.Add(new Row(heads.Zip(values).ToDictionary(x=>x.First,x=>x.Second,StringComparer.OrdinalIgnoreCase),n+1));}output[file]=list;}return output;}
    async Task ValidateRows(Dictionary<string,List<Row>> x,List<string> e){if(x.Count!=Required.Length)return;Need(x,e,"01_clientes.csv",["documento","nombre","telefono","email","direccion","activo"]);Need(x,e,"02_cuentas.csv",["numero_cuenta","activo"]);Need(x,e,"03_conexiones.csv",["codigo_conexion","codigo_fijo","id_sector","id_via","direccion","longitud","latitud","estado"]);Need(x,e,"04_medidores.csv",["serie","modelo","activo"]);Need(x,e,"05_instalaciones_medidor.csv",["codigo_conexion","serie","lectura_inicial","fecha_instalacion"]);Need(x,e,"06_tarifas.csv",["nombre","moneda","cargo_fijo","precio_m3","fecha_inicio","activa"]);Need(x,e,"07_contratos.csv",["documento","numero_cuenta","codigo_conexion","tarifa","fecha_inicio","fecha_fin"]);Unique(x["01_clientes.csv"],"documento",e);Unique(x["02_cuentas.csv"],"numero_cuenta",e);Unique(x["03_conexiones.csv"],"codigo_conexion",e);Unique(x["04_medidores.csv"],"serie",e);Unique(x["06_tarifas.csv"],"nombre",e);foreach(var r in x["03_conexiones.csv"]){if(!double.TryParse(r["longitud"],CultureInfo.InvariantCulture,out var lon)||!double.TryParse(r["latitud"],CultureInfo.InvariantCulture,out var lat)||lon is < -180 or > 180||lat is < -90 or > 90)e.Add($"Conexiones fila {r.Number}: coordenadas inválidas.");if(string.IsNullOrWhiteSpace(r["codigo_fijo"]))e.Add($"Conexiones fila {r.Number}: Código Fijo SIG obligatorio.");else if(await FixedId(r["codigo_fijo"]) is null)e.Add($"Conexiones fila {r.Number}: código fijo SIG no encontrado o ambiguo.");}foreach(var r in x["05_instalaciones_medidor.csv"]){if(!Exists(x["03_conexiones.csv"],"codigo_conexion",r["codigo_conexion"])||!Exists(x["04_medidores.csv"],"serie",r["serie"]))e.Add($"Instalaciones fila {r.Number}: conexión o medidor inexistente.");}foreach(var r in x["07_contratos.csv"])if(!Exists(x["01_clientes.csv"],"documento",r["documento"])||!Exists(x["02_cuentas.csv"],"numero_cuenta",r["numero_cuenta"])||!Exists(x["03_conexiones.csv"],"codigo_conexion",r["codigo_conexion"])||!Exists(x["06_tarifas.csv"],"nombre",r["tarifa"]))e.Add($"Contratos fila {r.Number}: referencias inexistentes.");var docs=x["01_clientes.csv"].Select(r=>r["documento"]).ToList();var accounts=x["02_cuentas.csv"].Select(r=>r["numero_cuenta"]).ToList();var connections=x["03_conexiones.csv"].Select(r=>r["codigo_conexion"]).ToList();var meters=x["04_medidores.csv"].Select(r=>r["serie"]).ToList();if(await db.Set<Client>().AnyAsync(r=>docs.Contains(r.Document)))e.Add("Ya existe un cliente con uno de los documentos importados.");if(await db.Set<Account>().AnyAsync(r=>accounts.Contains(r.Number)))e.Add("Ya existe una cuenta de abonado importada.");if(await db.Set<Connection>().AnyAsync(r=>connections.Contains(r.Code)))e.Add("Ya existe una conexión importada.");if(await db.Set<Meter>().AnyAsync(r=>meters.Contains(r.Serial)))e.Add("Ya existe un medidor importado.");}
    static void Need(Dictionary<string,List<Row>> x,List<string> e,string f,string[] cols){if(!x.TryGetValue(f,out var r))return;IEnumerable<string> keys=r.Count>0?r[0].Values.Keys:Array.Empty<string>();foreach(var c in cols)if(!keys.Contains(c,StringComparer.OrdinalIgnoreCase))e.Add($"{f}: falta columna {c}.");}
    static void Unique(List<Row> rows,string key,List<string> errors){foreach(var g in rows.Where(x=>!string.IsNullOrWhiteSpace(x[key])).GroupBy(x=>x[key],StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))errors.Add($"Valor duplicado en {key}: {g.Key}.");}
    static bool Exists(List<Row> r,string k,string value)=>r.Any(x=>string.Equals(x[k],value,StringComparison.OrdinalIgnoreCase));
    Task<int?> FixedId(string code)=>geo.FixedCodeId(code);
    async Task<int?> SectorId(string code)=>string.IsNullOrWhiteSpace(code)?null:await db.Set<Sector>().Where(x=>x.Code==code).Select(x=>(int?)x.Id).SingleOrDefaultAsync();
    static List<string> Csv(string s){var r=new List<string>();var b=new StringBuilder();var quoted=false;for(var i=0;i<s.Length;i++){if(s[i]=='"'){if(quoted&&i+1<s.Length&&s[i+1]=='"'){b.Append('"');i++;}else quoted=!quoted;}else if(s[i]==','&&!quoted){r.Add(b.ToString());b.Clear();}else b.Append(s[i]);}r.Add(b.ToString());return r;}
    static string Default(string a,string b)=>string.IsNullOrWhiteSpace(a)?b:a;static bool Bool(string x)=>bool.TryParse(x,out var b)&&b;static int? Int(string x)=>int.TryParse(x,out var n)?n:null;static decimal Dec(Row r,string key)=>decimal.Parse(r[key],CultureInfo.InvariantCulture);static double Double(Row r,string key)=>double.Parse(r[key],CultureInfo.InvariantCulture);static DateTime Date(Row r,string key)=>DateTime.SpecifyKind(DateTime.ParseExact(r[key],"yyyy-MM-dd",CultureInfo.InvariantCulture),DateTimeKind.Utc);static DateTime? NullableDate(string x)=>string.IsNullOrWhiteSpace(x)?null:DateTime.SpecifyKind(DateTime.ParseExact(x,"yyyy-MM-dd",CultureInfo.InvariantCulture),DateTimeKind.Utc);
}

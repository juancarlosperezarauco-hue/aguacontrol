using AquaControl.Application;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;

namespace AquaControl.Infrastructure;

public partial class GeoService
{
    public sealed record LayerQuality(string Layer, int Count, int? Invalid, int? WrongSrid,
        int? MissingGeometry, int? Linked, int? Unlinked, bool SpatialIndex, double[]? Bounds);
    public sealed class GeoExtentRow
    {
        public int Count { get; set; }
        public int Invalid { get; set; }
        public int WrongSrid { get; set; }
        public int MissingGeometry { get; set; }
        public int Indexed { get; set; }
        public string? Extent { get; set; }
    }

    public async Task<object> Summary(Actor actor)
    {
        actor.Require("geo.read");
        // Opening the map must not run the complete geometric validity audit.
        return await Quality(validate: false);
    }

    private async Task<List<LayerQuality>> Quality(bool validate = true)
    {
        var result = new List<LayerQuality>();
        // Fixed identifiers only. No request value is interpolated into SQL.
        foreach (var (layer, table) in new[] { ("codes", "CodigosFijos"), ("lots", "Lotes"), ("blocks", "Manzanas"), ("roads", "Vias") })
        {
            var checks = validate ? """
                COALESCE(SUM(CASE WHEN Geom.STIsValid()=0 THEN 1 ELSE 0 END),0) AS Invalid,
                COALESCE(SUM(CASE WHEN Geom.STSrid<>4326 THEN 1 ELSE 0 END),0) AS WrongSrid,
                COALESCE(SUM(CASE WHEN Geom IS NULL OR Geom.STIsEmpty()=1 THEN 1 ELSE 0 END),0) AS MissingGeometry,
                """ : "0 AS Invalid, 0 AS WrongSrid, 0 AS MissingGeometry,";
            var sql = $"""
                SELECT COUNT(*) AS [Count],
                    {checks}
                    (SELECT COUNT(*) FROM sys.spatial_indexes WHERE object_id=OBJECT_ID('dbo.{table}') AND is_disabled=0) AS Indexed,
                    geometry::EnvelopeAggregate(Geom).STAsText() AS Extent
                FROM dbo.{table}
                """;
            var row = (await db.Database.SqlQueryRaw<GeoExtentRow>(sql).ToListAsync()).Single();
            int? linked = layer == "codes" ? await db.Set<FixedCode>().CountAsync(x => x.IdLote != null)
                : layer == "lots" ? await db.Set<Lot>().CountAsync(x => x.IdManzana != null) : null;
            var envelope = row.Extent is null ? null : new WKTReader().Read(row.Extent).EnvelopeInternal;
            result.Add(new(layer, row.Count, validate ? row.Invalid : null, validate ? row.WrongSrid : null, validate ? row.MissingGeometry : null,
                linked, linked.HasValue ? row.Count - linked : null, row.Indexed > 0,
                envelope is null ? null : [envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY]));
        }
        return result;
    }

    public sealed record AssociationIssue(string Layer, int Id, int Ordinal, string Reason,
        int? CurrentId, int[] CandidateIds, double Longitude, double Latitude);

    // Read-only audit. Candidate IDs are review information, never automatic corrections.
    public async Task<object> Validate()
    {
        db.Database.SetCommandTimeout(300);
        var quality = await Quality();
        var blocks = await db.Set<Block>().AsNoTracking().ToListAsync();
        var lots = await db.Set<Lot>().AsNoTracking().ToListAsync();
        var codes = await db.Set<FixedCode>().AsNoTracking().ToListAsync();
        var blockTree = new STRtree<Block>();
        var lotTree = new STRtree<Lot>();
        foreach (var block in blocks.Where(x => x.Geom is { IsValid: true, IsEmpty: false }))
            blockTree.Insert(block.Geom!.EnvelopeInternal, block);
        foreach (var lot in lots.Where(x => x.Geom is { IsValid: true, IsEmpty: false }))
            lotTree.Insert(lot.Geom!.EnvelopeInternal, lot);
        var issues = new List<AssociationIssue>();
        var partialLots = new List<int>();
        foreach (var lot in lots.Where(x => x.Geom is { IsValid: true, IsEmpty: false }))
        {
            var point = lot.Geom!.InteriorPoint;
            var candidates = blockTree.Query(point.EnvelopeInternal).Where(x => x.Geom!.Intersects(point)).OrderBy(x => x.Id).ToList();
            Check("lots", lot.Id, lot.Ordinal, lot.IdManzana, candidates.Select(x => x.Id).ToArray(), point);
            var assigned = candidates.FirstOrDefault(x => x.Id == lot.IdManzana);
            if (assigned != null && !assigned.Geom!.Covers(lot.Geom)) partialLots.Add(lot.Id);
        }
        foreach (var code in codes.Where(x => x.Geom is { IsValid: true, IsEmpty: false }))
        {
            var point = code.Geom!.InteriorPoint;
            var candidates = lotTree.Query(code.Geom.EnvelopeInternal).Where(x => x.Geom!.Intersects(code.Geom)).Select(x => x.Id).Order().ToArray();
            Check("codes", code.Id, code.Ordinal, code.IdLote, candidates, point);
        }
        void Check(string layer, int id, int ordinal, int? current, int[] candidates, Point point)
        {
            string? reason = current.HasValue
                ? !candidates.Contains(current.Value) ? "VINCULO_NO_COINCIDE" : candidates.Length > 1 ? "VINCULO_EN_SOLAPE" : null
                : candidates.Length == 0 ? "SIN_COINCIDENCIA" : candidates.Length > 1 ? "MULTIPLES_COINCIDENCIAS" : "VINCULO_UNICO_PENDIENTE";
            if (reason != null) issues.Add(new(layer, id, ordinal, reason, current, candidates, point.X, point.Y));
        }
        return new
        {
            generatedAt = DateTime.UtcNow,
            database = db.Database.GetDbConnection().Database,
            rules = new[] { "Código fijo: intersección con un único lote.", "Lote: punto interior contenido en una única manzana; no certifica cobertura total.", "No se modifican vínculos existentes ni se asigna por proximidad." },
            layers = quality,
            imports = await db.Set<GeoImport>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            importIssues = await db.Set<GeoIssue>().GroupBy(x => new { x.ImportId, x.Reason }).Select(g => new { g.Key.ImportId, g.Key.Reason, count = g.Count() }).ToListAsync(),
            associationCounts = issues.GroupBy(x => new { x.Layer, x.Reason }).Select(g => new { g.Key.Layer, g.Key.Reason, count = g.Count() }),
            partialLotCoverage = new { count = partialLots.Count, lotIds = partialLots },
            operational = new
            {
                clients = await db.Set<Client>().CountAsync(),
                connections = await db.Set<Connection>().CountAsync(),
                connectionsWithFixedCode = await db.Set<Connection>().CountAsync(x => x.FixedCodeId != null),
                contracts = await db.Set<Contract>().CountAsync()
            },
            issues
        };
    }
}

using AquaControl.Application;
using Microsoft.EntityFrameworkCore;

namespace AquaControl.Infrastructure;

public sealed class GeoReference(AquaDb db) : IGeoReference
{
    public async Task<string?> FixedCodeLabel(int id) {
        var code=await db.Set<FixedCode>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);
        return code?.CodFijo?.ToString()??code?.CodF_SQL?.ToString()??code?.CodF_SIG;
    }
    public async Task<int?> FixedCodeId(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var values = await db.Set<FixedCode>().Where(x => x.CodFijo.ToString() == code).Select(x => x.Id).Take(2).ToListAsync();
        return values.Count == 1 ? values[0] : null;
    }
}

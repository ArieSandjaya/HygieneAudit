using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Domain;

// Aturan nilai audit: hanya item mandatori yang dihitung (pembilang maupun penyebut).
// Item opsional boleh dikosongkan atau diisi, tetapi tidak memengaruhi nilai dan tidak perlu follow up.
public static class AuditScoring
{
    public static IEnumerable<AuditItem> Scored(IEnumerable<AuditItem> items)
        => items.Where(i => i.IsMandatory);

    public static (int Total, int Pass, int Fail) Count(IEnumerable<AuditItem> items)
    {
        var scored = Scored(items).ToList();
        return (scored.Count,
                scored.Count(i => i.Status == AuditItemStatus.Pass),
                scored.Count(i => i.Status == AuditItemStatus.Fail));
    }

    public static double Rate(int pass, int total, int digits = 0)
        => total > 0 ? Math.Round((double)pass / total * 100, digits) : 0;
}

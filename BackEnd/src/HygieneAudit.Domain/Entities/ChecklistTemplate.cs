namespace HygieneAudit.Domain.Entities;

public class ChecklistTemplate
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool RequiresGas { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

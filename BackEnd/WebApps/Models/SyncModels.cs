using System.Collections.Generic;

namespace WebApps.Models
{
    public class SyncUpdateItemPayload
    {
        public string AuditId { get; set; }
        public int TemplateId { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public List<string> Photos { get; set; }
    }

    public class SyncDraftPayload
    {
        public string Id { get; set; }
    }
}

namespace HygieneAudit.Application.DTOs;

// ── Users ────────────────────────────────────────────────────────────────────

public class UserResponse
{
    public int    Id        { get; set; }
    public string Username  { get; set; } = string.Empty;
    public string Name      { get; set; } = string.Empty;
    public string Role      { get; set; } = string.Empty;
    public bool   IsActive  { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;
    public string Role     { get; set; } = "Auditor";   // "Auditor" | "Admin"
}

public class UpdateUserRequest
{
    public string? Name     { get; set; }
    public string? Role     { get; set; }
    public string? Password { get; set; }   // null = keep existing
    public bool?   IsActive { get; set; }
}

// ── Templates ────────────────────────────────────────────────────────────────

public class TemplateResponse
{
    public int    Id           { get; set; }
    public string Category     { get; set; } = string.Empty;
    public string Name         { get; set; } = string.Empty;
    public bool   RequiresGas  { get; set; }
    public int    DisplayOrder { get; set; }
    public bool   IsActive     { get; set; }
}

public class CreateTemplateRequest
{
    public string Category     { get; set; } = string.Empty;
    public string Name         { get; set; } = string.Empty;
    public bool   RequiresGas  { get; set; }
    public int    DisplayOrder { get; set; }
}

public class UpdateTemplateRequest
{
    public string? Category     { get; set; }
    public string? Name         { get; set; }
    public bool?   RequiresGas  { get; set; }
    public int?    DisplayOrder { get; set; }
    public bool?   IsActive     { get; set; }
}

// ── Tenants ──────────────────────────────────────────────────────────────────

public class CreateTenantRequest
{
    public string  Name     { get; set; } = string.Empty;
    public bool    UsesGas  { get; set; }
    public string? Floor    { get; set; }
    public string? Category { get; set; }
}

public class UpdateTenantRequest
{
    public string? Name     { get; set; }
    public bool?   UsesGas  { get; set; }
    public string? Floor    { get; set; }
    public string? Category { get; set; }
    public bool?   IsActive { get; set; }
}

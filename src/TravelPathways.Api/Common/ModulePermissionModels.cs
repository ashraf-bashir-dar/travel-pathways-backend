namespace TravelPathways.Api.Common;

/// <summary>Per-module permission grant stored on the user.</summary>
public sealed class ModulePermissionGrant
{
    public AppModuleKey Module { get; set; }
    public bool View { get; set; } = true;
    public bool Create { get; set; } = true;
    public bool Edit { get; set; } = true;
    public bool Delete { get; set; } = true;
    public ModuleDataScope DataScope { get; set; } = ModuleDataScope.Own;
}

/// <summary>API representation of a module permission grant.</summary>
public sealed class ModulePermissionGrantDto
{
    public AppModuleKey Module { get; set; }
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
    public bool Delete { get; set; }
    public ModuleDataScope DataScope { get; set; }
}

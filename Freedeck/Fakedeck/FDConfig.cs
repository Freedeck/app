using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace Freedeck.Fakedeck;

public class FdConfig
{
	public bool writeLogs { get; set; } = false;
	public string release { get; set; } = "stable";
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public Dictionary<string, List<Dictionary<string, FdcTile>>> profiles { get; set; } 
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public string theme { get; set; } = "default";
    public string profile { get; set; } = "Default";
    public int screenSaverActivationTime { get; set; } = 5;
    public bool soundOnPress { get; set; } = false;
    public bool useAuthentication { get; set; } = false;
    public int iconCountPerPage { get; set; } = 12;
    public int port { get; set; } = 5754;
}
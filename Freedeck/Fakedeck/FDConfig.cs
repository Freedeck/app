using System.Collections.Generic;

namespace Freedeck.Fakedeck;

public class FdConfig
{
	public bool writeLogs { get; set; } = false;
    public string release { get; set; } = "stable";
    public Dictionary<string, List<Dictionary<string, FdcTile>>> profiles { get; set; } 
    public string theme { get; set; } = "default";
    public string profile { get; set; } = "Default";
    public int screenSaverActivationTime { get; set; } = 5;
    public bool soundOnPress { get; set; } = false;
    public bool useAuthentication { get; set; } = false;
    public int iconCountPerPage { get; set; } = 12;
    public int port { get; set; } = 5754;
}
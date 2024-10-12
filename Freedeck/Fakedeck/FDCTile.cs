using System.Collections.Generic;

namespace Freedeck.Fakedeck;

public class FdcTile
{
    public string type { get; set; }
    public int pos { get; set; }
    public string uuid { get; set; }
    public Dictionary<string, object> data { get; set; } = new Dictionary<string, object>();
}
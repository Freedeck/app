using System.Collections.Generic;

namespace Freedeck.Fakedeck;

public class TileBuilder
{
    private FdcTile tile = new FdcTile();

    public TileBuilder SetType(string type)
    {
        tile.type = type;
        return this;
    }

    public TileBuilder SetPosition(int pos)
    {
        tile.pos = pos;
        return this;
    }

    public TileBuilder SetUUID(string uuid)
    {
        tile.uuid = uuid;
        return this;
    }

    public TileBuilder AddData(string key, object value)
    {
        if (tile.data == null)
        {
            tile.data = new Dictionary<string, object>();
        }
        tile.data[key] = value;
        return this;
    }

    public FdcTile Build()
    {
        return tile;
    }
}

public class ProfileBuilder
{
    private List<Dictionary<string, FdcTile>> profileEntries = new List<Dictionary<string, FdcTile>>();

    public ProfileBuilder AddEntry(string key, FdcTile tile)
    {
        var entry = new Dictionary<string, FdcTile> { { key, tile } };
        profileEntries.Add(entry);  // Add a dictionary with the key and Tile object
        return this;
    }

    public List<Dictionary<string, FdcTile>> Build()
    {
        return profileEntries;
    }
}


public class ConfigBuilder
{
    private FdConfig config = new FdConfig();

    public ConfigBuilder SetWriteLogs(bool writeLogs)
    {
        config.writeLogs = writeLogs;
        return this;
    }

    public ConfigBuilder SetRelease(string release)
    {
        config.release = release;
        return this;
    }

    public ConfigBuilder SetTheme(string theme)
    {
        config.theme = theme;
        return this;
    }

    public ConfigBuilder SetActiveProfile(string profile)
    {
        config.profile = profile;
        return this;
    }

    public ConfigBuilder SetScreenSaverActivationTime(int time)
    {
        config.screenSaverActivationTime = time;
        return this;
    }

    public ConfigBuilder SetSoundOnPress(bool soundOnPress)
    {
        config.soundOnPress = soundOnPress;
        return this;
    }

    public ConfigBuilder SetUseAuthentication(bool useAuthentication)
    {
        config.useAuthentication = useAuthentication;
        return this;
    }

    public ConfigBuilder SetIconCountPerPage(int iconCount)
    {
        config.iconCountPerPage = iconCount;
        return this;
    }

    public ConfigBuilder SetPort(int port)
    {
        config.port = port;
        return this;
    }

    public ConfigBuilder AddProfile(string profileName, List<Dictionary<string, FdcTile>> profileEntries)
    {
        if (config.profiles == null)
        {
            config.profiles = new Dictionary<string, List<Dictionary<string, FdcTile>>>();
        }
        config.profiles[profileName] = profileEntries;
        return this;
    }
    
    public FdConfig Build()
    {
        return config;
    }
}
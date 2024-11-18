using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Masasas;
public class UserPreferences
{
    public struct PresetValue
    {
        public required double Value { get; set; }
        public required string Unit { get; set; }
        public string? Name { get; set; } = null;

        [SetsRequiredMembers]
        public PresetValue(double value, string unit)
        {
            Value = value;
            Unit = unit;
        }

        public PresetValue() { }
    }

    required public string Name { get; set; }
    required public List<PresetValue> HeightPresets { get; set; } = [];

    public UserPreferences() { }

    [SetsRequiredMembers]
    public UserPreferences(string name)
    {
        Name = name;
    }
}

class User
{
    required public string PasswordHashed { get; set; }
    required public string CreationDate { get; set; }
    public string? Alias { get; set; } = null;

    public bool Administrator { get; set; } = false;
    public bool AllowedPersonalization { get; set; } = true;
    public bool AllowedSelfDeletion { get; set; } = true;
    required public UserPreferences? Preferences { get; set; }

    [JsonIgnore]
    public string DailyAccessCodeYesterday => Utils.Hash(PasswordHashed + DateTime.UtcNow.AddDays(-1).Day + DateTime.UtcNow.AddDays(-1).Year);
    [JsonIgnore]
    public string DailyAccessCode => Utils.Hash(PasswordHashed + DateTime.UtcNow.Day + DateTime.UtcNow.Year);

    public User() { }

    [SetsRequiredMembers]
    public User(string password, UserPreferences? preferences)
    {
        CreationDate = DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
        PasswordHashed = Utils.Hash(password + CreationDate);
        Preferences = preferences;
    }
}

class UnsecuredUser
{
    required public string PasswordRSA { get; set; }
    required public UserPreferences Preferences { get; set; }

    public string? Alias { get; set; } = null;
    public bool Administrator { get; set; } = false;
    public bool AllowedPersonalization { get; set; } = true;
    public bool AllowedSelfDeletion { get; set; } = true;

    public UnsecuredUser() { }

    [SetsRequiredMembers]
    public UnsecuredUser(string passwordRSA, UserPreferences preferences)
    {
        PasswordRSA = passwordRSA;
        Preferences = preferences;
    }

    public static implicit operator User(UnsecuredUser user)
    {
        if (user.Alias != null)
            return new(Utils.DecryptFromHex(user.PasswordRSA), null)
            {
                Alias = user.Alias
            };

        return new(Utils.DecryptFromHex(user.PasswordRSA), user.Preferences)
        {
            Administrator = user.Administrator,
            AllowedPersonalization = user.AllowedPersonalization,
            AllowedSelfDeletion = user.AllowedSelfDeletion,
        };
    }
}

class TableData
{
    private double currentHeight;

    required public string Location { get; set; }
    required public string MacAddress { get; set; }
    required public string Manufacturer { get; set; }
    required public double MinHeight { get; set; }
    required public double MaxHeight { get; set; }
    public double CurrentHeight
    {
        get => Math.Clamp(currentHeight, MinHeight, MaxHeight);

        set => currentHeight = Math.Clamp(value, MinHeight, MaxHeight);
    }
    public string Icon { get; set; } = "table";

    public TableData() { }

    [SetsRequiredMembers]
    public TableData(string macAddress, string manufacturer, double minHeight, double maxHeight, string location)
    {
        Location = location;
        MacAddress = macAddress;
        Manufacturer = manufacturer;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
        CurrentHeight = minHeight;
    }
}


class UnsecuredTable
{
    required public TableData Data { get; set; }

    public UnsecuredTable() { }

    [SetsRequiredMembers]
    public UnsecuredTable(TableData data)
    {
        Data = data;
    }
}

class Table
{
    required public string BaseAccessCode { get; set; } = Convert.ToHexString(Guid.NewGuid().ToByteArray());
    [JsonIgnore]
    public string DailyAccessCodeYesterday => Utils.Hash(BaseAccessCode + DateTime.UtcNow.AddDays(-1).Day + DateTime.UtcNow.AddDays(-1).Year);
    [JsonIgnore]
    public string DailyAccessCode => Utils.Hash(BaseAccessCode + DateTime.UtcNow.Day + DateTime.UtcNow.Year);
    required public TableData Data { get; set; }

    public Table() { }

    [SetsRequiredMembers]
    public Table(TableData data)
    {
        Data = data;
    }

    public static implicit operator Table(UnsecuredTable table)
    {
        return new(table.Data);
    }
}

[method: SetsRequiredMembers]
class Config()
{
    required public bool GuestWarning { get; set; } = true;
    required public bool UserSelfDeletion { get; set; } = true;
    required public bool UserPersonalization { get; set; } = true;
    required public double? ConfigReloadPeriodSeconds { get; set; } = 5;

    // depending on how we interact with the tables, these might be needed
    // required public string TableAPIScheme { get; set; } = "http";
    // required public string TableAPIHostname { get; set; } = "localhost";
    // required public int TableAPIPort { get; set; } = 8080;

    [JsonIgnore]
    public TimeSpan ConfigReloadPeriod
    {
        get
        {
            if (ConfigReloadPeriodSeconds != null)
                ConfigReloadPeriodSeconds = Math.Max((double)ConfigReloadPeriodSeconds, 0.1);

            return ConfigReloadPeriodSeconds != null
                ? TimeSpan.FromSeconds(ConfigReloadPeriodSeconds ?? throw new("HOW???"))
                : Timeout.InfiniteTimeSpan;
        }
    }
}

class HttpResponse(string content, string contentType = "text/plain", int statusCode = 200) : IResult
{
    public readonly string Content = content;
    public readonly string ContentType = contentType;
    public readonly int StatusCode = statusCode;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCode;
        httpContext.Response.ContentType = ContentType;
        return httpContext.Response.WriteAsync(Content);
    }
}
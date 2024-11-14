using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Masasas;
public class UserPreferences
{
    public struct HeightValue
    {
        public required double Value { get; set; }
        public required string Unit { get; set; }

        [SetsRequiredMembers]
        public HeightValue(double value, string unit)
        {
            Value = value;
            Unit = unit;
        }

        public HeightValue() { }
    }
    required public string Name { get; set; }
    required public List<HeightValue> HeightPresets { get; set; } = [];

    public UserPreferences() { }

    [SetsRequiredMembers]
    public UserPreferences(string name)
    {
        Name = name;
    }
}

class User
{
    required public string PasswordEncrypted { get; set; }
    [JsonIgnore]
    public string DailyAccessCodeYesterday => Utils.Encrypt(PasswordEncrypted + DateTime.UtcNow.AddDays(-1).Day + DateTime.UtcNow.AddDays(-1).Year);
    [JsonIgnore]
    public string DailyAccessCode => Utils.Encrypt(PasswordEncrypted + DateTime.UtcNow.Day + DateTime.UtcNow.Year);
    required public bool Administrator { get; set; }
    required public bool AllowedPersonalization { get; set; } = true;
    required public UserPreferences Preferences { get; set; }

    public User() { }

    [SetsRequiredMembers]
    public User(string accessCode, bool administrator, bool allowedPersonalization, UserPreferences userPreferences)
    {
        PasswordEncrypted = Utils.Encrypt(accessCode);
        Administrator = administrator;
        AllowedPersonalization = allowedPersonalization;
        Preferences = userPreferences;
    }
}

class UnsecuredUser
{
    required public string Password { get; set; }
    required public bool Administrator { get; set; }
    required public bool AllowedPersonalization { get; set; }

    required public UserPreferences Preferences { get; set; }

    public UnsecuredUser() { }

    [SetsRequiredMembers]
    public UnsecuredUser(string password, bool administrator, bool allowedPersonalization, UserPreferences userPreferences)
    {
        Password = password;
        Administrator = administrator;
        AllowedPersonalization = allowedPersonalization;
        Preferences = userPreferences;
    }

    public static implicit operator User(UnsecuredUser user)
    {
        return new(user.Password, user.Administrator, user.AllowedPersonalization, user.Preferences);
    }
}

class TableData
{
    required public string Location { get; set; }
    required public string MacAddress { get; set; }
    required public string Manufacturer { get; set; }
    required public double MaxHeight { get; set; }
    required public double MinHeight { get; set; }
    required public double CurrentHeight { get; set; }
    required public string Icon { get; set; }

    public TableData() { }

    [SetsRequiredMembers]
    public TableData(string macAddress, string manufacturer, double minHeight, double maxHeight, string location)
    {
        Location = location;
        MacAddress = macAddress;
        Manufacturer = manufacturer;
        MaxHeight = maxHeight;
        MinHeight = minHeight;
        CurrentHeight = minHeight;
        Icon = "table";
    }

    public void SetHeight(double height)
    {
        CurrentHeight = Math.Clamp(height, MinHeight, MaxHeight);
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
    public string DailyAccessCodeYesterday => Utils.Encrypt(BaseAccessCode + DateTime.UtcNow.AddDays(-1).Day + DateTime.UtcNow.AddDays(-1).Year);
    [JsonIgnore]
    public string DailyAccessCode => Utils.Encrypt(BaseAccessCode + DateTime.UtcNow.Day + DateTime.UtcNow.Year);
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

class Config
{
    required public bool GuestWarning { get; set; } = true;
    required public bool UserSelfDeletion { get; set; } = true;
    required public bool UserPersonalization { get; set; } = true;
    required public double? ConfigReloadPeriodSeconds { get; set; } = 5;
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

    [SetsRequiredMembers]
    public Config() { }
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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Masasas;

partial class Program
{
    static Config config = new();
    static ConcurrentDictionary<string, User> users = [];
    static ConcurrentDictionary<string, Table> tables = [];

    static bool configAccess = false;
    static bool configChanged = false;
    static bool usersAccess = false;
    static bool tablesAccess = false;
    static Timer? configWatcher;

    static void UpdateConfigWatcher()
    {
        configWatcher?.Dispose();
        configWatcher = new((_) =>
       {
           if (configChanged)
           {
               configChanged = false;
               return;
           }
           if (DateTime.Now - File.GetLastWriteTime("config.json") < config.ConfigReloadPeriod.Add(TimeSpan.FromMilliseconds(10)))
               try
               {
                   config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json")) ?? throw new();
                   Console.WriteLine($"Reloaded config");
                   UpdateConfigWatcher();
               }
               catch
               {
                   Console.WriteLine("Config was invalid on update");
               }
       }, null, config.ConfigReloadPeriod, config.ConfigReloadPeriod);
    }


    static async void SaveUsers()
    {
        if (!usersAccess)
        {
            usersAccess = true;
            await File.WriteAllTextAsync("users.json", JsonSerializer.Serialize(users, Utils.JsonOptions));
            usersAccess = false;
            Console.WriteLine("Users saved");
        }
    }

    static async void SaveTables()
    {
        if (!tablesAccess)
        {
            tablesAccess = true;
            await File.WriteAllTextAsync("tables.json", JsonSerializer.Serialize(tables, Utils.JsonOptions));
            tablesAccess = false;
            Console.WriteLine("Tables saved");
        }
    }

    static async void SaveConfig()
    {
        if (!configAccess)
        {
            configAccess = true;
            await File.WriteAllTextAsync("config.json", JsonSerializer.Serialize(config, Utils.JsonOptions));
            configAccess = false;
            configChanged = true;
            Console.WriteLine("Config saved");
        }
    }

    static void LoadUsers()
    {
        users = File.Exists("users.json")
        ? JsonSerializer.Deserialize<ConcurrentDictionary<string, User>>(File.ReadAllText("users.json")) ?? []
        : [];
        Console.WriteLine("State loaded or initialized");
    }

    static void LoadTables()
    {
        tables = File.Exists("tables.json")
        ? JsonSerializer.Deserialize<ConcurrentDictionary<string, Table>>(File.ReadAllText("tables.json")) ?? []
        : [];
        Console.WriteLine("State loaded or initialized");
    }

    static void LoadConfig()
    {
        config = File.Exists("config.json")
        ? JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json")) ?? new()
        : new();
    }


    public static void Main(string[] args)
    {
        LoadConfig();
        LoadUsers();
        LoadTables();

        if (users.IsEmpty)
            users["guest"] = Data.GuestUser;

        SaveConfig();
        SaveUsers();
        SaveTables();

        UpdateConfigWatcher();

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();
        app.UseHttpsRedirection();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }


        var routes = app.MapGroup("/");
        routes.MapGet("/rsa", () => Utils.OkText(Utils.RSA.ExportRSAPublicKeyPem()));
        routes.MapGet("/user/{id}/{passwordRSA}", UserCodeGet);
        routes.MapGet("/user/{id}/{accessCode}/{command}", UserRouteGet);
        routes.MapPost("/user/{id}/{accessCode}/{command}", UserRoutePost);
        routes.MapGet("/table/{id}/{accessCode}/{command}", TableRouteGet);
        routes.MapPost("/table/{id}/{accessCode}/{command}", TableRoutePost);
        routes.MapGet("/admin/{id}/{accessCode}/{command}", AdminRouteGet);
        routes.MapGet("/admin/{id}/{accessCode}/{command}/{commandValue}", AdminRouteGetWithValue);
        routes.MapPost("/admin/{id}/{accessCode}/{command}", AdminRoutePost);
        routes.MapPost("/admin/{id}/{accessCode}/{command}/{commandValue}", AdminRoutePostWithCommandValue);
        routes.MapGet("/", () => config.GuestWarning ? Data.RootWithWarning : Data.Root);
        app.Run();

        configWatcher?.Dispose();
        configWatcher = null;

        SaveConfig();
        SaveUsers();
        SaveTables();
    }
}

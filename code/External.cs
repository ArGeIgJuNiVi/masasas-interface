using System.Text.Json;

namespace Masasas;

partial class Program
{
    class ExternalAPIs
    {
        public class Kr64Api
        {
#pragma warning disable IDE1006
            private class ApiTable
            {
                public required Config config { get; set; }
                public required State state { get; set; }
                public required Usage usage { get; set; }
                public required List<LastErrors> lastErrors { get; set; }

                public class Config
                {
                    public required string name { get; set; }
                    public required string manufacturer { get; set; }
                };
                public class State
                {
                    public required int position_mm { get; set; }
                    public required int speed_mms { get; set; }
                    public required string status { get; set; }
                    public required bool isPositionLost { get; set; }
                    public required bool isOverloadProtectionUp { get; set; }
                    public required bool isOverloadProtectionDown { get; set; }
                    public required bool isAntiCollision { get; set; }
                };
                public class Usage
                {
                    public required int activationsCounter { get; set; }
                    public required int sitStandCounter { get; set; }
                };
                public class LastErrors
                {
                    public static int time_s { get; set; }
                    public static int errorCode { get; set; }
                };
            }
#pragma warning restore IDE1006

            public static async Task Run(string tableID)
            {
                string? json = null;
                try
                {
                    using HttpClient externalClient = new() { BaseAddress = new(tables[tableID].Data.Api!.Url) };
                    // if table data was set recently, update the external value, otherwise update the local 
                    if (tables[tableID].Data.SetRecently)
                    {
                        tables[tableID].Data.SetRecently = false;
                        int position_mm = (int)(tables[tableID].Data.CurrentHeight * 1000);
                        json = await (await externalClient.PutAsync(
                            $"/api/v2/{tables[tableID].Data.Api!.Key}/desks/{tables[tableID].Data.MacAddress.ToLowerInvariant()}/state",
                            new StringContent($"{{\"position_mm\": {position_mm}}}")
                        )).Content.ReadAsStringAsync();
                    }
                    else
                    {
                        json = await (await externalClient.GetAsync(
                            $"/api/v2/{tables[tableID].Data.Api!.Key}/desks/{tables[tableID].Data.MacAddress.ToLowerInvariant()}"
                        )).Content.ReadAsStringAsync();

                        ApiTable desk = JsonSerializer.Deserialize<ApiTable>(json)!;

                        // do not correct existing table height until the table stops
                        if (desk.state.speed_mms == 0)
                        {
                            tables[tableID].Data.CurrentHeight = desk.state.position_mm / 1000.0;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(json ?? e.Message);
                }

            }

            public static async Task<bool> ImportTables(string url, string key)
            {
                try
                {
                    HttpClient externalClient = new() { BaseAddress = new(url), Timeout = TimeSpan.FromSeconds(1), };
                    string json = await (await externalClient.GetAsync(
                            $"/api/v2/{key}/desks/"
                        )).Content.ReadAsStringAsync();

                    var apiMacs = JsonSerializer.Deserialize<List<string>>(json)!;

                    foreach (string macAddress in apiMacs)
                    {
                        json = await (await externalClient.GetAsync(
                            $"/api/v2/{key}/desks/{macAddress}"
                        )).Content.ReadAsStringAsync();
                        var table = JsonSerializer.Deserialize<ApiTable>(json)!;
                        tables[macAddress] = new(new(
                            macAddress,
                            "api",
                            table.config.manufacturer,
                            0.68,
                            1.32,
                            table.config.name
                        )
                        {
                            CurrentHeight = table.state.position_mm / 1000.0,
                            Api = new(url, key, "Kr64"),
                        });
                    }
                    SaveTables();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return false;
                }
            }
        }
    }

    static Timer? apiCaller;

    static async void ExternalAPICaller(object? _)
    {
        foreach ((string id, object _) in tables)
        {
            //only interact with api tables
            if (tables[id].Data.ConnectionMode != "api")
                continue;
            switch (tables[id].Data.Api!.Type.ToLowerInvariant())
            {
                case "kr64":
                    await ExternalAPIs.Kr64Api.Run(id);
                    break;
                case "dummy":
                default:
                    Console.WriteLine($"dummy api run called ({tables[id].Data.Api!.Type})");
                    break;
            };

        }
    }

    static async Task<bool> ImportTablesFromApi(TableData.ApiData data)
    {
        switch (data.Type.ToLowerInvariant())
        {
            case "kr64":
                return await ExternalAPIs.Kr64Api.ImportTables(data.Url, data.Key);

            case "dummy":
            default:
                Console.WriteLine($"dummy api import called ({data.Type.ToLowerInvariant()})");
                return false;
        };
    }

    static void UpdateExternalAPI()
    {
        apiCaller?.Dispose();
        apiCaller = new(ExternalAPICaller, null, config.ExternalAPIRequestFrequency, config.ExternalAPIRequestFrequency);
    }
}
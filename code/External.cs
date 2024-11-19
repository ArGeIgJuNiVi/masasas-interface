using System.Text.Json;

namespace Masasas;

partial class Program
{
    interface IExternalApi
    {
        Task Run();

        Task<bool> ImportTables();
    }
    class ExternalAPIs
    {
        public class DummyApi : IExternalApi
        {
            public Task<bool> ImportTables()
            {
                return new(() => true);
            }

            public Task Run()
            {
                return new(() => { });
            }
        }

        public class Kr64Api : IExternalApi
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

            public async Task Run()
            {
                string? json = null;
                try
                {
                    foreach (string id in tables.Keys)
                    {
                        using HttpClient externalClient = new() { BaseAddress = new Uri(config.ExternalAPIUrl) };

                        // if table data was set recently, update the external value, otherwise update the local 
                        if (tables[id].Data.SetRecently)
                        {
                            tables[id].Data.SetRecently = false;
                            int position_mm = (int)(tables[id].Data.CurrentHeight * 1000);
                            json = await (await externalClient.PutAsync(
                                $"/api/v2/{config.ExternalAPIKey}/desks/{tables[id].Data.MacAddress.ToLowerInvariant()}/state",
                                new StringContent($"{{\"position_mm\": {position_mm}}}")
                            )).Content.ReadAsStringAsync();
                        }
                        else
                        {
                            json = await (await externalClient.GetAsync(
                                $"/api/v2/{config.ExternalAPIKey}/desks/{tables[id].Data.MacAddress.ToLowerInvariant()}"
                            )).Content.ReadAsStringAsync();

                            ApiTable desk = JsonSerializer.Deserialize<ApiTable>(json)!;

                            // do not correct existing table height until the table stops
                            if (desk.state.speed_mms == 0)
                            {
                                tables[id].Data.CurrentHeight = desk.state.position_mm / 1000.0;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(json ?? e.Message);
                }
            }

            public async Task<bool> ImportTables()
            {
                try
                {
                    using HttpClient externalClient = new() { BaseAddress = new Uri(config.ExternalAPIUrl) };

                    string json = await (await externalClient.GetAsync(
                            $"/api/v2/{config.ExternalAPIKey}/desks/"
                        )).Content.ReadAsStringAsync();

                    var apiMacs = JsonSerializer.Deserialize<List<string>>(json)!;

                    foreach (string macAddress in apiMacs)
                    {
                        json = await (await externalClient.GetAsync(
                            $"/api/v2/{config.ExternalAPIKey}/desks/{macAddress}"
                        )).Content.ReadAsStringAsync();
                        var table = JsonSerializer.Deserialize<ApiTable>(json)!;
                        tables[macAddress] = new(new(
                            macAddress,
                            table.config.manufacturer,
                            0.68,
                            1.32,
                            table.config.name
                        )
                        {
                            CurrentHeight = table.state.position_mm / 1000.0
                        });
                    }
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

    static IExternalApi api = new ExternalAPIs.DummyApi();

    static void ExternalAPICaller(object? _)
    {
        api.Run();
    }

    static void UpdateExternalAPI()
    {
        api = config.ExternalAPIType.ToLowerInvariant() switch
        {
            "kr64" => new ExternalAPIs.Kr64Api(),
            "dummy" or _ => new ExternalAPIs.DummyApi(),
        };
        apiCaller?.Dispose();
        apiCaller = new(ExternalAPICaller, null, config.ExternalAPIRequestFrequency, config.ExternalAPIRequestFrequency);
    }
}
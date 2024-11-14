using System.Globalization;
using System.Text.Json;

namespace Masasas;

partial class Program
{
    static HttpResponse UserCodeGet(string id, string password)
    {
        if (!users.TryGetValue(id, out User? user) || user.PasswordEncrypted != Utils.Encrypt(password))
        {
            return Utils.BadRequestText("Invalid user id or password");
        }

        return Utils.OkText(user.DailyAccessCode);
    }

    static HttpResponse UserRouteGet(string id, string accessCode, string command)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid user id or daily access code");
        }

        switch (command)
        {
            case "get_preferences":
                return Utils.OkJson(users[id].Preferences);

            case "get_personalization_state":
                return Utils.OkText((config.UserPersonalization && users[id].AllowedPersonalization).ToString());

            case "get_tables":
                return Utils.OkJson(tables.Select((val) => new { ID = val.Key, val.Value.DailyAccessCode, val.Value.Data }));

            case "delete_user":
                if (!config.UserSelfDeletion)
                    return Utils.BadRequestText("User self deletion is disabled");

                if (user.Administrator && users.Count((val) => val.Value.Administrator) == 1)
                    return Utils.BadRequestText("Cannot delete the last administrator");

                return Utils.OkJson(users[id].Preferences);

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    async static Task<HttpResponse> UserRoutePost(string id, string accessCode, string command, Stream body)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid user id or daily access code");
        }

        switch (command)
        {
            case "set_preferences":
                string? bodyText = null;

                if (!config.UserPersonalization || !user.AllowedPersonalization)
                    return Utils.BadRequestText("User personalization is disabled");

                try
                {
                    bodyText = await new StreamReader(body).ReadToEndAsync();
                    users[id].Preferences = JsonSerializer.Deserialize<UserPreferences>(bodyText) ?? throw new();
                    SaveUsers();
                    return Utils.OkJson(users[id].Preferences);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }

                return Utils.BadRequestText($"""
                Invalid user preferences:
                {bodyText}
                Correct format:
                {JsonSerializer.Serialize(Data.GuestUser.Preferences, Utils.JsonOptions)}
                """);

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static HttpResponse TableRouteGet(string id, string accessCode, string command)
    {
        if (
            tables.TryGetValue(id, out Table? table)
            && (table.DailyAccessCodeYesterday == accessCode || table.DailyAccessCode == accessCode)
        )
        {
            return command switch
            {
                "get_data" => Utils.OkJson(tables[id].Data),
                _ => Utils.BadRequestText("Unknown command"),
            };
        }

        return Utils.BadRequestText("Invalid table id or daily access code");
    }

    static async Task<HttpResponse> TableRoutePost(string id, string accessCode, string command, Stream body)
    {
        if (!tables.TryGetValue(id, out Table? table) || table.DailyAccessCodeYesterday != accessCode && table.DailyAccessCode != accessCode)
        {
            return Utils.BadRequestText("Invalid table id or daily access code");
        }

        switch (command)
        {
            case "set_height":
                try
                {
                    tables[id].Data.SetHeight(Convert.ToDouble(await new StreamReader(body).ReadToEndAsync(), CultureInfo.InvariantCulture));
                    SaveTables();
                    return Utils.OkJson(tables[id].Data.CurrentHeight);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid table height, should be a double in meters");

            case "set_height_percentage":
                try
                {
                    tables[id].Data.SetHeight(tables[id].Data.MinHeight + Convert.ToDouble(await new StreamReader(body).ReadToEndAsync(), CultureInfo.InvariantCulture) * (tables[id].Data.MaxHeight - tables[id].Data.MinHeight));
                    SaveTables();
                    return Utils.OkJson((tables[id].Data.CurrentHeight - tables[id].Data.MinHeight) / (tables[id].Data.MaxHeight - tables[id].Data.MinHeight));
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid table height percentage, should be a double between 0 and 1");

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static HttpResponse AdminRouteGet(string id, string accessCode, string command)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");
        }

        if (!user.Administrator)
            return new HttpResponse("Unauthorized user", "text/plain", 401);

        switch (command)
        {
            case "get_users":
                return Utils.OkJson(users.Select((val) => new { ID = val.Key, val.Value.Preferences }));

            case "disable_guest_warning":
                config.GuestWarning = false;
                SaveConfig();
                return Utils.OkText("Warning disabled");

            case "enable_user_self_deletion":
                config.UserSelfDeletion = true;
                SaveConfig();
                return Utils.OkText("User self deletion enabled");

            case "disable_user_self_deletion":
                config.UserSelfDeletion = false;
                SaveConfig();
                return Utils.OkText("User self deletion disabled");

            case "enable_user_personalization":
                config.UserPersonalization = true;
                SaveConfig();
                return Utils.OkText("User personalization disabled");

            case "disable_user_personalization":
                config.UserPersonalization = false;
                SaveConfig();
                return Utils.OkText("User personalization disabled");

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static HttpResponse AdminRouteGetWithValue(string id, string accessCode, string command, string commandValue)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");

        }

        if (!user.Administrator)
            return new HttpResponse("Unauthorized user", "text/plain", 401);

        switch (command)
        {
            case "delete_user":
                if (commandValue == id && users.Count((val) => val.Value.Administrator) == 1)
                    return Utils.BadRequestText("Cannot delete last administrator");

                if (!users.TryRemove(commandValue, out _))
                    return Utils.BadRequestText("User does not exist");

                if (commandValue == "guest")
                    config.GuestWarning = false;

                SaveUsers();
                return Utils.OkText($"Deleted user {commandValue}");

            case "delete_table":
                if (!tables.TryRemove(commandValue, out _))
                    return Utils.BadRequestText("Table does not exist");

                SaveTables();
                return Utils.OkText($"Deleted table {commandValue}");

            case "enable_user_personalization":
                {
                    if (!users.TryGetValue(commandValue, out User? commandUser))
                        return Utils.BadRequestText("User does not exist");
                    commandUser.AllowedPersonalization = true;
                    SaveConfig();
                    return Utils.OkText($"User personalization enabled for {commandValue}");
                }

            case "disable_user_personalization":
                {
                    if (!users.TryGetValue(commandValue, out User? commandUser))
                        return Utils.BadRequestText("User does not exist");
                    commandUser.AllowedPersonalization = false;
                    SaveConfig();
                    return Utils.OkText($"User personalization disabled for {commandValue}");
                }

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static async Task<HttpResponse> AdminRoutePost(string id, string accessCode, string command, Stream body)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");
        }

        if (!user.Administrator)
            return new HttpResponse("Unauthorized user", "text/plain", 401);

        switch (command)
        {
            case "set_config_reload_seconds":
                try
                {
                    string? bodyText = await new StreamReader(body).ReadToEndAsync();
                    if (bodyText == "null" || string.IsNullOrWhiteSpace(bodyText))
                        config.ConfigReloadPeriodSeconds = null;
                    else
                        config.ConfigReloadPeriodSeconds = Convert.ToDouble(bodyText, CultureInfo.InvariantCulture);
                    UpdateConfigWatcher();
                    SaveConfig();
                    return Utils.OkJson(config.ConfigReloadPeriodSeconds);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid config reload time, should be a double in seconds, or null to disable reloading");
            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static async Task<HttpResponse> AdminRoutePostWithCommandValue(string id, string accessCode, string command, string commandValue, Stream body)
    {
        if (!users.TryGetValue(id, out User? user) || (user.DailyAccessCode != accessCode && user.DailyAccessCodeYesterday != accessCode))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");
        }

        if (!user.Administrator)
            return new HttpResponse("Unauthorized user", "text/plain", 401);

        string? bodyText = null;
        switch (command)
        {
            case "create_user":
                if (commandValue == id && users.Count((val) => val.Value.Administrator) == 1)
                    return Utils.BadRequestText("Cannot edit the last administrator");
                try
                {
                    bodyText = await new StreamReader(body).ReadToEndAsync();
                    UnsecuredUser unencryptedUser = JsonSerializer.Deserialize<UnsecuredUser>(bodyText) ?? throw new();
                    users[commandValue] = unencryptedUser;
                    SaveUsers();
                    return Utils.OkJson(unencryptedUser);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText($"""
                Invalid user:
                {bodyText}
                Correct format:
                {JsonSerializer.Serialize(Data.GuestUser, Utils.JsonOptions)}
                """);
            case "create_table":
                try
                {
                    bodyText = await new StreamReader(body).ReadToEndAsync();
                    UnsecuredTable table = JsonSerializer.Deserialize<UnsecuredTable>(bodyText) ?? throw new();
                    tables[commandValue] = table;
                    SaveTables();
                    return Utils.OkJson(table);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText($"""
                Invalid table:
                {bodyText}
                Correct format:
                {JsonSerializer.Serialize(Data.NewTable, Utils.JsonOptions)}
                """);
            default:
                return Utils.BadRequestText("Unknown command");
        };
    }
}

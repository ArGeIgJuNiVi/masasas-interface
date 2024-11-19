using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Masasas;

partial class Program
{
    private static bool ValidateUser(string id, string accessCode, [MaybeNullWhen(false)] out User user)
    {
        return users.TryGetValue(id, out user) && user != null && (user.DailyAccessCode == accessCode || user.DailyAccessCodeYesterday == accessCode) && user.Alias == null;
    }

    private static bool ValidateTable(string id, string accessCode, [MaybeNullWhen(false)] out Table table)
    {
        return tables.TryGetValue(id, out table) && (table.DailyAccessCodeYesterday == accessCode || table.DailyAccessCode == accessCode);
    }

    static HttpResponse UserGet(string id, string password)
    {
        List<string> IDs = [id];

        while (true)
        {
            if (!users.TryGetValue(IDs.Last(), out User? user) || user == null)
            {
                //integrity enforcement: delete all users that alias to nothing
                if (IDs.Count > 1)
                    foreach (string deletedID in IDs)
                    {
                        users.TryRemove(deletedID, out _);
                    }
                SaveUsers();
                return Utils.BadRequestText("Invalid user id or password");
            }

            if (user!.Alias != null)
            {
                if (IDs.Contains(user.Alias))
                {
                    //integrity enforcement: delete all users that eventually alias to themselves
                    foreach (string deletedID in IDs)
                    {
                        users.TryRemove(deletedID, out _);
                    }
                    SaveUsers();
                    return Utils.BadRequestText("Invalid user id or password");
                }
                else
                {
                    IDs.Add(user.Alias);
                    continue;
                }
            }

            if (users.TryGetValue(IDs.First(), out User? loginUser) && loginUser.PasswordHashed == Utils.Hash(password + loginUser.CreationDate))
            {
                return Utils.OkJson(new { UserID = IDs.Last(), user.DailyAccessCode });
            }
            else
            {
                return Utils.BadRequestText("Invalid user id or password");
            }
        }
    }

    static HttpResponse UserRouteGet(string id, string accessCode, string command)
    {
        if (!ValidateUser(id, accessCode, out User? user))
        {
            return Utils.BadRequestText("Invalid user id or daily access code");
        }

        switch (command)
        {
            case "get_preferences":
                return Utils.OkJson(users[id].Preferences);

            case "get_personalization_state":
                return Utils.OkText((config.UserPersonalization && users[id].AllowedPersonalization).ToString());

            case "get_self_deletion_state":
                return Utils.OkText(users[id].AllowedSelfDeletion.ToString());

            case "get_tables":
                return Utils.OkJson(tables.Select((val) => new { ID = val.Key, val.Value.DailyAccessCode, val.Value.Data }));

            case "delete_user":
                if (!config.UserSelfDeletion)
                    return Utils.BadRequestText("User self deletion is disabled");

                if (user.Administrator && users.Count((val) => val.Value.Administrator) == 1)
                    return Utils.BadRequestText("Cannot delete the last administrator");

                users.TryRemove(id, out User? deletedUser);
                return Utils.OkJson(deletedUser?.Preferences);

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    async static Task<HttpResponse> UserRoutePost(string id, string accessCode, string command, Stream body)
    {
        if (!ValidateUser(id, accessCode, out User? user))
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
                {JsonSerializer.Serialize(Data.UnsecuredGuestUser.Preferences, Utils.JsonOptions)}
                """);

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static HttpResponse TableRouteGet(string id, string accessCode, string command)
    {
        if (!ValidateTable(id, accessCode, out Table? table))
        {
            return Utils.BadRequestText("Invalid table id or daily access code");
        }

        return command switch
        {
            "get_data" => Utils.OkJson(table),
            _ => Utils.BadRequestText("Unknown command"),
        };
    }


    static async Task<HttpResponse> TableRoutePost(string id, string accessCode, string command, Stream body)
    {
        if (!ValidateTable(id, accessCode, out Table? table))
        {
            return Utils.BadRequestText("Invalid table id or daily access code");
        }

        switch (command)
        {
            case "set_height":
                try
                {
                    table.Data.CurrentHeight = Convert.ToDouble(await new StreamReader(body).ReadToEndAsync(), CultureInfo.InvariantCulture);
                    table.Data.SetRecently = true;

                    SaveTables();
                    return Utils.OkJson(table.Data.CurrentHeight);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid table height, should be a double in meters");

            case "set_height_percentage":
                try
                {
                    table.Data.CurrentHeight = table.Data.MinHeight + Convert.ToDouble(await new StreamReader(body).ReadToEndAsync(), CultureInfo.InvariantCulture) * (table.Data.MaxHeight - table.Data.MinHeight);
                    table.Data.SetRecently = true;

                    SaveTables();
                    return Utils.OkJson((table.Data.CurrentHeight - table.Data.MinHeight) / (table.Data.MaxHeight - table.Data.MinHeight));
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid table height percentage, should be a double between 0 and 1");

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static HttpResponse AdminGet(string id, string accessCode)
    {
        if (!ValidateUser(id, accessCode, out User? user))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");
        }

        return Utils.OkText(user.Administrator.ToString());
    }

    static async Task<HttpResponse> AdminRouteGet(string id, string accessCode, string command)
    {
        if (!ValidateUser(id, accessCode, out User? user))
        {
            return Utils.BadRequestText("Invalid admin id or daily access code");
        }

        if (!user.Administrator)
            return new HttpResponse("Unauthorized user", "text/plain", 401);

        switch (command)
        {
            case "get_users":
                return Utils.OkJson(users.Select((val) => new { ID = val.Key, val.Value.Preferences }));

            case "import_external_api_tables":
                if (await api.ImportTables())
                    return Utils.OkText("Imported tables successfully");
                else return Utils.BadRequestText("Could not import external api tables");

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
        if (!ValidateUser(id, accessCode, out User? user))
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

            case "alias_user":
                if (commandValue == id && users.Count((val) => val.Value.Administrator) == 1)
                    return Utils.BadRequestText("Cannot alias last administrator");

                if (!users.TryRemove(commandValue, out _))
                    return Utils.BadRequestText("User does not exist");


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
        if (!ValidateUser(id, accessCode, out User? user))
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
                    return Utils.OkText(config.ConfigReloadPeriodSeconds.ToString() ?? "null");
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid config reload time, should be a double in seconds, or null to disable reloading");

            case "set_external_api_url":
                try
                {
                    string? bodyText = await new StreamReader(body).ReadToEndAsync();
                    config.ExternalAPIUrl = new Uri(bodyText).ToString();
                    SaveConfig();
                    return Utils.OkText(config.ExternalAPIUrl);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid api url");

            case "set_external_api_key":
                try
                {
                    string? bodyText = await new StreamReader(body).ReadToEndAsync();
                    config.ExternalAPIKey = bodyText;
                    SaveConfig();
                    return Utils.OkText(config.ExternalAPIKey);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid api key");

            case "set_external_api_type":
                try
                {
                    string? bodyText = await new StreamReader(body).ReadToEndAsync();
                    config.ExternalAPIType = bodyText;
                    UpdateExternalAPI();
                    SaveConfig();
                    return Utils.OkText(config.ExternalAPIType);
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid api type");

            case "set_external_api_request_frequency_seconds":
                try
                {
                    string? bodyText = await new StreamReader(body).ReadToEndAsync();
                    if (bodyText == "null" || string.IsNullOrWhiteSpace(bodyText))
                        config.ExternalAPIRequestFrequencySeconds = null;
                    else
                        config.ExternalAPIRequestFrequencySeconds = Convert.ToDouble(bodyText, CultureInfo.InvariantCulture);
                    UpdateExternalAPI();
                    SaveConfig();
                    return Utils.OkText(config.ExternalAPIRequestFrequencySeconds.ToString() ?? "null");
                }
                catch (Exception e) { Console.WriteLine(e.Message); }
                return Utils.BadRequestText("Invalid api request frequency time, should be a double in seconds, or null to disable requests");

            default:
                return Utils.BadRequestText("Unknown command");
        };
    }

    static async Task<HttpResponse> AdminRoutePostWithCommandValue(string id, string accessCode, string command, string commandValue, Stream body)
    {
        if (!ValidateUser(id, accessCode, out User? user))
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
                {JsonSerializer.Serialize(Data.UnsecuredGuestUser, Utils.JsonOptions)}
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

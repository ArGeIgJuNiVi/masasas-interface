using System.Text.Json;
namespace Masasas;
static class Data
{
    public const string GuestUserID = "guest";
    public static readonly UnsecuredUser GuestUser = new(Utils.EncryptToHex("1234"), true, true, new("Guest") { HeightPresets = [new(1.0, "%"), new(1.5, "m")] });
    public const string NewUserID = "new_user";
    public static readonly UnsecuredUser NewUser = new("NEW_USER_PASSWORD_RSA", true, true, new("NEW_USER_NAME"));

    public static readonly UnsecuredTable NewTable = new(new(
        "00:11:22:33:44:55",
        "MANUFACTURER_NAME",
        1.0,
        1.5,
        "PHYSICAL_LOCATION"
    ));

    public static readonly HttpResponse Root = new($"""
    Usage:
    GET: /rsa - get the rsa public key of this server

    GET: /user/USER_ID/USER_PASSWORD_RSA - get user daily access code

    GET: /user/USER_ID/USER_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_preferences - get user preferences
    get_personalization_state - get if the user is able to set preferences 
    get_tables - get the list of all tables and their daily access codes
    delete_user - delete self

    POST: /user/USER_ID/USER_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    set_preferences - set user preferences
    New user preferences json:
    {JsonSerializer.Serialize(GuestUser.Preferences, Utils.JsonOptions)}

    GET: /table/TABLE_ID/TABLE_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_data - get table data

    POST: /table/TABLE_ID/TABLE_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    set_height - set table height (double, in meters)
    set_height_percentage - set table height percentage (double, 0 to 1)

    Administrator usage:
    GET: /admin/ADMIN_ID/ADMIN_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_users - get the list of all users
    disable_guest_warning - disable initial warning about the default account
    enable_user_self_deletion - enable the ability of the users to delete their own accounts
    disable_user_self_deletion - disable the ability of the users to delete their own accounts
    enable_user_personalization - enable the ability of the users to modify their account personalization
    disable_user_personalization - disable the ability of the users to modify their account personalization
    enable_user_personalization/USER_ID - enable the ability of a user to modify their account personalization
    disable_user_personalization/USER_ID - disable the ability of a user to modify their account personalization
    delete_user/USER_ID - delete a user account
    delete_table/TABLE_ID - delete a table

    POST: /admin/ADMIN_ID/ADMIN_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    set_config_reload_seconds - set the time to reload a file or null to disable automatic reloading (double, in seconds)
    create_user/USER_ID - create or update a user account
    New user account json:
    {JsonSerializer.Serialize(GuestUser, Utils.JsonOptions)}
    create_table/TABLE_ID - create or update a table
    New table json structure:
    {JsonSerializer.Serialize(NewTable, Utils.JsonOptions)}
    """);


    private static readonly string dailyAccessCode = ((User)GuestUser).DailyAccessCode;
    public static readonly HttpResponse RootWithWarning = new($"""
    Welcome to the Masasas table api

    --- WARNING ---
    By default there is one administrator user with the following login details:
    USER_ID: guest
    USER_PASSWORD_RSA: {GuestUser.PasswordRSA}
    USER_DAILY_ACCESS_CODE: {dailyAccessCode}

    Make sure to create a new user with different credentials and delete this one to improve security:
    POST: /admin/{GuestUserID}/{dailyAccessCode}/create_user/{NewUserID}
    BODY:
    {JsonSerializer.Serialize(NewUser, Utils.JsonOptions)}
    GET: /admin/{GuestUserID}/{dailyAccessCode}/delete_user/{GuestUserID}

    If you only want to turn off this warning, edit the config file and the it will be reloaded in a few seconds or just run
    GET: /admin/{GuestUserID}/{dailyAccessCode}/disable_guest_warning
    --- WARNING ---

    {Root.Content}
    """);
}

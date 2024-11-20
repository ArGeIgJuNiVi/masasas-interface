using System.Text.Json;
namespace Masasas;
static class Data
{
    public const string GuestUserID = "guest";
    public const string GuestUserPassword = "1234";
    public static readonly UnsecuredUser UnsecuredGuestUser = new(GuestUserPassword, new("Guest") { HeightPresets = [new(1.0, "%"), new(1.5, "m")] })
    {
        Administrator = true,
        AllowedPersonalization = true,
        AllowedSelfDeletion = false,
    };
    public static User GuestUser = UnsecuredGuestUser;

    public const string NewUserID = "new_user";
    public static readonly UnsecuredUser NewUser = new("NEW_USER_PASSWORD_RSA", new("NEW_USER_NAME"))
    {
        Administrator = true,
        AllowedPersonalization = true,
        AllowedSelfDeletion = true,
    };

    public static readonly UnsecuredTable NewTable = new(new(
        "00:11:22:33:44:55",
        "bluetooth",
        "MANUFACTURER_NAME",
        1.0,
        1.5,
        "USER_FRIENDLY_NAME"
    ));

    public static readonly HttpResponse Root = new($"""
    Usage:
    GET: /user/USER_ID/USER_PASSWORD - get user id and daily access code

    GET: /user/USER_ID/USER_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_preferences - get user preferences
    get_personalization_state - get if the user is able to set preferences 
    get_self_deletion_state - get if the user is able to delete their own account 
    get_tables - get the list of all tables and their daily access codes
    delete_user - delete self

    POST: /user/USER_ID/USER_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    set_preferences - set user preferences
    New user preferences json:
    {JsonSerializer.Serialize(UnsecuredGuestUser.Preferences, Utils.JsonOptions)}

    GET: /table/TABLE_ID/TABLE_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_data - get table data

    POST: /table/TABLE_ID/TABLE_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    set_height - set table height (double, in meters)
    set_height_percentage - set table height percentage (double, 0 to 1)

    Administrator usage:
    GET: /admin/ADMIN_ID/ADMIN_DAILY_ACCESS_CODE - get if the user is an administrator

    GET: /admin/ADMIN_ID/ADMIN_DAILY_ACCESS_CODE/COMMAND
    Options for COMMAND
    get_users - get the list of all users
    import_external_api_tables - import the tables from the defined external api
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
    set_external_api_key - set the external api key (string)
    set_external_api_url - set the external api url (string, parsed into Uri)
    set_external_api_type - set the used external api implementation (Options: "dummy", "Kr64", invalid values are equivalent to dummy)
    set_external_api_request_frequency_seconds - set the time to update the tables' data using the external api or null to disable it (double, in seconds)
    create_user/USER_ID - create or update a user account
    New user account json:
    {JsonSerializer.Serialize(NewUser, Utils.JsonOptions)}
    create_table/TABLE_ID - create or update a table
    New table json structure:
    {JsonSerializer.Serialize(NewTable, Utils.JsonOptions)}
    """);


    public static HttpResponse RootWithWarning => new($"""
    Welcome to the Masasas table api

    --- WARNING ---
    By default there is one administrator user with the following login details:
    USER_ID: guest
    USER_PASSWORD: {GuestUserPassword}
    USER_PASSWORD_HASHED: {GuestUser.PasswordHashed}
    USER_CREATION_DATE: {GuestUser.CreationDate}
    USER_DAILY_ACCESS_CODE: {GuestUser.DailyAccessCode}

    Make sure to create a new user with different credentials and delete this one to improve security:
    POST: /admin/{GuestUserID}/{GuestUser.DailyAccessCode}/create_user/{NewUserID}
    BODY:
    {JsonSerializer.Serialize(NewUser, Utils.JsonOptions)}
    GET: /admin/{GuestUserID}/{GuestUser.DailyAccessCode}/delete_user/{GuestUserID}

    If you only want to turn off this warning, edit the config file or just run
    GET: /admin/{GuestUserID}/{GuestUser.DailyAccessCode}/disable_guest_warning
    --- WARNING ---

    {Root.Content}
    """);

    public static readonly HttpResponse RSAPub = Utils.OkText(Utils.RSA.ExportRSAPublicKeyPem());
}

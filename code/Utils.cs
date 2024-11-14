using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Masasas;

static class Utils
{
    public static string Encrypt(string str) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(str)));

    public static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };

    public static HttpResponse BadRequestText(string text) => new(text, "text/plain", 400);
    public static HttpResponse BadRequestJson<T>(T json) => new(JsonSerializer.Serialize(json, JsonOptions), "application/json", 400);
    public static HttpResponse OkText(string text) => new(text);
    public static HttpResponse OkJson<T>(T json) => new(JsonSerializer.Serialize(json, JsonOptions), "application/json");
}
namespace PicoERP.Application.Interfaces;

public interface ISettingsService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

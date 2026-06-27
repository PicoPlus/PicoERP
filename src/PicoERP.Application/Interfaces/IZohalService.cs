using PicoERP.Application.DTOs;

namespace PicoERP.Application.Interfaces;

public interface IZohalService
{
    /// <summary>
    /// Calls the Zohal national-identity inquiry API.
    /// Returns a result with Matched=false (and an Error message) when something goes wrong.
    /// </summary>
    Task<ZohalIdentityResultDto> InquireAsync(string nationalCode, string birthDate);
}

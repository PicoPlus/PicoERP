using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PicoERP.Application.Interfaces;
using PicoERP.Web.Filters;

namespace PicoERP.Web.Controllers;

/// <summary>
/// Returns financial accounts for the MAUI app's account picker dropdown.
/// </summary>
[ApiController]
[Route("api/financial-accounts")]
[MobileApiKey]
[Authorize]
public sealed class FinancialAccountController : ControllerBase
{
    private readonly IFinancialAccountService _service;
    public FinancialAccountController(IFinancialAccountService service) => _service = service;

    /// <summary>GET /api/financial-accounts — all active accounts.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _service.GetAllAsync();
        return Ok(list);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Web.Filters;

namespace PicoERP.Web.Controllers;

/// <summary>
/// REST API for bank transfer receipts and payments — used by the MAUI mobile app.
/// Requires both a valid X-Mobile-Api-Key header AND a valid JWT bearer token.
/// </summary>
[ApiController]
[Route("api/bank-transfers")]
[MobileApiKey]
[Authorize]
public sealed class BankTransferController : ControllerBase
{
    private readonly IBankTransferService _service;
    public BankTransferController(IBankTransferService service) => _service = service;

    private string CurrentUser =>
        User.FindFirstValue(ClaimTypes.Name) ?? "mobile";

    // ── Receipts ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/bank-transfers/receipts?from=&amp;to=</summary>
    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var list = await _service.GetReceiptsAsync(from, to);
        return Ok(list);
    }

    /// <summary>GET /api/bank-transfers/receipts/{id}</summary>
    [HttpGet("receipts/{id:int}")]
    public async Task<IActionResult> GetReceipt(int id)
    {
        var item = await _service.GetReceiptByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>POST /api/bank-transfers/receipts — create a new incoming receipt.</summary>
    [HttpPost("receipts")]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateBankTransferReceiptDto dto)
    {
        var result = await _service.CreateReceiptAsync(dto, CurrentUser);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });
        return CreatedAtAction(nameof(GetReceipt), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>DELETE /api/bank-transfers/receipts/{id}</summary>
    [HttpDelete("receipts/{id:int}")]
    public async Task<IActionResult> DeleteReceipt(int id)
    {
        var result = await _service.DeleteReceiptAsync(id);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    // ── Payments linked to a receipt ─────────────────────────────────────────

    /// <summary>POST /api/bank-transfers/receipts/{id}/payments — add an outgoing payment.</summary>
    [HttpPost("receipts/{id:int}/payments")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] CreateBankTransferPaymentDto dto)
    {
        dto.ReceiptId = id;
        var result = await _service.AddPaymentAsync(dto, CurrentUser);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }

    /// <summary>DELETE /api/bank-transfers/payments/{id}</summary>
    [HttpDelete("payments/{id:int}")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var result = await _service.DeletePaymentAsync(id);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}

using Microsoft.EntityFrameworkCore;
using PicoERP.Application.DTOs;
using PicoERP.Application.Interfaces;
using PicoERP.Domain.Entities;
using PicoERP.Infrastructure.Persistence;

namespace PicoERP.Infrastructure.Services;

public class SmsLogService : ISmsLogService
{
    private readonly AppDbContext _db;

    public SmsLogService(AppDbContext db) => _db = db;

    public async Task<List<SmsLogDto>> GetLogsForContactAsync(string contactPhone)
    {
        var normalized = NormalizePhone(contactPhone);
        return await _db.SmsLogs
            .AsNoTracking()
            .Where(l => l.ContactPhone == normalized || l.ContactPhone == contactPhone)
            .OrderByDescending(l => l.SentAt)
            .Select(l => ToDto(l))
            .ToListAsync();
    }

    public async Task<List<SmsLogDto>> GetLogsAsync(string? contactHsId = null, int page = 1, int pageSize = 50)
    {
        var q = _db.SmsLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(contactHsId))
            q = q.Where(l => l.ContactHsId == contactHsId);
        return await q
            .OrderByDescending(l => l.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => ToDto(l))
            .ToListAsync();
    }

    public async Task<int> SaveLogAsync(SmsLogDto dto)
    {
        var entity = new SmsLog
        {
            ContactHsId      = dto.ContactHsId,
            ContactPhone     = dto.ContactPhone,
            ContactName      = dto.ContactName,
            Direction        = dto.Direction,
            Message          = dto.Message,
            IpPanelMessageId = dto.IpPanelMessageId,
            SenderNumber     = dto.SenderNumber,
            PatternCode      = dto.PatternCode,
            Status           = dto.Status ?? "sent",
            SentAt           = dto.SentAt == default ? DateTime.UtcNow : dto.SentAt,
            CreatedAt        = DateTime.UtcNow
        };
        _db.SmsLogs.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }

    private static string NormalizePhone(string number)
    {
        var n = number.TrimStart('+').Trim();
        if (n.StartsWith('0'))
            n = "98" + n[1..];
        return n;
    }

    private static SmsLogDto ToDto(SmsLog l) => new()
    {
        Id               = l.Id,
        ContactHsId      = l.ContactHsId,
        ContactPhone     = l.ContactPhone,
        ContactName      = l.ContactName,
        Direction        = l.Direction,
        Message          = l.Message,
        IpPanelMessageId = l.IpPanelMessageId,
        SenderNumber     = l.SenderNumber,
        PatternCode      = l.PatternCode,
        Status           = l.Status,
        SentAt           = l.SentAt,
        CreatedAt        = l.CreatedAt
    };
}

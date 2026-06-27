using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

/// <summary>Keeps a record of every Zohal national-identity inquiry made from the app.</summary>
public class ZohalLog : BaseEntity
{
    public string  NationalCode { get; set; } = string.Empty;
    public string  BirthDate    { get; set; } = string.Empty;
    public bool    Matched      { get; set; }
    public string? FirstName    { get; set; }
    public string? LastName     { get; set; }
    public string? FatherName   { get; set; }
    public bool?   IsDead       { get; set; }
    public string? Error        { get; set; }
    /// <summary>HubSpot contact ID this inquiry was run for, if known.</summary>
    public string? ContactHsId  { get; set; }
    public DateTime InquiredAt  { get; set; } = DateTime.UtcNow;
}

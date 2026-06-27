namespace PicoERP.Application.DTOs;

/// <summary>Result from the Zohal national-identity inquiry web service.</summary>
public class ZohalIdentityResultDto
{
    /// <summary>True when national_code + birth_date matched a record in the civil registry.</summary>
    public bool Matched { get; set; }

    public string? FirstName  { get; set; }
    public string? LastName   { get; set; }
    public string? FatherName { get; set; }

    /// <summary>True when the person is alive.</summary>
    public bool? Alive  { get; set; }

    /// <summary>True when the person is deceased.</summary>
    public bool? IsDead { get; set; }

    public string? NationalCode { get; set; }

    /// <summary>Error message from the API, if any (400 responses).</summary>
    public string? Error { get; set; }
}

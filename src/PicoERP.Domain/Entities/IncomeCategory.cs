using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

public class IncomeCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Income> Incomes { get; set; } = new List<Income>();
}

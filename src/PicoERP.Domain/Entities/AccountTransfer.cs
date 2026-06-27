using PicoERP.Domain.Common;

namespace PicoERP.Domain.Entities;

public class AccountTransfer : BaseEntity
{
    public int FromAccountId { get; set; }
    public FinancialAccount FromAccount { get; set; } = null!;
    public int ToAccountId { get; set; }
    public FinancialAccount ToAccount { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? RegisteredBy { get; set; }
}

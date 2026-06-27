using PicoERP.Domain.Common;
using PicoERP.Domain.Enums;

namespace PicoERP.Domain.Entities;

public class ExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public ExpenseGroup Group { get; set; }
    public int? ParentCategoryId { get; set; }
    public ExpenseCategory? ParentCategory { get; set; }
    public ICollection<ExpenseCategory> SubCategories { get; set; } = new List<ExpenseCategory>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public bool IsActive { get; set; } = true;
}

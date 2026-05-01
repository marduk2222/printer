namespace printer.Models;

public class InvoicePrintViewModel
{
    public printer.Data.Entities.Invoice Invoice { get; set; } = null!;
    public printer.Data.Entities.InvoicePrintSettings Settings { get; set; } = null!;
}

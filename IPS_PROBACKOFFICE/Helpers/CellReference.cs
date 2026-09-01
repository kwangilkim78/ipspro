using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace InvoiceReportPrinter.WinForms.Helpers;

public static class CellReferenceHelper
{
    public static ICell GetOrCreateCell(ISheet sheet, string address)
    {
        var reference = new CellReference(address);
        IRow row = sheet.GetRow(reference.Row) ?? sheet.CreateRow(reference.Row);
        return row.GetCell(reference.Col) ?? row.CreateCell(reference.Col);
    }
}

using System.Runtime.InteropServices;

namespace InvoiceReportPrinter.WinForms.Services;

public static class InvoicePdfExporter
{
    public static Task<string> ExportAsync(string workbookPath)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Export(workbookPath)); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Invoice PDF export" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string Export(string workbookPath)
    {
        string source = Path.GetFullPath(workbookPath);
        string folder = Path.GetDirectoryName(source)!;
        string target = Path.ChangeExtension(source, ".pdf");
        string temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.pdf");
        object? application = null;
        object? workbooks = null;
        object? workbook = null;
        try
        {
            Type type = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException("PDF 저장에는 Microsoft Excel 설치가 필요합니다.");
            application = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Microsoft Excel을 시작하지 못했습니다.");
            dynamic excel = application;
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excel.AutomationSecurity = 3; // 매크로 실행 차단
            excel.EnableEvents = false;
            excel.AskToUpdateLinks = false;
            workbooks = excel.Workbooks;
            workbook = ((dynamic)workbooks).Open(source, UpdateLinks: 0, ReadOnly: true,
                IgnoreReadOnlyRecommended: true, AddToMru: false);
            ((dynamic)workbook).ExportAsFixedFormat(Type: 0, Filename: temporary,
                Quality: 0, IncludeDocProperties: true, IgnorePrintAreas: false,
                OpenAfterPublish: false);
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException("Excel에서 PDF 파일이 생성되지 않았습니다.");
            string originalTarget = target;
            for (int suffix = 2; ; suffix++)
            {
                try { File.Move(temporary, target, overwrite: false); break; }
                catch (IOException) when (File.Exists(target))
                {
                    target = Path.Combine(folder,
                        $"{Path.GetFileNameWithoutExtension(originalTarget)} ({suffix}).pdf");
                }
            }
            return target;
        }
        finally
        {
            // 이 작업에서 만든 Excel 인스턴스만 닫는다. 사용자가 연 Excel에는 접근하지 않는다.
            try { if (workbook is not null) ((dynamic)workbook).Close(SaveChanges: false); }
            catch { }
            Release(workbook);
            Release(workbooks);
            try { if (application is not null) ((dynamic)application).Quit(); }
            catch { }
            Release(application);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }
}

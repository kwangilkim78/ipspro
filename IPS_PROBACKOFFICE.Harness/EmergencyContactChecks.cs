using IPS_PROBACKOFFICE.Models;

internal static class EmergencyContactChecks
{
    public static void Run()
    {
        CheckEditInstitution();
        var valid = new EmergencyContact { InstitutionId = 1, ContactName = "담당자", WorkPhone = "02-1234-5678" };
        valid.Validate();
        (valid with { Category = "IT", WorkPhone = "", MobilePhone = "+82 10 1234 5678" }).Validate();
        (valid with { Category = "백오피스", Priority = 99, Email = "ops@example.com" }).Validate();
        Reject(valid with { InstitutionId = 0 });
        Reject(valid with { ContactName = " " });
        Reject(valid with { Category = "기타" });
        Reject(valid with { WorkPhone = "", MobilePhone = " " });
        Reject(valid with { Priority = 0 });
        Reject(valid with { Priority = 100 });
        Reject(valid with { Email = "invalid-email" });
        Reject(valid with { Remarks = new string('가', 1001) });
        Reject(valid with { MobilePhone = new string('1', 51) });
        using var schema = typeof(EmergencyContact).Assembly.GetManifestResourceStream("IPS_PROBACKOFFICE.Database.v63_emergency_contacts.sql");
        if (schema is null) throw new InvalidOperationException("내장 DB 설치 스크립트 누락");
        Console.WriteLine("HARNESS PASS: 비상연락망 필수입력, 업무 구분, 연락 순서, 길이, 이메일 검증 및 DB 설치 리소스");
    }

    private static void Reject(EmergencyContact contact)
    {
        try { contact.Validate(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("유효하지 않은 연락처를 허용했습니다.");
    }

    private static void CheckEditInstitution()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var institutions = new[]
                {
                    new InvoiceReportPrinter.WinForms.Models.Institution("", "DBS Singapore", null, null, 1),
                    new InvoiceReportPrinter.WinForms.Models.Institution("", "하나은행", null, null, 2)
                };
                var type = typeof(EmergencyContact).Assembly.GetType("IPS_PROBACKOFFICE.EmergencyContactEditForm")!;
                foreach (long id in new long[] { 2, 3 })
                {
                    var contact = new EmergencyContact { Id = 10, InstitutionId = id, InstitutionName = "하나은행", ContactName = "담당자", WorkPhone = "02" };
                    Func<EmergencyContact, Task> noOp = _ => Task.CompletedTask;
                    using var form = (System.Windows.Forms.Form)Activator.CreateInstance(type, institutions, contact, noOp, noOp)!;
                    form.BindingContext = new System.Windows.Forms.BindingContext();
                    form.CreateControl();
                    form.PerformLayout();
                    var table = (System.Windows.Forms.TableLayoutPanel)form.Controls[0];
                    var combo = (System.Windows.Forms.ComboBox)table.GetControlFromPosition(1, 0)!;
                    if (combo.SelectedItem is not InvoiceReportPrinter.WinForms.Models.Institution selected || selected.ExternalInstitutionId != id)
                        throw new InvalidOperationException("수정 창에서 기존 기관 선택이 변경되었습니다.");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("기관 선택 회귀 검사 실패", failure);
        Console.WriteLine("HARNESS PASS: 수정 창 기존 기관 유지 및 목록에서 누락된 기관 유지");
    }
}

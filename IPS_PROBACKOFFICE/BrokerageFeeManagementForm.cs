using IPS_PROBACKOFFICE.Data;

namespace IPS_PROBACKOFFICE;

public sealed class BrokerageFeeManagementForm : Form
{
    private readonly BrokerageFeeForm _brokerageFeeForm;
    private readonly ApiFeeExemptionForm _apiFeeExemptionForm;

    public BrokerageFeeManagementForm(
        BrokerageFeeRepository brokerageFeeRepository,
        ApiFeeExemptionRepository apiFeeExemptionRepository)
    {
        Text = "중개 수수료 관리";
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.White;

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(18, 6)
        };

        _brokerageFeeForm = new BrokerageFeeForm(brokerageFeeRepository);
        _apiFeeExemptionForm = new ApiFeeExemptionForm(apiFeeExemptionRepository);

        tabs.TabPages.Add(CreateTab("기관별 중개 수수료", _brokerageFeeForm));
        tabs.TabPages.Add(CreateTab("API 수수료 면제 관리", _apiFeeExemptionForm));
        tabs.SelectedIndexChanged += (_, _) => ShowSelectedTab(tabs);
        Shown += (_, _) => ShowSelectedTab(tabs);
        Controls.Add(tabs);
    }

    private static TabPage CreateTab(string title, Form form)
    {
        var page = new TabPage
        {
            Text = title,
            BackColor = Color.White,
            Padding = Padding.Empty
        };

        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        page.Controls.Add(form);
        return page;
    }

    private static void ShowSelectedTab(TabControl tabs)
    {
        if (tabs.SelectedTab?.Controls.OfType<Form>().FirstOrDefault() is { Visible: false } form)
            form.Show();
    }
}

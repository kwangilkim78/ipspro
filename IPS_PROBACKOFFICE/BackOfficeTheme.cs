using System.Runtime.CompilerServices;

namespace IPS_PROBACKOFFICE;

/// <summary>화면 컨트롤 전용 스타일. 데이터, 열 순서, 출력 서식은 변경하지 않는다.</summary>
internal static class BackOfficeTheme
{
    private static readonly ConditionalWeakTable<Control, object> Styled = new();
    internal static readonly Color Background = Color.FromArgb(245, 247, 250);
    private static readonly Color Ink = Color.FromArgb(34, 65, 96);
    private static readonly Color Header = Color.FromArgb(225, 234, 244);

    internal static void Apply(Control control)
    {
        if (Styled.TryGetValue(control, out _)) return;
        Styled.Add(control, new object());
        switch (control)
        {
            case DataGridView grid:
                StyleGrid(grid);
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(201, 213, 227);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 242, 250);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(210, 229, 249);
                button.BackColor = Color.White;
                button.ForeColor = Ink;
                button.UseVisualStyleBackColor = false;
                // 고정 크기의 기존 도구모음은 유지해 줄바꿈/잘림을 방지한다.
                if (button.AutoSize) button.Padding = new Padding(8, 3, 8, 3);
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                break;
            case Form form:
                form.BackColor = Background;
                if (form.Padding == Padding.Empty) form.Padding = new Padding(16);
                break;
            case FlowLayoutPanel flow:
                flow.BackColor = Color.White;
                break;
            case TabControl:
            case TabPage:
            case TableLayoutPanel:
            case Panel:
            case GroupBox:
                // 명시적으로 설정한 보고서/섹션 배경색은 보존한다.
                if (control.BackColor == SystemColors.Control)
                    control.BackColor = Background;
                break;
        }

        foreach (Control child in control.Controls) Apply(child);
        control.ControlAdded += (_, e) =>
        {
            if (e.Control is not null) Apply(e.Control);
        };
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(229, 234, 240);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 38);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
        // 기존 숫자 서식과 정렬을 유지하고 배경/선택 색상만 공통화한다.
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 229, 249);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 55, 90);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 30);
        // 합계/경고 색상은 각 화면의 행 스타일 및 CellFormatting에서 계속 적용한다.
    }
}

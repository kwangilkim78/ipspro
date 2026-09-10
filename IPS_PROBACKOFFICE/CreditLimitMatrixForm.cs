using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using System.Drawing.Printing;

namespace IPS_PROBACKOFFICE;

public sealed class CreditLimitMatrixForm : Form
{
    private sealed record DirectionalCell(
        string TopRightAmount,
        string BottomLeftAmount,
        bool TopRightInactive,
        bool BottomLeftInactive);

    private static readonly string[] Products =
    [
        "DF 1M",
        "NDF 1M/NDF MAR/NDF TOMMAR",
        "BASIS MAR",
        "BASIS TOMMAR",
        "TOTAL"
    ];

    private readonly CreditRepository _repository;
    private readonly ComboBox _cmbProduct = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 280
    };
    private readonly Button _btnRefresh = new() { Text = "전체 새로고침", Width = 130 };
    private readonly CheckBox _chkCreditAvailable = new()
    {
        Text = "Credit Available",
        AutoSize = true,
        Checked = false,
        Margin = new Padding(18, 7, 0, 0)
    };
    private readonly Label _status = new() { AutoSize = true, Text = "조회 대기" };
    private readonly Label _legend = new()
    {
        Dock = DockStyle.Top,
        Height = 42,
        Padding = new Padding(10, 5, 0, 0)
    };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        EnableHeadersVisualStyles = false,
        ColumnHeadersHeight = 82,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
    };
    private IReadOnlyList<CreditInstitution> _institutions = [];
    private IReadOnlyList<CreditMatrixEntry> _entries = [];
    private int _highlightedRow = -1;
    private int _highlightedColumn = -1;
    private readonly ContextMenuStrip _printMenu = new();

    public CreditLimitMatrixForm(CreditRepository repository)
    {
        _repository = repository;
        Text = "전체 기관 한도 매트릭스";
        BuildUi();
        BackOfficeTheme.Apply(_grid);

        Shown += async (_, _) => await LoadMatrixAsync();
        _btnRefresh.Click += async (_, _) => await LoadMatrixAsync();
        _cmbProduct.SelectedIndexChanged += (_, _) => RefreshSelectedProduct();
        _chkCreditAvailable.CheckedChanged += (_, _) => RefreshSelectedProduct();
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellMouseClick += Grid_CellMouseClick;
        _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        _grid.CellMouseEnter += (_, e) =>
        {
            _highlightedRow = e.RowIndex;
            _highlightedColumn = e.ColumnIndex > 0 ? e.ColumnIndex : -1;
            _grid.Invalidate();
        };
        _grid.MouseLeave += (_, _) =>
        {
            _highlightedRow = _highlightedColumn = -1;
            _grid.Invalidate();
        };

        var previewItem = new ToolStripMenuItem("인쇄 미리보기");
        var printItem = new ToolStripMenuItem("바로 인쇄");
        previewItem.Click += (_, _) => PrintMatrix(showPreview: true);
        printItem.Click += (_, _) => PrintMatrix(showPreview: false);
        _printMenu.Items.Add(previewItem);
        _printMenu.Items.Add(printItem);
        _grid.ContextMenuStrip = _printMenu;
    }

    private void BuildUi()
    {
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10),
            WrapContents = false
        };
        topPanel.Controls.Add(new Label
        {
            Text = "상품",
            AutoSize = true,
            Margin = new Padding(0, 8, 7, 0)
        });
        _cmbProduct.Items.AddRange(Products);
        _cmbProduct.SelectedIndex = 0;
        topPanel.Controls.Add(_cmbProduct);
        topPanel.Controls.Add(_btnRefresh);
        topPanel.Controls.Add(_chkCreditAvailable);
        topPanel.Controls.Add(_status);

        UpdateLegend();

        Controls.Add(_grid);
        Controls.Add(_legend);
        Controls.Add(topPanel);
    }

    private async Task LoadMatrixAsync()
    {
        await RunBusyAsync(async () =>
        {
            _institutions = await _repository.GetInstitutionsAsync();
            _entries = await _repository.GetAllLimitsAsync();
            RefreshSelectedProduct();
            _status.Text = $"활성 기관 {_institutions.Count:N0}개 / 한도 {_entries.Count:N0}건";
        });
    }

    private void RefreshSelectedProduct()
    {
        if (_cmbProduct.SelectedItem is not string product || _institutions.Count == 0)
            return;

        UpdateLegend();
        PopulateMatrix(_institutions, _entries, product);
    }

    private void PopulateMatrix(
        IReadOnlyList<CreditInstitution> institutions,
        IReadOnlyList<CreditMatrixEntry> entries,
        string selectedProduct)
    {
        _highlightedRow = -1;
        _highlightedColumn = -1;
        _grid.Rows.Clear();
        _grid.Columns.Clear();

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Counterparty",
            HeaderText = "Counterparty",
            Width = 190,
            Frozen = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.True,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            }
        });

        foreach (var institution in institutions)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = $"Institution_{institution.CreditorId}",
                HeaderText = $"{institution.CreditorId}\n{MatrixInstitutionName(institution)}",
                Width = 112,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Tag = institution.CreditorId,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.White,
                    Font = new Font("Consolas", 8.5F, FontStyle.Bold)
                }
            });
        }

        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(225, 234, 244);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(34, 65, 96);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 235, 247);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.GridColor = Color.FromArgb(210, 220, 232);

        var byPair = entries
            .GroupBy(x => (x.InstitutionId, x.CounterpartyId))
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var counterparty in institutions)
        {
            int rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = counterparty.CreditorId;
            row.Height = 58;
            row.Cells[0].Value = $"{counterparty.CreditorId}  {MatrixInstitutionName(counterparty)}";

            for (int columnIndex = 1; columnIndex < _grid.Columns.Count; columnIndex++)
            {
                int columnInstitutionIndex = columnIndex - 1;
                if (columnInstitutionIndex == rowIndex)
                    continue;

                CreditInstitution columnInstitution = institutions[columnInstitutionIndex];
                CreditMatrixEntry? rowToColumn = FindEntry(
                    byPair,
                    counterparty.CreditorId,
                    columnInstitution.CreditorId,
                    selectedProduct);
                CreditMatrixEntry? columnToRow = FindEntry(
                    byPair,
                    columnInstitution.CreditorId,
                    counterparty.CreditorId,
                    selectedProduct);

                string Amount(CreditMatrixEntry? entry, bool full = false)
                {
                    decimal value = entry is null
                        ? 0m
                        : _chkCreditAvailable.Checked
                            ? entry.AvailableAmount
                            : entry.LimitAmount;

                    return full ? value.ToString("#,0.############################")
                        : (value / 1_000_000m).ToString("0.############################");
                }

                row.Cells[columnIndex].Value = null;
                row.Cells[columnIndex].Tag = new DirectionalCell(
                    TopRightAmount: Amount(columnToRow),
                    BottomLeftAmount: Amount(rowToColumn),
                    TopRightInactive: columnToRow is not null && !columnToRow.IsActive,
                    BottomLeftInactive: rowToColumn is not null && !rowToColumn.IsActive);

                row.Cells[columnIndex].ToolTipText =
                    $"{MatrixInstitutionName(counterparty)} → {MatrixInstitutionName(columnInstitution)}: {Amount(rowToColumn, true)}\n" +
                    $"{MatrixInstitutionName(columnInstitution)} → {MatrixInstitutionName(counterparty)}: {Amount(columnToRow, true)}";
            }
        }
    }

    private static string MatrixInstitutionName(CreditInstitution institution) =>
        // 107: SAMSUNG SECURITIES, 108: SCB LONDON
        institution.CreditorId is 107 or 108
            ? $"{institution.Name} (NOP)"
            : institution.Name;

    private static CreditMatrixEntry? FindEntry(
        IReadOnlyDictionary<(long InstitutionId, long CounterpartyId), List<CreditMatrixEntry>> byPair,
        long institutionId,
        long counterpartyId,
        string selectedProduct)
    {
        if (!byPair.TryGetValue((institutionId, counterpartyId), out var pairEntries))
            return null;

        if (string.Equals(selectedProduct, "TOTAL", StringComparison.OrdinalIgnoreCase))
        {
            CreditMatrixEntry? first = pairEntries.FirstOrDefault();
            if (first is null)
                return null;

            return first with
            {
                AssetClass = "TOTAL",
                Tenor = "TOTAL",
                LimitAmount = pairEntries.Sum(x => x.LimitAmount),
                AvailableAmount = pairEntries.Sum(x => x.AvailableAmount),
                IsActive = pairEntries.All(x => x.IsActive)
            };
        }

        return pairEntries.FirstOrDefault(x =>
            string.Equals(
                ClassifyProduct(x.AssetClass, x.Tenor),
                selectedProduct,
                StringComparison.OrdinalIgnoreCase));
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        bool rowHighlighted = e.RowIndex >= 0 && e.RowIndex == _highlightedRow;
        bool columnHighlighted = e.ColumnIndex > 0 && e.ColumnIndex == _highlightedColumn;

        bool institutionLabel = (e.RowIndex == -1 && e.ColumnIndex > 0)
            || (e.RowIndex >= 0 && e.ColumnIndex == 0);
        if (institutionLabel && e.FormattedValue is string label && label.EndsWith(" (NOP)", StringComparison.Ordinal)
            && e.Graphics is not null && e.CellStyle is not null)
        {
            bool header = e.RowIndex == -1;
            e.PaintBackground(e.CellBounds, true);
            if (rowHighlighted || columnHighlighted)
            {
                using var background = new SolidBrush(Color.FromArgb(189, 215, 238));
                e.Graphics.FillRectangle(background, e.CellBounds);
            }
            e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
            Font font = e.CellStyle.Font ?? _grid.Font;
            Rectangle nameBounds = Rectangle.Inflate(e.CellBounds, -5, -3);
            Rectangle nopBounds = nameBounds;
            if (header)
            {
                int markerHeight = TextRenderer.MeasureText("(NOP)", font).Height;
                nameBounds.Height -= markerHeight;
                nopBounds.Y = nameBounds.Bottom;
                nopBounds.Height = markerHeight;
            }
            else
            {
                int markerWidth = TextRenderer.MeasureText("(NOP)", font).Width;
                nameBounds.Width -= markerWidth + 4;
                nopBounds.X = nameBounds.Right + 4;
                nopBounds.Width = markerWidth;
            }
            TextFormatFlags alignment = header ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, label[..^6], font, nameBounds,
                e.CellStyle.ForeColor, alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(e.Graphics, "(NOP)", font, nopBounds,
                Color.Firebrick, alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.Handled = true;
            return;
        }

        if (e.RowIndex == -1 && columnHighlighted)
        {
            using var headerBackground = new SolidBrush(Color.FromArgb(189, 215, 238));
            e.Graphics.FillRectangle(headerBackground, e.CellBounds);
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Border |
                DataGridViewPaintParts.ContentForeground |
                DataGridViewPaintParts.ErrorIcon);
            e.Handled = true;
            return;
        }

        if (e.RowIndex >= 0 && e.ColumnIndex == 0 && rowHighlighted)
        {
            using var rowBackground = new SolidBrush(Color.FromArgb(189, 215, 238));
            e.Graphics.FillRectangle(rowBackground, e.CellBounds);
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Border |
                DataGridViewPaintParts.ContentForeground |
                DataGridViewPaintParts.ErrorIcon |
                DataGridViewPaintParts.Focus);
            e.Handled = true;
            return;
        }

        if (e.RowIndex < 0 || e.ColumnIndex <= 0)
            return;

        using var pen = new Pen(Color.FromArgb(210, 220, 232), 1F);
        int columnInstitutionIndex = e.ColumnIndex - 1;
        Color cellBackground = rowHighlighted && columnHighlighted
            ? Color.FromArgb(157, 195, 230)
            : rowHighlighted || columnHighlighted
                ? Color.FromArgb(221, 235, 247)
                : e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(240, 245, 252);

        if (columnInstitutionIndex == e.RowIndex)
        {
            using var diagonalBackground = new SolidBrush(Color.FromArgb(235, 238, 242));
            e.Graphics.FillRectangle(diagonalBackground, e.CellBounds);
            e.Graphics.DrawRectangle(pen, e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, "—", e.CellStyle.Font, e.CellBounds,
                Color.FromArgb(145, 155, 168), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Handled = true;
            return;
        }

        using (var background = new SolidBrush(cellBackground))
            e.Graphics.FillRectangle(background, e.CellBounds);

        e.Graphics.DrawRectangle(
            pen,
            e.CellBounds.X,
            e.CellBounds.Y,
            e.CellBounds.Width - 1,
            e.CellBounds.Height - 1);

        if (_grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is DirectionalCell amounts)
        {
            Rectangle topRight = new(
                e.CellBounds.Left + 6,
                e.CellBounds.Top + 3,
                e.CellBounds.Width - 12,
                e.CellBounds.Height / 2 - 3);
            Rectangle bottomLeft = new(
                e.CellBounds.Left + 6,
                e.CellBounds.Top + e.CellBounds.Height / 2,
                e.CellBounds.Width - 12,
                e.CellBounds.Height / 2 - 3);

            Color topColor = amounts.TopRightInactive ? Color.Firebrick
                : amounts.TopRightAmount == "0" ? Color.FromArgb(139, 150, 166) : Color.FromArgb(30, 66, 112);
            Color bottomColor = amounts.BottomLeftInactive ? Color.Firebrick
                : amounts.BottomLeftAmount == "0" ? Color.FromArgb(139, 150, 166) : Color.FromArgb(67, 102, 148);
            using var topFont = new Font(
                "맑은 고딕",
                10F,
                amounts.TopRightAmount != "0" || amounts.TopRightInactive ? FontStyle.Bold : FontStyle.Regular);
            using var bottomFont = new Font(
                "맑은 고딕",
                10F,
                amounts.BottomLeftAmount != "0" || amounts.BottomLeftInactive ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                amounts.TopRightAmount,
                topFont,
                topRight,
                topColor,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                e.Graphics,
                amounts.BottomLeftAmount,
                bottomFont,
                bottomLeft,
                bottomColor,
                TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        e.Handled = true;
    }

    private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        _highlightedRow = e.RowIndex;
        _highlightedColumn = e.ColumnIndex > 0 ? e.ColumnIndex : -1;
        _grid.Invalidate();
    }

    private void Grid_ColumnHeaderMouseClick(
        object? sender,
        DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex <= 0)
            return;

        _highlightedRow = -1;
        _highlightedColumn = e.ColumnIndex;
        _grid.Invalidate();
    }

    private void PrintMatrix(bool showPreview)
    {
        if (_grid.Rows.Count == 0 || _grid.Columns.Count == 0)
        {
            MessageBox.Show("먼저 크레딧 한도 매트릭스를 조회해주세요.", "인쇄", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using Bitmap matrixImage = CaptureEntireMatrix();
            using var document = CreatePrintDocument(matrixImage);

            if (showPreview)
            {
                using var preview = new PrintPreviewDialog
                {
                    Document = document,
                    Width = 1200,
                    Height = 800,
                    StartPosition = FormStartPosition.CenterParent
                };
                UiLanguage.Apply(preview);
                preview.ShowDialog(this);
            }
            else
            {
                using var dialog = new PrintDialog
                {
                    Document = document,
                    UseEXDialog = true,
                    AllowSomePages = false,
                    AllowSelection = false
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    document.Print();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "인쇄 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Bitmap CaptureEntireMatrix()
    {
        int contentWidth = _grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(x => x.Visible)
            .Sum(x => x.Width) + 2;
        int contentHeight = _grid.ColumnHeadersHeight + _grid.Rows
            .Cast<DataGridViewRow>()
            .Where(x => x.Visible)
            .Sum(x => x.Height) + 2;

        Size originalSize = _grid.Size;
        ScrollBars originalScrollBars = _grid.ScrollBars;
        int firstDisplayedRow = _grid.FirstDisplayedScrollingRowIndex;
        int firstDisplayedColumn = _grid.FirstDisplayedScrollingColumnIndex;

        try
        {
            _grid.ScrollBars = ScrollBars.None;
            _grid.Size = new Size(contentWidth, contentHeight);
            _grid.ClearSelection();
            _grid.Invalidate();
            _grid.Update();

            var bitmap = new Bitmap(contentWidth, contentHeight);
            _grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            return bitmap;
        }
        finally
        {
            _grid.Size = originalSize;
            _grid.ScrollBars = originalScrollBars;
            if (firstDisplayedRow >= 0 && firstDisplayedRow < _grid.Rows.Count)
                _grid.FirstDisplayedScrollingRowIndex = firstDisplayedRow;
            if (firstDisplayedColumn >= 0 && firstDisplayedColumn < _grid.Columns.Count)
                _grid.FirstDisplayedScrollingColumnIndex = firstDisplayedColumn;
        }
    }

    private PrintDocument CreatePrintDocument(Bitmap matrixImage)
    {
        var document = new PrintDocument
        {
            DocumentName = $"Credit Limit Matrix - {_cmbProduct.SelectedItem}"
        };
        document.DefaultPageSettings.Landscape = true;
        document.DefaultPageSettings.Margins = new Margins(15, 15, 15, 15);

        document.PrintPage += (_, e) =>
        {
            Graphics graphics = e.Graphics;
            RectangleF marginBounds = e.MarginBounds;
            RectangleF bounds = marginBounds;
            bounds.Inflate(-2F, -2F);
            const int titleHeight = 42;
            float imageAreaHeight = Math.Max(1F, bounds.Height - titleHeight);
            float scale = Math.Min(
                1F,
                Math.Min(
                    bounds.Width / matrixImage.Width,
                    imageAreaHeight / matrixImage.Height));

            string product = _cmbProduct.SelectedItem?.ToString() ?? string.Empty;
            string valueName = _chkCreditAvailable.Checked ? "Credit Available" : "Credit Limit";
            using var titleFont = new Font("맑은 고딕", 12F, FontStyle.Bold);
            using var infoFont = new Font("맑은 고딕", 8.5F);
            graphics.DrawString(UiLanguage.Text("전체 기관 한도 매트릭스"), titleFont, Brushes.Black, bounds.Left, bounds.Top);
            graphics.DrawString(
                $"{product} · {valueName} · 단위: 백만 · 위: 열 → 행 / 아래: 행 → 열 · {DateTime.Now:yyyy-MM-dd HH:mm}",
                infoFont,
                Brushes.DimGray,
                bounds.Left,
                bounds.Top + 25);

            float destinationWidth = matrixImage.Width * scale;
            float destinationHeight = matrixImage.Height * scale;
            float destinationLeft = bounds.Left + (bounds.Width - destinationWidth) / 2F;
            float destinationTop = bounds.Top + titleHeight;
            graphics.DrawImage(
                matrixImage,
                new RectangleF(destinationLeft, destinationTop, destinationWidth, destinationHeight));

            e.HasMorePages = false;
        };

        return document;
    }

    private static string? ClassifyProduct(string assetClass, string tenor) =>
        (assetClass.Trim().ToUpperInvariant(), tenor.Trim().ToUpperInvariant()) switch
        {
            ("FX", "*") => Products[0],
            ("NDF", "*") => Products[1],
            ("BNDF", "BASIS_MAR") => Products[2],
            ("BNDF", "BASIS_TOMMAR") => Products[3],
            _ => null
        };

    private void UpdateLegend()
    {
        string valueName = _chkCreditAvailable.Checked
            ? "Credit Available"
            : "Credit Limit";

        _legend.Text =
            $"{valueName} · 단위: 백만 (50 = 50,000,000) · 오른쪽 위: 열 기관 → 행 기관 / 왼쪽 아래: 행 기관 → 열 기관\n" +
            "마우스 올리기: 행·열 강조 및 원금액 확인 · 값 없음은 0 · 빨강: 비활성 한도";
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            UseWaitCursor = true;
            _btnRefresh.Enabled = false;
            _cmbProduct.Enabled = false;
            _chkCreditAvailable.Enabled = false;
            _status.Text = "전체 한도 조회 중...";
            await action();
        }
        catch (Exception ex)
        {
            _status.Text = "조회 오류";
            MessageBox.Show(ex.Message, "전체 한도 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnRefresh.Enabled = true;
            _cmbProduct.Enabled = true;
            _chkCreditAvailable.Enabled = true;
        }
    }
}

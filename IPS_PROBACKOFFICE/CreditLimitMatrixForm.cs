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
        Dock = DockStyle.Bottom,
        Height = 28,
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
        ColumnHeadersHeight = 62,
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

        Shown += async (_, _) => await LoadMatrixAsync();
        _btnRefresh.Click += async (_, _) => await LoadMatrixAsync();
        _cmbProduct.SelectedIndexChanged += (_, _) => RefreshSelectedProduct();
        _chkCreditAvailable.CheckedChanged += (_, _) => RefreshSelectedProduct();
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellMouseClick += Grid_CellMouseClick;
        _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

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
            Width = 150,
            Frozen = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            }
        });

        foreach (var institution in institutions)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = $"Institution_{institution.CreditorId}",
                HeaderText = $"{institution.CreditorId}\n{institution.Name}",
                Width = 135,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Tag = institution.CreditorId,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.FromArgb(255, 230, 153),
                    Font = new Font("Consolas", 8.5F, FontStyle.Bold)
                }
            });
        }

        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 235, 247);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.GridColor = Color.Black;

        var byPair = entries
            .GroupBy(x => (x.InstitutionId, x.CounterpartyId))
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var counterparty in institutions)
        {
            int rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = counterparty.CreditorId;
            row.Height = 58;
            row.Cells[0].Value = $"{counterparty.CreditorId}  {counterparty.Name}";

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

                string Amount(CreditMatrixEntry? entry)
                {
                    decimal value = entry is null
                        ? 0m
                        : _chkCreditAvailable.Checked
                            ? entry.AvailableAmount
                            : entry.LimitAmount;

                    return value != 0m
                        ? value.ToString("N0")
                        : "0";
                }

                row.Cells[columnIndex].Value = null;
                row.Cells[columnIndex].Tag = new DirectionalCell(
                    TopRightAmount: Amount(columnToRow),
                    BottomLeftAmount: Amount(rowToColumn),
                    TopRightInactive: columnToRow is not null && !columnToRow.IsActive,
                    BottomLeftInactive: rowToColumn is not null && !rowToColumn.IsActive);

                row.Cells[columnIndex].ToolTipText =
                    $"{counterparty.Name} → {columnInstitution.Name}: {Amount(rowToColumn)}\n" +
                    $"{columnInstitution.Name} → {counterparty.Name}: {Amount(columnToRow)}";
            }
        }
    }

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

        using var pen = new Pen(Color.Black, 1F);
        int columnInstitutionIndex = e.ColumnIndex - 1;
        Color cellBackground = rowHighlighted && columnHighlighted
            ? Color.FromArgb(157, 195, 230)
            : rowHighlighted || columnHighlighted
                ? Color.FromArgb(221, 235, 247)
                : Color.FromArgb(255, 230, 153);

        if (columnInstitutionIndex == e.RowIndex)
        {
            using var diagonalBackground = new SolidBrush(cellBackground);
            e.Graphics.FillRectangle(diagonalBackground, e.CellBounds);
            e.Graphics.DrawRectangle(pen, e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            e.Graphics.DrawLine(
                pen,
                e.CellBounds.Left,
                e.CellBounds.Top,
                e.CellBounds.Right - 1,
                e.CellBounds.Bottom - 1);
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
                e.CellBounds.Left + e.CellBounds.Width / 3,
                e.CellBounds.Top + 3,
                e.CellBounds.Width * 2 / 3 - 7,
                e.CellBounds.Height / 2 - 3);
            Rectangle bottomLeft = new(
                e.CellBounds.Left + 6,
                e.CellBounds.Top + e.CellBounds.Height / 2,
                e.CellBounds.Width * 2 / 3 - 6,
                e.CellBounds.Height / 2 - 3);

            Color topColor = amounts.TopRightInactive ? Color.Red : Color.Black;
            Color bottomColor = amounts.BottomLeftInactive ? Color.Red : Color.Black;
            using var topFont = new Font(
                "맑은 고딕",
                9F,
                amounts.TopRightInactive ? FontStyle.Bold : FontStyle.Regular);
            using var bottomFont = new Font(
                "맑은 고딕",
                9F,
                amounts.BottomLeftInactive ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                amounts.TopRightAmount,
                topFont,
                topRight,
                topColor,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                e.Graphics,
                amounts.BottomLeftAmount,
                bottomFont,
                bottomLeft,
                bottomColor,
                TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
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
                $"{product} · {valueName} · {DateTime.Now:yyyy-MM-dd HH:mm}",
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
            $"{valueName} · 오른쪽 위: 열 기관(Creditor) → 행 기관(Debtor) / " +
            "왼쪽 아래: 행 기관 → 열 기관 · 값 없음은 0 · 셀 클릭: 가로/세로 강조";
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

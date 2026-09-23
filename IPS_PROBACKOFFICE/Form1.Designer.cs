namespace IPSEBO
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private TabControl tabs = null!;
        private TabPage bokTab = null!;
        private TabPage kbTab = null!;
        private TabPage rateTab = null!;
        private DateTimePicker dateTimePicker1 = null!;
        private DateTimePicker dateTimePicker2 = null!;
        private ComboBox kbRoundComboBox = null!;
        private CheckBox kbIntervalCheckBox = null!;
        private CheckBox kbCumulativeCheckBox = null!;
        private DataGridView dataGridView1 = null!;
        private DataGridView dataGridView2 = null!;
        private TextBox textBox1 = null!;
        private TextBox textBox2 = null!;
        private TextBox textBox3 = null!;
        private TextBox textBox4 = null!;
        private TextBox textBox5 = null!;
        private TextBox textBox6 = null!;
        private TextBox textBox7 = null!;
        private CheckBox allRatesCheckBox = null!;
        private TextBox commonRateTextBox = null!;
        private TextBox commonPointsTextBox = null!;
        private DateTimePicker nyCloseHistoryDate = null!;
        private DataGridView nyCloseHistoryGrid = null!;
        private Label nyCloseHistoryStatus = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            tabs = new TabControl();
            bokTab = new TabPage("BOK");
            kbTab = new TabPage("KB EOD");
            rateTab = new TabPage("뉴욕종가");
            var bokLayout = new TableLayoutPanel();
            var kbLayout = new TableLayoutPanel();
            var bokToolbar = new FlowLayoutPanel();
            var kbToolbar = new FlowLayoutPanel();
            var ratePanel = new FlowLayoutPanel();
            var rateLayout = new TableLayoutPanel();
            var historyToolbar = new FlowLayoutPanel();
            var btnNyCloseHistory = new Button();
            var btnLoad = new Button();
            var btnBokExcel = new Button();
            var btnKbEod = new Button();
            var btnKbExcel = new Button();
            var btnKbEmail = new Button();
            var btnSftpTest = new Button();
            var btnFtpSend = new Button();
            var button1 = new Button();
            dateTimePicker1 = new DateTimePicker();
            dateTimePicker2 = new DateTimePicker();
            kbRoundComboBox = new ComboBox();
            kbIntervalCheckBox = new CheckBox();
            kbCumulativeCheckBox = new CheckBox();
            dataGridView1 = new DataGridView();
            dataGridView2 = new DataGridView();
            textBox1 = new TextBox();
            textBox2 = new TextBox();
            textBox3 = new TextBox();
            textBox4 = new TextBox();
            textBox5 = new TextBox();
            textBox6 = new TextBox();
            textBox7 = new TextBox();
            allRatesCheckBox = new CheckBox();
            commonRateTextBox = new TextBox();
            commonPointsTextBox = new TextBox();
            nyCloseHistoryDate = new DateTimePicker();
            nyCloseHistoryGrid = new DataGridView();
            nyCloseHistoryStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nyCloseHistoryGrid).BeginInit();
            SuspendLayout();

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(bokTab);
            tabs.TabPages.Add(kbTab);
            tabs.TabPages.Add(rateTab);

            bokLayout.ColumnCount = 1;
            bokLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bokLayout.Dock = DockStyle.Fill;
            bokLayout.RowCount = 2;
            bokLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bokLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            kbLayout.ColumnCount = 1;
            kbLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kbLayout.Dock = DockStyle.Fill;
            kbLayout.RowCount = 2;
            kbLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            kbLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bokToolbar.AutoSize = true;
            bokToolbar.Dock = DockStyle.Fill;
            bokToolbar.Padding = new Padding(6);
            bokToolbar.Controls.Add(new Label { Text = "BOK 기준일", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            dateTimePicker1.Format = DateTimePickerFormat.Short;
            bokToolbar.Controls.Add(dateTimePicker1);
            btnLoad.Text = "BOK 조회";
            btnLoad.AutoSize = true;
            btnLoad.Click += btnLoad_Click;
            bokToolbar.Controls.Add(btnLoad);
            btnBokExcel.Text = "엑셀 저장";
            btnBokExcel.AutoSize = true;
            btnBokExcel.Click += btnBokExcel_Click;
            bokToolbar.Controls.Add(btnBokExcel);
            bokToolbar.Controls.Add(bokValidationLabel);

            dataGridView1.Name = "dataGridView1";
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.ReadOnly = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            kbToolbar.AutoSize = true;
            kbToolbar.Dock = DockStyle.Fill;
            kbToolbar.Padding = new Padding(6);
            kbToolbar.Controls.Add(new Label { Text = "KB EOD 기준일", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            dateTimePicker2.Format = DateTimePickerFormat.Short;
            dateTimePicker2.ValueChanged += KbFilter_Changed;
            kbToolbar.Controls.Add(dateTimePicker2);
            kbToolbar.Controls.Add(new Label { Text = "전송 회차", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
            kbRoundComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            kbRoundComboBox.Width = 105;
            kbRoundComboBox.Items.AddRange(new object[] { "18:00", "익일 02:00", "익일 06:00" });
            kbRoundComboBox.SelectedIndex = 0;
            kbRoundComboBox.SelectedIndexChanged += KbFilter_Changed;
            kbToolbar.Controls.Add(kbRoundComboBox);
            kbIntervalCheckBox.Text = "구간별";
            kbIntervalCheckBox.AutoSize = true;
            kbIntervalCheckBox.Checked = true;
            kbIntervalCheckBox.Margin = new Padding(10, 6, 3, 0);
            kbIntervalCheckBox.CheckedChanged += KbIntervalCheckBox_CheckedChanged;
            kbToolbar.Controls.Add(kbIntervalCheckBox);
            kbCumulativeCheckBox.Text = "누적";
            kbCumulativeCheckBox.AutoSize = true;
            kbCumulativeCheckBox.Margin = new Padding(3, 6, 6, 0);
            kbCumulativeCheckBox.CheckedChanged += KbCumulativeCheckBox_CheckedChanged;
            kbToolbar.Controls.Add(kbCumulativeCheckBox);
            btnKbEod.Text = "KB EOD 조회";
            btnKbEod.AutoSize = true;
            btnKbEod.Click += btnKbEod_Click_1;
            kbToolbar.Controls.Add(btnKbEod);
            btnKbExcel.Text = "엑셀 저장";
            btnKbExcel.AutoSize = true;
            btnKbExcel.Click += btnKbExcel_Click;
            kbToolbar.Controls.Add(btnKbExcel);
            btnKbEmail.Text = "메일 발송";
            btnKbEmail.AutoSize = true;
            btnKbEmail.Click += btnKbEmail_Click;
            kbToolbar.Controls.Add(btnKbEmail);

            dataGridView2.Name = "dataGridView2";
            dataGridView2.Dock = DockStyle.Fill;
            dataGridView2.ReadOnly = true;
            dataGridView2.AllowUserToAddRows = false;
            dataGridView2.AllowUserToDeleteRows = false;
            dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            ratePanel.Dock = DockStyle.Top;
            ratePanel.AutoSize = true;
            ratePanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ratePanel.Padding = new Padding(14);
            ratePanel.WrapContents = true;
            allRatesCheckBox.Text = "전체 입력 (해제 시 개별 입력)";
            allRatesCheckBox.AutoSize = true;
            allRatesCheckBox.Checked = true;
            allRatesCheckBox.Margin = new Padding(3, 8, 12, 0);
            allRatesCheckBox.CheckedChanged += AllRatesCheckBox_CheckedChanged;
            ratePanel.Controls.Add(allRatesCheckBox);
            ratePanel.Controls.Add(new Label { Text = "환율", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            commonRateTextBox.Width = 90;
            commonRateTextBox.TextChanged += CommonRates_TextChanged;
            ratePanel.Controls.Add(commonRateTextBox);
            ratePanel.Controls.Add(new Label { Text = "포인트", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
            commonPointsTextBox.Width = 90;
            commonPointsTextBox.TextChanged += CommonRates_TextChanged;
            ratePanel.Controls.Add(commonPointsTextBox);
            ratePanel.SetFlowBreak(commonPointsTextBox, true);
            ratePanel.Controls.Add(new Label { Text = "DF", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            foreach (var box in new[] { textBox1, textBox2, textBox3, textBox4, textBox5, textBox6 })
                box.Width = 90;
            ratePanel.Controls.Add(textBox1);
            ratePanel.Controls.Add(textBox2);
            ratePanel.Controls.Add(textBox3);
            ratePanel.Controls.Add(new Label { Text = "NDF", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
            ratePanel.Controls.Add(textBox4);
            ratePanel.Controls.Add(textBox5);
            ratePanel.Controls.Add(textBox6);
            button1.Text = "환율 저장";
            button1.AutoSize = true;
            button1.Click += button1_Click;
            ratePanel.Controls.Add(button1);
            ratePanel.Controls.Add(new Label { Text = "FTP 경로", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
            textBox7.Width = 220;
            ratePanel.Controls.Add(textBox7);
            btnSftpTest.Text = "SFTP 접속 테스트";
            btnSftpTest.AutoSize = true;
            btnSftpTest.Click += btnSftpTest_Click;
            ratePanel.Controls.Add(btnSftpTest);
            btnFtpSend.Text = "FTP 전송";
            btnFtpSend.AutoSize = true;
            btnFtpSend.Click += btnFtpSend_Click;
            ratePanel.Controls.Add(btnFtpSend);

            rateLayout.ColumnCount = 1;
            rateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rateLayout.Dock = DockStyle.Fill;
            rateLayout.RowCount = 3;
            rateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            historyToolbar.Dock = DockStyle.Fill;
            historyToolbar.AutoSize = true;
            historyToolbar.Padding = new Padding(14, 16, 14, 8);
            historyToolbar.Controls.Add(new Label
            {
                Text = "뉴욕종가 입력내역",
                AutoSize = true,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                Margin = new Padding(3, 8, 12, 0)
            });
            historyToolbar.Controls.Add(new Label { Text = "기준일", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
            nyCloseHistoryDate.Format = DateTimePickerFormat.Short;
            nyCloseHistoryDate.Width = 120;
            historyToolbar.Controls.Add(nyCloseHistoryDate);
            btnNyCloseHistory.Text = "조회";
            btnNyCloseHistory.AutoSize = true;
            btnNyCloseHistory.Click += btnNyCloseHistory_Click;
            historyToolbar.Controls.Add(btnNyCloseHistory);
            nyCloseHistoryStatus.AutoSize = true;
            nyCloseHistoryStatus.ForeColor = Color.DimGray;
            nyCloseHistoryStatus.Margin = new Padding(12, 8, 3, 0);
            historyToolbar.Controls.Add(nyCloseHistoryStatus);

            nyCloseHistoryGrid.Dock = DockStyle.Left;
            nyCloseHistoryGrid.Width = 640;
            nyCloseHistoryGrid.MinimumSize = new Size(640, 0);
            nyCloseHistoryGrid.Margin = new Padding(17, 3, 3, 3);
            nyCloseHistoryGrid.ReadOnly = true;
            nyCloseHistoryGrid.AllowUserToAddRows = false;
            nyCloseHistoryGrid.AllowUserToDeleteRows = false;
            nyCloseHistoryGrid.AllowUserToOrderColumns = true;
            nyCloseHistoryGrid.RowHeadersVisible = false;
            nyCloseHistoryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            nyCloseHistoryGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            nyCloseHistoryGrid.BackgroundColor = Color.White;

            rateLayout.Controls.Add(ratePanel, 0, 0);
            rateLayout.Controls.Add(historyToolbar, 0, 1);
            rateLayout.Controls.Add(nyCloseHistoryGrid, 0, 2);

            bokLayout.Controls.Add(bokToolbar, 0, 0);
            bokLayout.Controls.Add(dataGridView1, 0, 1);
            kbLayout.Controls.Add(kbToolbar, 0, 0);
            kbLayout.Controls.Add(dataGridView2, 0, 1);
            bokTab.Controls.Add(bokLayout);
            kbTab.Controls.Add(kbLayout);
            rateTab.Controls.Add(rateLayout);
            Controls.Add(tabs);
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1240, 695);
            Name = "Form1";
            Text = "BOK · KB EOD · 뉴욕종가";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)nyCloseHistoryGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion
    }
}

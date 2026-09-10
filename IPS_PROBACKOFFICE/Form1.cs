using ClosedXML.Excel;
using MySqlConnector;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Windows.Forms;
using Renci.SshNet;
using IPS_PROBACKOFFICE;

namespace IPSEBO
{
    public enum OperationsScreen
    {
        All,
        Bok,
        KbEod,
        NewYorkClose
    }

    public partial class Form1 : Form
    {
        // DB 접속정보는 Program에서 검증한 백오피스 전용 계정을 전달받는다.
        private readonly string connectionString;
        private string csvFileName = "reference_rates.csv";
        private const string NyCloseAgentInbox = "/opt/ips/nyclose-agent/inbox";
        private const string FileTransferHost = "10.10.4.64";
        private const string FileTransferUsername = "root";
        private const string FileTransferPassword = "Wjswkrjfo12#";
        string localDirectory = @"Y:\전자거래사업본부\뉴욕종가";
        private bool changingKbMode;
        private DateTime? lastKbReportDate;
        private string? lastKbRound;
        private bool lastKbWasCumulative;
        private readonly BusinessDaySettings businessDaySettings;
        private readonly OperationsScreen currentScreen;

        public Form1() : this(string.Empty, new BusinessDaySettings(), OperationsScreen.All)
        {
        }

        public Form1(string connectionString, BusinessDaySettings businessDaySettings)
            : this(connectionString, businessDaySettings, OperationsScreen.All)
        {
        }

        public Form1(
            string connectionString,
            BusinessDaySettings businessDaySettings,
            OperationsScreen screen)
        {
            this.connectionString = connectionString;
            this.businessDaySettings = businessDaySettings;
            currentScreen = screen;
            Font = new Font("맑은 고딕", 10F);
            InitializeComponent();

            if (screen != OperationsScreen.All)
                ShowOnly(screen);

            // 실행 시 textBox7에 기본 경로를 미리 넣어둡니다.
            textBox7.Text = "/home/edgeWater/UpLoad";

            // 프로그램 시작 시 CSV 파일 읽기
            LoadCsvToTextBoxes();
        }

        private void ShowOnly(OperationsScreen screen)
        {
            TabPage selectedPage = screen switch
            {
                OperationsScreen.Bok => bokTab,
                OperationsScreen.KbEod => kbTab,
                OperationsScreen.NewYorkClose => rateTab,
                _ => bokTab
            };

            Text = selectedPage.Text;
            Control[] pageControls = selectedPage.Controls.Cast<Control>().ToArray();
            Controls.Remove(tabs);
            foreach (Control control in pageControls)
                Controls.Add(control);
            tabs.Dispose();
        }

        // 1. BOK report 조회
        private void btnLoad_Click(object sender, EventArgs e)
        {
            DateTime reportDate = dateTimePicker1.Value.Date;
            (DateTime startDateTime, DateTime endDateTime) = businessDaySettings.GetDailyRange(reportDate);

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    // PRODUCT 값에 따라 31, 32 치환. Reference No는 뷰의 한국시간 날짜별 BOKId를 사용한다.
                    // NDF는 매수/매도 측 행의 trade_id가 서로 다를 수 있으므로
                    // 실제 체결 특성(시각/수량/가격/결제일/테너/레그)으로 한 거래를 식별한다.
                    string ndfGroupExpression = @"CONCAT_WS('|',
                        'NDF',
                        DATE_FORMAT(
                            DATE_ADD(
                                GREATEST(
                                    COALESCE(tr.`Buy.created`, '1970-01-01'),
                                    COALESCE(tr.`Sell.created`, '1970-01-01')
                                ), INTERVAL 9 HOUR
                            ), '%Y%m%d%H%i%s'
                        ),
                        CAST(tr.`Filled Quantity` AS CHAR),
                        CAST(tr.leg_price AS CHAR),
                        COALESCE(DATE_FORMAT(tr.leg_settle_date, '%Y%m%d'), ''),
                        COALESCE(TRIM(tr.TYPE), ''),
                        COALESCE(TRIM(tr.LEG_TYPE), '')
                    )";

                    string query = $@"
        WITH bok_rows AS
        (
        SELECT
          DATE_FORMAT(
            DATE_ADD(
              GREATEST(
                COALESCE(tr.`Buy.created`, '1970-01-01'),
                COALESCE(tr.`Sell.created`, '1970-01-01')
              ), INTERVAL 9 HOUR
            ), '%Y-%m-%d'
          ) AS `ACTUAL TRADING DATE`,
          DATE_FORMAT(@reportDate, '%Y-%m-%d') AS `TRADING DATE`,
          '1' AS `Branch Code`,
          CAST(CONCAT(CHAR(96), '04542') AS CHAR) AS `Branch Adress`,
          '1' AS `New/Delete/Update`,
          CASE 
            WHEN PRODUCT = 'DF' THEN '31'
            WHEN PRODUCT = 'NDF' THEN '32'
            ELSE PRODUCT
          END AS `Product Code`,
          MIN(BOKId) OVER (
            PARTITION BY
              CASE
                WHEN UPPER(TRIM(tr.PRODUCT)) = 'NDF'
                  THEN {ndfGroupExpression}
                ELSE CONCAT('ROW:', CAST(tr.BOKId AS CHAR))
              END,
              COALESCE(TRIM(tr.LEG_TYPE), '')
          ) AS `Reference No`,
          MAX(NULLIF(TRIM(COALESCE(bi.institution_code, tr.`Buy.Executing_Organisation`)), '')) OVER (
            PARTITION BY
              CASE
                WHEN UPPER(TRIM(tr.PRODUCT)) = 'NDF'
                  THEN {ndfGroupExpression}
                ELSE CONCAT('ROW:', CAST(tr.BOKId AS CHAR))
              END,
              COALESCE(TRIM(tr.LEG_TYPE), '')
          ) AS `Buyer Code`,
          MAX(NULLIF(TRIM(COALESCE(si.institution_code, tr.`Sell.Executing_Organisation`)), '')) OVER (
            PARTITION BY
              CASE
                WHEN UPPER(TRIM(tr.PRODUCT)) = 'NDF'
                  THEN {ndfGroupExpression}
                ELSE CONCAT('ROW:', CAST(tr.BOKId AS CHAR))
              END,
              COALESCE(TRIM(tr.LEG_TYPE), '')
          ) AS `Seller Code`,
          MAX(tr.`Filled Quantity`) OVER (
            PARTITION BY
              CASE
                WHEN UPPER(TRIM(tr.PRODUCT)) = 'NDF'
                  THEN {ndfGroupExpression}
                ELSE CONCAT('ROW:', CAST(tr.BOKId AS CHAR))
              END,
              COALESCE(TRIM(tr.LEG_TYPE), '')
          ) AS `Amount`,
          'USD' AS `Basic Currency`,
          'KRW' AS `Counter Currency`,
          DATE_FORMAT(DATE_ADD(GREATEST(COALESCE(tr.`Buy.created`, '1970-01-01'), COALESCE(tr.`Sell.created`, '1970-01-01')), INTERVAL 9 HOUR), '%H%i%s') AS `Trade Time`,
          `Listed/OTC`,
          DATE_FORMAT(leg_settle_date, '%Y%m%d') AS `Settle Date`,
          '' AS `Near Value Date`,
          '' AS `Far Value Date`,
          '' AS `Maturity Date`,
          CASE
            WHEN UPPER(TYPE) = 'SN' THEN '61'
            WHEN UPPER(TYPE) = '1M' THEN '31'
            ELSE TYPE
          END AS `Tenor Code`,
          CASE
            WHEN COALESCE(TRIM(tr.LEG_TYPE), '') = '' THEN COALESCE(
              buy_order.fill_price_final,
              sell_order.fill_price_final,
              buy_order.fill_price_vwap_client,
              sell_order.fill_price_vwap_client,
              buy_order.fill_price_vwap,
              sell_order.fill_price_vwap,
              tr.leg_price
            )
            ELSE tr.leg_price
          END AS `Exchange Rate`,
          '' AS `Near Leg Rate`,
          '' AS `Far Leg Rate`,
          tr.trade_id AS `__trade_id`,
          COALESCE(TRIM(tr.LEG_TYPE), '') AS `__leg_type`,
          ROW_NUMBER() OVER (
            PARTITION BY
              CASE
                WHEN UPPER(TRIM(tr.PRODUCT)) = 'NDF'
                  THEN {ndfGroupExpression}
                ELSE CONCAT('ROW:', CAST(tr.BOKId AS CHAR))
              END,
              COALESCE(TRIM(tr.LEG_TYPE), '')
            ORDER BY
              CASE
                WHEN NULLIF(TRIM(COALESCE(bi.institution_code, tr.`Buy.Executing_Organisation`)), '') IS NOT NULL
                 AND NULLIF(TRIM(COALESCE(si.institution_code, tr.`Sell.Executing_Organisation`)), '') IS NOT NULL
                  THEN 0
                ELSE 1
              END,
              tr.BOKId ASC
          ) AS `__row_no`
        FROM ips_ew.v_trading_report tr
        LEFT JOIN mex_prod.orders buy_order
          ON buy_order.client_order_id = tr.`Buy.client_order_id`
        LEFT JOIN mex_prod.orders sell_order
          ON sell_order.client_order_id = tr.`Sell.client_order_id`
        LEFT JOIN mextrader_admin_prod.organizations bo ON tr.`Buy.Executing_Organisation` = bo.name
        LEFT JOIN ips_ew.institution_local_info bi ON bi.external_institution_id = bo.id
        LEFT JOIN mextrader_admin_prod.organizations so ON tr.`Sell.Executing_Organisation` = so.name
        LEFT JOIN ips_ew.institution_local_info si ON si.external_institution_id = so.id
        WHERE leg_price IS NOT NULL
          AND DATE_ADD(
                GREATEST(
                    COALESCE(tr.`Buy.created`, '1970-01-01'),
                    COALESCE(tr.`Sell.created`, '1970-01-01')
                ), INTERVAL 9 HOUR
              ) >= @startDateTime
          AND DATE_ADD(
                GREATEST(
                    COALESCE(tr.`Buy.created`, '1970-01-01'),
                    COALESCE(tr.`Sell.created`, '1970-01-01')
                ), INTERVAL 9 HOUR
              ) < @endDateTime
        )
        SELECT
          `ACTUAL TRADING DATE`,
          `TRADING DATE`,
          `Branch Code`,
          `Branch Adress`,
          `New/Delete/Update`,
          `Product Code`,
          `Reference No`,
          CASE WHEN `Buyer Code` IN ('1123-1', '1123-2')
            THEN '1123' ELSE `Buyer Code` END AS `Buyer Code`,
          CASE WHEN `Seller Code` IN ('1123-1', '1123-2')
            THEN '1123' ELSE `Seller Code` END AS `Seller Code`,
          `Amount`,
          `Basic Currency`,
          `Counter Currency`,
          `Trade Time`,
          `Listed/OTC`,
          `Settle Date`,
          `Near Value Date`,
          `Far Value Date`,
          `Maturity Date`,
          `Tenor Code`,
          `Exchange Rate`,
          `Near Leg Rate`,
          `Far Leg Rate`
        FROM bok_rows
        WHERE `__row_no` = 1
        ORDER BY `Reference No` ASC, `__trade_id` ASC, FIELD(`__leg_type`, 'NEAR', 'FAR');";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@reportDate", reportDate);
                        cmd.Parameters.AddWithValue("@startDateTime", startDateTime);
                        cmd.Parameters.AddWithValue("@endDateTime", endDateTime);

                        DataTable dt = new DataTable();
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        dataGridView1.DataSource = dt;
                        dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("BOK 조회 중 오류: " + ex.Message);
                }
            }
        }

        // 2. KB EOD 조회
        private void btnKbEod_Click_1(object sender, EventArgs e)
        {
            DateTime reportDate = dateTimePicker2.Value.Date;
            string selectedRound = kbRoundComboBox.SelectedItem?.ToString() ?? "18:00";
            bool cumulative = kbCumulativeCheckBox.Checked;
            (DateTime startDateTime, DateTime endDateTime) =
                GetKbDateTimeRange(reportDate, selectedRound, cumulative);

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    DataTable kbDt = new DataTable();

                    // mextrader_admin_prod.users의 full_name을 영문 딜러명으로 사용
                    string kbQuery = @"
        SELECT 
            tr.PRODUCT AS `Product`, 
            CASE 
                WHEN 'KB Seoul' IN (
                    tr.`Buy.Executing_Organisation`,
                    tr.`Sell.Executing_Organisation`
                ) THEN CONCAT(
                    ROW_NUMBER() OVER (
                        PARTITION BY tr.PRODUCT
                        ORDER BY
                            GREATEST(
                                COALESCE(tr.`Buy.created`, '1970-01-01'),
                                COALESCE(tr.`Buy.modified`, '1970-01-01'),
                                COALESCE(tr.`Sell.created`, '1970-01-01'),
                                COALESCE(tr.`Sell.modified`, '1970-01-01')
                            ) ASC,
                            tr.BOKId ASC
                    ),
                    '. ',
                    CASE
                        WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul'
                            THEN 'KB SELL'
                        WHEN tr.`Buy.Executing_Organisation` = 'KB Seoul'
                            THEN 'KB BUY'
                    END
                )
                ELSE 'UNKNOWN'
            END AS `Direction`,
            CONCAT('US$ ', FORMAT(tr.`Original Quantity` / 1000000, 0), ' mio') AS `Amt`,
            tr.symbol AS `ccy`,
            FORMAT(
                CASE
                    WHEN COALESCE(TRIM(tr.LEG_TYPE), '') = '' THEN COALESCE(
                        buy_order.fill_price_final,
                        sell_order.fill_price_final,
                        buy_order.fill_price_vwap_client,
                        sell_order.fill_price_vwap_client,
                        buy_order.fill_price_vwap,
                        sell_order.fill_price_vwap,
                        tr.price
                    )
                    ELSE tr.price
                END,
                2
            ) AS `all-in-rate`,
            DATE_FORMAT(tr.`Settle Date`, '%d-%b-%Y') AS `val date`,
            CASE 
                WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul' THEN 
                    CASE tr.`Buy.Executing_Organisation`
                        WHEN 'SCB London' THEN 'SCB LDN'
                        WHEN 'DBS SINGAPORE' THEN 'DBS'
                        ELSE tr.`Buy.Executing_Organisation`
                    END
                WHEN tr.`Buy.Executing_Organisation` = 'KB Seoul' THEN 
                    CASE tr.`Sell.Executing_Organisation`
                        WHEN 'SCB London' THEN 'SCB LDN'
                        WHEN 'DBS SINGAPORE' THEN 'DBS'
                        ELSE tr.`Sell.Executing_Organisation`
                    END
            END AS `counterparty`,
            tr.TYPE AS `Tenor`, 
            CASE 
                WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul' THEN tr.`Sell.client_order_id`
                WHEN tr.`Buy.Executing_Organisation` = 'KB Seoul' THEN tr.`Buy.client_order_id`
            END AS `Ref.`,
            CASE 
                WHEN EXISTS (
                    SELECT 1
                    FROM ips_ew.api_fee_exemption afe
                    WHERE afe.is_active = 1
                      AND afe.institution_code = @kbInstitutionCode
                      AND LOWER(TRIM(afe.trader_id)) = LOWER(TRIM(
                          CASE
                              WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul'
                                  THEN tr.`Sell.Executing_Trader`
                              ELSE tr.`Buy.Executing_Trader`
                          END
                      ))
                      AND DATE_ADD(
                        GREATEST(
                            COALESCE(tr.`Buy.created`, '1970-01-01'),
                            COALESCE(tr.`Buy.modified`, '1970-01-01'),
                            COALESCE(tr.`Sell.created`, '1970-01-01'),
                            COALESCE(tr.`Sell.modified`, '1970-01-01')
                        ), INTERVAL 9 HOUR
                      ) >= afe.start_datetime
                      AND DATE_ADD(
                        GREATEST(
                            COALESCE(tr.`Buy.created`, '1970-01-01'),
                            COALESCE(tr.`Buy.modified`, '1970-01-01'),
                            COALESCE(tr.`Sell.created`, '1970-01-01'),
                            COALESCE(tr.`Sell.modified`, '1970-01-01')
                        ), INTERVAL 9 HOUR
                      ) < afe.end_datetime
                )
                    THEN '0'
                WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul' THEN FORMAT(IFNULL(tr.`Sell.brokerage_commission`, 0), 0)
                WHEN tr.`Buy.Executing_Organisation` = 'KB Seoul' THEN FORMAT(IFNULL(tr.`Buy.brokerage_commission`, 0), 0)
                ELSE '0'
            END AS `Bro.(KRW)`,
            CASE
                WHEN CASE WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul' THEN tr.`Sell.Executing_Trader` ELSE tr.`Buy.Executing_Trader` END = 'kb11' THEN 'kb11 사용불가'
                ELSE COALESCE(
                    NULLIF(TRIM(u.full_name), ''),
                    CASE WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul'
                         THEN tr.`Sell.Executing_Trader`
                         ELSE tr.`Buy.Executing_Trader` END
                )
            END AS `Trader`
        FROM 
            ips_ew.v_trading_report tr
        LEFT JOIN
            mex_prod.orders buy_order
                ON buy_order.client_order_id = tr.`Buy.client_order_id`
        LEFT JOIN
            mex_prod.orders sell_order
                ON sell_order.client_order_id = tr.`Sell.client_order_id`
        LEFT JOIN
            mextrader_admin_prod.users u ON u.username = CASE WHEN tr.`Sell.Executing_Organisation` = 'KB Seoul' THEN tr.`Sell.Executing_Trader` ELSE tr.`Buy.Executing_Trader` END
        WHERE
            ('KB Seoul' IN (tr.`Buy.Executing_Organisation`, tr.`Sell.Executing_Organisation`))
            AND DATE_ADD(
                GREATEST(
                    COALESCE(tr.`Buy.created`, '1970-01-01'),
                    COALESCE(tr.`Buy.modified`, '1970-01-01'),
                    COALESCE(tr.`Sell.created`, '1970-01-01'),
                    COALESCE(tr.`Sell.modified`, '1970-01-01')
                ), INTERVAL 9 HOUR
            ) >= @startDateTime
            AND DATE_ADD(
                GREATEST(
                    COALESCE(tr.`Buy.created`, '1970-01-01'),
                    COALESCE(tr.`Buy.modified`, '1970-01-01'),
                    COALESCE(tr.`Sell.created`, '1970-01-01'),
                    COALESCE(tr.`Sell.modified`, '1970-01-01')
                ), INTERVAL 9 HOUR
            ) < @endDateTime
        ORDER BY 
            tr.PRODUCT ASC,
            GREATEST(
                COALESCE(tr.`Buy.created`, '1970-01-01'),
                COALESCE(tr.`Buy.modified`, '1970-01-01'),
                COALESCE(tr.`Sell.created`, '1970-01-01'),
                COALESCE(tr.`Sell.modified`, '1970-01-01')
            ) ASC,
            tr.BOKId ASC;";

                    using (MySqlCommand cmd = new MySqlCommand(kbQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDateTime", startDateTime);
                        cmd.Parameters.AddWithValue("@endDateTime", endDateTime);
                        cmd.Parameters.AddWithValue("@kbInstitutionCode", "1104");
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(kbDt);
                        }
                    }

                    dataGridView2.DataSource = kbDt;
                    dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                    lastKbReportDate = reportDate;
                    lastKbRound = selectedRound;
                    lastKbWasCumulative = cumulative;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("KB 조회 중 오류: " + ex.Message);
                }
            }
        }

        private static (DateTime Start, DateTime End) GetKbDateTimeRange(
            DateTime reportDate,
            string selectedRound,
            bool cumulative)
        {
            DateTime baseStart = reportDate.Date.AddHours(6);

            return selectedRound switch
            {
                "18:00" => (baseStart, reportDate.Date.AddHours(18)),
                "익일 02:00" => (
                    cumulative ? baseStart : reportDate.Date.AddHours(18),
                    reportDate.Date.AddDays(1).AddHours(2)),
                "익일 06:00" => (
                    cumulative ? baseStart : reportDate.Date.AddDays(1).AddHours(2),
                    reportDate.Date.AddDays(1).AddHours(6)),
                _ => throw new InvalidOperationException("KB EOD 전송 회차가 올바르지 않습니다.")
            };
        }

        private void KbIntervalCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (changingKbMode)
                return;

            changingKbMode = true;
            if (kbIntervalCheckBox.Checked)
                kbCumulativeCheckBox.Checked = false;
            else if (!kbCumulativeCheckBox.Checked)
                kbIntervalCheckBox.Checked = true;
            changingKbMode = false;
            ClearKbResult();
        }

        private void KbCumulativeCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (changingKbMode)
                return;

            changingKbMode = true;
            if (kbCumulativeCheckBox.Checked)
                kbIntervalCheckBox.Checked = false;
            else if (!kbIntervalCheckBox.Checked)
                kbCumulativeCheckBox.Checked = true;
            changingKbMode = false;
            ClearKbResult();
        }

        private void KbFilter_Changed(object? sender, EventArgs e)
        {
            ClearKbResult();
        }

        private void ClearKbResult()
        {
            if (dataGridView2 is null)
                return;

            dataGridView2.DataSource = null;
            lastKbReportDate = null;
            lastKbRound = null;
        }

        // 3. CSV 파일 관리 및 FTP 전송 
        private void LoadCsvToTextBoxes()
        {
            try
            {
                string filePath = Path.Combine(Application.StartupPath, csvFileName);
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    if (lines.Length >= 2)
                    {
                        var row1 = lines[0].Replace("\"", "").Split(',');
                        var row2 = lines[1].Replace("\"", "").Split(',');
                        if (row1.Length >= 5) { textBox1.Text = row1[2]; textBox2.Text = row1[3]; textBox3.Text = row1[4]; }
                        if (row2.Length >= 5) { textBox4.Text = row2[2]; textBox5.Text = row2[3]; textBox6.Text = row2[4]; }
                    }
                }
            }
            catch { }
        }

        private async void btnSftpTest_Click(object sender, EventArgs e)
        {
            var testButton = sender as Button;
            if (testButton is not null)
                testButton.Enabled = false;

            try
            {
                string folderPath = GetTransferFolderPath();
                await Task.Run(() =>
                {
                    using var client = new SftpClient(FileTransferHost, FileTransferUsername, FileTransferPassword);
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    client.Connect();
                    _ = client.GetAttributes(folderPath);
                    client.Disconnect();
                });

                MessageBox.Show(
                    $"SFTP 접속 성공\n{FileTransferHost}:22\n{folderPath}",
                    "SFTP 접속 테스트",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"SFTP 접속 실패\n{ex.Message}",
                    "SFTP 접속 테스트",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (testButton is not null)
                    testButton.Enabled = true;
            }
        }

        private void btnFtpSend_Click(object sender, EventArgs e)
        {
            try
            {
                string localPath = Path.Combine(localDirectory, csvFileName);

                if (!File.Exists(localPath))
                {
                    MessageBox.Show("먼저 '저장' 버튼을 눌러 파일을 생성해주세요.");
                    return;
                }

                string folderPath = GetTransferFolderPath();

                string remotePath = CombineRemotePath(folderPath, csvFileName);
                string agentPath = CombineRemotePath(NyCloseAgentInbox, csvFileName);

                using (var client = new SftpClient(FileTransferHost, FileTransferUsername, FileTransferPassword))
                {
                    client.Connect();
                    using (var fs = new FileStream(localPath, FileMode.Open))
                    {
                        client.UploadFile(fs, remotePath, true);
                    }

                    // nyclose-agent가 사용하는 CSV 템플릿도 같은 파일로 갱신한다.
                    using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        client.UploadFile(fs, agentPath, true);
                    }
                    client.Disconnect();
                }

                MessageBox.Show(
                    $"서버 전송 및 덮어쓰기 완료!\n\n" +
                    $"기존 경로: {remotePath}\n" +
                    $"NY Close Agent: {agentPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"전송 에러: {ex.Message}");
            }
        }

        private void btnBokExcel_Click(object sender, EventArgs e)
        {
            ExportGridToExcel(
                dataGridView1,
                dateTimePicker1.Value,
                "BOK_Report",
                removeTradingDate: true);
        }

        private static string CombineRemotePath(string directory, string fileName)
        {
            string normalized = directory.Trim().TrimEnd('/');
            return normalized + "/" + fileName;
        }

        private string GetTransferFolderPath()
        {
            string folderPath = textBox7.Text.Trim();
            return string.IsNullOrEmpty(folderPath) ? "/home/edgeWater/Templates" : folderPath;
        }

        private void btnKbExcel_Click(object sender, EventArgs e)
        {
            if (!IsCurrentKbResult())
            {
                MessageBox.Show("조회 조건이 변경되었습니다. KB EOD 조회를 다시 실행해주세요.");
                return;
            }

            ExportGridToExcel(
                dataGridView2,
                dateTimePicker2.Value,
                $"KB_EOD_{GetKbRoundFileText()}_{GetKbModeText()}",
                removeTradingDate: false);
        }

        private void btnKbEmail_Click(object sender, EventArgs e)
        {
            if (!IsCurrentKbResult())
            {
                MessageBox.Show("조회 조건이 변경되었습니다. KB EOD 조회를 다시 실행해주세요.");
                return;
            }

            if (dataGridView2.DataSource is not DataTable original)
            {
                MessageBox.Show("먼저 KB EOD 조회를 실행해주세요.");
                return;
            }

            DataTable exportData = original.Copy();

            using var dialog = new KbEodEmailDialog(
                dateTimePicker2.Value,
                exportData);
            dialog.ShowDialog(this);
        }

        private bool IsCurrentKbResult()
        {
            return dataGridView2.DataSource is DataTable
                && lastKbReportDate == dateTimePicker2.Value.Date
                && lastKbRound == (kbRoundComboBox.SelectedItem?.ToString() ?? "18:00")
                && lastKbWasCumulative == kbCumulativeCheckBox.Checked;
        }

        private string GetKbRoundFileText()
        {
            return (kbRoundComboBox.SelectedItem?.ToString() ?? "18:00")
                .Replace("익일 ", string.Empty)
                .Replace(":", string.Empty);
        }

        private string GetKbModeText() =>
            kbCumulativeCheckBox.Checked ? "누적" : "구간별";

        private void ExportGridToExcel(
            DataGridView grid,
            DateTime reportDate,
            string prefix,
            bool removeTradingDate)
        {
            if (grid.DataSource is not DataTable original)
            {
                MessageBox.Show("먼저 조회를 실행해주세요.");
                return;
            }

            try
            {
                DataTable exportData = original.Copy();
                if (removeTradingDate
                    && exportData.Columns.Contains("ACTUAL TRADING DATE"))
                    exportData.Columns.Remove("ACTUAL TRADING DATE");

                if (removeTradingDate
                    && exportData.Columns.Contains("TRADING DATE"))
                    exportData.Columns.Remove("TRADING DATE");

                using var workbook = new XLWorkbook();
                workbook.Worksheets.Add(exportData, "Sheet1");

                string fileName =
                    $"{prefix}_{reportDate:yyyyMMdd}.xlsx";
                string filePath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop),
                    fileName);

                workbook.SaveAs(filePath);
                MessageBox.Show(
                    $"바탕화면에 저장되었습니다: {fileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("엑셀 저장 오류: " + ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"\"USD/KRW,DF 1M,{textBox1.Text.Trim()},{textBox2.Text.Trim()},{textBox3.Text.Trim()},7899\"");
                sb.AppendLine($"\"USD/KRW,NDF 1M,{textBox4.Text.Trim()},{textBox5.Text.Trim()},{textBox6.Text.Trim()},7801\"");
                string csvContent = sb.ToString();

                if (!Directory.Exists(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                string localPath = Path.Combine(localDirectory, csvFileName);

                Properties.Settings.Default.LastValue1 = textBox1.Text;
                Properties.Settings.Default.LastValue2 = textBox2.Text;
                Properties.Settings.Default.LastValue3 = textBox3.Text;
                Properties.Settings.Default.LastValue4 = textBox4.Text;
                Properties.Settings.Default.LastValue5 = textBox5.Text;
                Properties.Settings.Default.LastValue6 = textBox6.Text;
                Properties.Settings.Default.Save();

                File.WriteAllText(localPath, csvContent, new UTF8Encoding(false));

                MessageBox.Show($"로컬 저장 완료: {localPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 에러: {ex.Message}");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = Properties.Settings.Default.LastValue1;
            textBox2.Text = Properties.Settings.Default.LastValue2;
            textBox3.Text = Properties.Settings.Default.LastValue3;
            textBox4.Text = Properties.Settings.Default.LastValue4;
            textBox5.Text = Properties.Settings.Default.LastValue5;
            textBox6.Text = Properties.Settings.Default.LastValue6;
            if (currentScreen is OperationsScreen.All or OperationsScreen.NewYorkClose)
            {
                nyCloseHistoryDate.Value = DateTime.Today;
                LoadNyCloseHistory();
            }
        }

        private void btnNyCloseHistory_Click(object sender, EventArgs e)
        {
            LoadNyCloseHistory();
        }

        private void LoadNyCloseHistory()
        {
            // DB의 modified는 UTC로 저장되므로 사용자가 선택한 KST 하루를
            // UTC 조회 범위로 변환한다. 예: KST 08/25 -> UTC 08/24 15:00 ~ 08/25 15:00
            DateTime selectedKstDate = nyCloseHistoryDate.Value.Date;
            DateTime fromUtc = DateTime.SpecifyKind(
                selectedKstDate.AddHours(-9), DateTimeKind.Unspecified);
            DateTime toUtc = fromUtc.AddDays(1);

            const string sql = @"
                SELECT
                    DATE_ADD(modified, INTERVAL 9 HOUR) AS `시간`,
                    price AS `환율`,
                    swap_points AS `SWAP POINTS`
                   FROM mex_prod.matcher_active_quotes
                WHERE modified >= @from
                  AND modified < @to
                ORDER BY modified DESC, instrument_id, side;";

            try
            {
                using var connection = new MySqlConnection(connectionString);
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@from", fromUtc);
                command.Parameters.AddWithValue("@to", toUtc);
                using var adapter = new MySqlDataAdapter(command);
                var table = new DataTable();
                adapter.Fill(table);
                nyCloseHistoryGrid.DataSource = table;
                nyCloseHistoryStatus.Text = $"{selectedKstDate:yyyy-MM-dd} · {table.Rows.Count:N0}건";

                if (nyCloseHistoryGrid.Columns["시간"] is DataGridViewColumn modifiedColumn)
                {
                    modifiedColumn.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss.fff";
                    modifiedColumn.FillWeight = 50;
                }
                foreach (string columnName in new[] { "환율", "SWAP POINTS" })
                    if (nyCloseHistoryGrid.Columns[columnName] is DataGridViewColumn column)
                    {
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        column.DefaultCellStyle.Format = "N3";
                        column.FillWeight = 25;
                    }
            }
            catch (Exception ex)
            {
                nyCloseHistoryStatus.Text = "조회 실패";
                MessageBox.Show("뉴욕종가 입력내역 조회 중 오류: " + ex.Message);
            }
        }
    }
}

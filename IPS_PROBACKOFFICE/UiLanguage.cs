using System.Text;

namespace IPS_PROBACKOFFICE;

internal static class UiLanguage
{
    private static readonly string SettingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPS_PROBACKOFFICE",
        "ui-language.txt");

    private static readonly Dictionary<string, string> KoreanToEnglish = new(StringComparer.Ordinal)
    {
        ["영업일 종료시간"] = "Business Day End",
        ["(당일 08시~익일 선택시간 · BOK/대표이사 보고/Invoice)"] = "(08:00 to selected time next day · BOK/Executive Report/Invoice)",
        ["비밀번호 변경"] = "Change Password",
        ["백오피스 DB 접속 설정"] = "Backoffice Database Connection",
        ["백오피스 전용 DB 계정"] = "Backoffice Database Account",
        ["DB 사용자"] = "Database User",
        ["DB 비밀번호"] = "Database Password",
        ["연결 및 저장"] = "Connect and Save",
        ["로그아웃"] = "Sign Out",
        ["리포트"] = "REPORTS",
        ["수수료"] = "FEES",
        ["기준정보"] = "MASTER DATA",
        ["시스템"] = "SYSTEM",
        ["BOK · KB EOD · 뉴욕종가"] = "BOK · KB EOD · New York Close",
        ["기관 · 딜러 관리"] = "Institution · Dealer Management",
        ["기관 정보"] = "Institution Information",
        ["기관·딜러 정보"] = "Institution / Dealer Information",
        ["기관별 비상연락망"] = "Emergency Contacts",
        ["비상연락망 신규입력"] = "New Emergency Contact",
        ["비상연락망 수정"] = "Edit Emergency Contact",
        ["신규입력"] = "New Contact",
        ["업무 구분"] = "Category",
        ["연락 순서"] = "Contact Priority",
        ["담당자"] = "Contact Name",
        ["수정자"] = "Updated By",
        ["수정일시"] = "Updated At",
        ["딜러 정보"] = "Dealer Information",
        ["전체 기관 한도 매트릭스"] = "Credit Limit Matrix",
        ["인쇄 미리보기"] = "Print Preview",
        ["바로 인쇄"] = "Print",
        ["인쇄"] = "Print",
        ["인쇄 오류"] = "Print Error",
        ["거래 CSV 업로드"] = "Trade CSV Import",
        ["Settle Date"] = "Settle Date",
        ["날짜별 조회"] = "Daily Search",
        ["엑셀 임포트"] = "Excel Import",
        ["엑셀 선택"] = "Select Excel",
        ["DB 저장"] = "Save to DB",
        ["Settle Date 엑셀"] = "Settle Date Excel",
        ["기관별 중개 수수료"] = "Brokerage Fee Management",
        ["중개 수수료 관리"] = "Brokerage Fee Management",
        ["스위치 수수료 조회 및 수정"] = "Switch Fee Inquiry / Maintenance",
        ["API 수수료 면제 관리"] = "API Fee Exemption Management",
        ["API 거래 수수료 면제 정책"] = "API Trading Fee Exemption Policies",
        ["대표이사 보고"] = "Executive Report",
        ["매출 수수료 조회"] = "Sales Fee Report",
        ["수수료 계산서 내역"] = "Fee Statement Details",
        ["월별 계산서 저장"] = "Save Monthly Statement",
        ["월/일별 수수료"] = "Monthly / Daily Fees",
        ["월별"] = "Monthly",
        ["일별"] = "Daily",
        ["조회연도"] = "Year",
        ["월"] = "Month",
        ["총 수수료 USD"] = "Total Fees USD",
        ["총 수수료 KRW"] = "Total Fees KRW",
        ["월을 선택해 주세요."] = "Select a month.",
        ["중개 수수료 USD"] = "Brokerage USD",
        ["중개 수수료 KRW"] = "Brokerage KRW",
        ["스위치 수수료 USD"] = "Switch Fee USD",
        ["스위치 수수료 KRW"] = "Switch Fee KRW",
        ["총 수수료(KRW 환산)"] = "Total Fee (KRW)",
        ["전체 합계"] = "Grand Total",
        ["백오피스 사용자 관리"] = "User Management",
        ["화면 권한 저장"] = "Save Screen Access",
        ["화면 권한 없음"] = "No Screen Access",
        ["사용자를 선택하세요."] = "Select a user.",
        ["현재 아이디에 허용된 화면이 없습니다. 관리자에게 화면 권한을 요청하세요."] = "No screens are allowed for this account. Ask an administrator for access.",
        ["FIX 전문 조회"] = "FIX Message Viewer",
        ["상태"] = "Status",
        ["전송 구분"] = "Transmission",
        ["전송여부"] = "Sent",
        ["전송"] = "Sent",
        ["미전송"] = "Not Sent",
        ["채널"] = "Channel",
        ["검색"] = "Search Text",
        ["전체"] = "All",
        ["전송시간"] = "Created",
        ["순서"] = "No.",
        ["필드명"] = "Field Name",
        ["값"] = "Value",
        ["파싱된 FIX 태그"] = "Parsed FIX Tags",
        ["FIX 원문 (SOH는 |로 표시)"] = "Raw FIX Message (SOH shown as |)",
        ["조회"] = "Search",
        ["새로고침"] = "Refresh",
        ["전체 새로고침"] = "Refresh All",
        ["엑셀 저장"] = "Save Excel",
        ["메일 전송"] = "Send Email",
        ["메일 발송"] = "Send Email",
        ["대표이사 보고 메일 발송"] = "Send Executive Report Email",
        ["저장"] = "Save",
        ["수정"] = "Edit",
        ["취소"] = "Cancel",
        ["신규"] = "New",
        ["신규 등록"] = "New",
        ["변경"] = "Change",
        ["종료"] = "Exit",
        ["로그인"] = "Sign In",
        ["아이디"] = "User ID",
        ["비밀번호"] = "Password",
        ["아이디/비밀번호 저장"] = "Remember ID/Password",
        ["자동 로그인"] = "Auto Sign-In",
        ["보내는 사람"] = "From",
        ["받는 사람"] = "To",
        ["제목"] = "Subject",
        ["본문"] = "Body",
        ["거래일"] = "Trade Date",
        ["조회기간"] = "Date Range",
        ["메시지 전송일"] = "Message Sent Date",
        ["환율(USD/KRW)"] = "Exchange Rate (USD/KRW)",
        ["환율 저장"] = "Save Rates",
        ["전체 입력 (해제 시 개별 입력)"] = "Apply to all (uncheck to edit individually)",
        ["환율"] = "Rate",
        ["포인트"] = "Points",
        ["기관"] = "Institution",
        ["기관정보"] = "Institutions",
        ["딜러정보"] = "Dealers",
        ["기관코드"] = "Institution Code",
        ["딜러 ID"] = "Trader ID",
        ["시작일"] = "Start Date",
        ["종료일"] = "End Date",
        ["사용 중"] = "Active",
        ["종료/미사용"] = "Expired/Inactive",
        ["기관명"] = "Institution Name",
        ["대표 이메일"] = "Primary Email",
        ["영문명"] = "English Name",
        ["한글명"] = "Korean Name",
        ["이메일"] = "Email",
        ["휴대전화"] = "Mobile Phone",
        ["회사전화"] = "Work Phone",
        ["직급"] = "Job Title",
        ["통화"] = "Currency",
        ["비고"] = "Remarks",
        ["상품"] = "Product",
        ["사용"] = "Active",
        ["권한"] = "Role",
        ["사용자명"] = "User Name",
        ["변경 필요"] = "Change Required",
        ["마지막 로그인"] = "Last Sign-In",
        ["비밀번호 변경일"] = "Password Changed",
        ["등록일"] = "Created At",
        ["CSV 선택"] = "Select CSV",
        ["TRADES 입력"] = "Import Trades",
        ["거래 CSV"] = "Trade CSV",
        ["Invoice 생성"] = "Create Invoice",
        ["전체저장"] = "Save All",
        ["데이터 원본"] = "Data Source",
        ["조회월"] = "Report Month",
        ["뉴욕종가"] = "New York Close",
        ["BOK 기준일"] = "BOK Report Date",
        ["BOK 조회"] = "Search BOK",
        ["BOK 기간 조회"] = "Search BOK Date Range",
        ["KB EOD 기준일"] = "KB EOD Report Date",
        ["전송 회차"] = "Reporting Session",
        ["KB EOD 조회"] = "Search KB EOD",
        ["FTP 전송"] = "Send via FTP",
        ["SFTP 접속 테스트"] = "Test SFTP Connection",
        ["준비"] = "Ready",
        ["대기"] = "Ready",
        ["조회 대기"] = "Ready",
        ["입력 대기"] = "Pending",
        ["조회 중..."] = "Searching...",
        ["저장 중..."] = "Saving...",
        ["처리 중..."] = "Processing...",
        ["발송 중..."] = "Sending...",
        ["변경 중..."] = "Changing...",
        ["조회 실패"] = "Search Failed",
        ["입력 실패"] = "Import Failed",
        ["거래 없음"] = "No Trades",
        ["Bank"] = "Bank",
        ["Brokerage"] = "Brokerage",
        ["Switch Fee"] = "Switch Fee",
        ["Daily Total"] = "Daily Total",
        ["Monthly Total"] = "Monthly Total",
        ["Net Fee"] = "Net Fee",
        ["Product"] = "Product",
        ["은행"] = "Bank",
        ["중개 수수료"] = "Brokerage",
        ["스위치 수수료"] = "Switch Fee",
        ["일일 합계"] = "Daily Total",
        ["월 누계"] = "Monthly Total",
        ["순 수수료"] = "Net Fee",
        ["제품"] = "Product"
    };

    private static readonly Dictionary<string, string> EnglishToKorean =
        KoreanToEnglish
            .Where(x => x.Key != x.Value)
            .GroupBy(x => x.Value, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.Ordinal);

    public static bool English { get; private set; } = Load();

    public static void SetEnglish(bool value)
    {
        English = value;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingPath)!);
            File.WriteAllText(SettingPath, value ? "en" : "ko", Encoding.UTF8);
        }
        catch
        {
            // 언어 저장에 실패해도 현재 실행 중인 화면 전환은 유지한다.
        }
    }

    public static string Text(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        Dictionary<string, string> map = English ? KoreanToEnglish : EnglishToKorean;
        if (map.TryGetValue(value, out string? translated))
            return translated;

        if (English && value.StartsWith("로그인: ", StringComparison.Ordinal))
            return "Signed in: " + value[4..];
        if (!English && value.StartsWith("Signed in: ", StringComparison.Ordinal))
            return "로그인: " + value[11..];

        return value;
    }

    public static void Apply(Control root)
    {
        if (root is Form or Label or Button or CheckBox or RadioButton or GroupBox or TabPage)
            root.Text = Text(root.Text);

        if (root is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = Text(column.HeaderText);
        }

        if (root is ToolStrip strip)
            Apply(strip.Items);
        if (root.ContextMenuStrip is not null)
            Apply(root.ContextMenuStrip.Items);

        foreach (Control child in root.Controls)
            Apply(child);
    }

    private static void Apply(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Text = Text(item.Text);
            if (item is ToolStripDropDownItem dropDown)
                Apply(dropDown.DropDownItems);
        }
    }

    private static bool Load()
    {
        try
        {
            return File.Exists(SettingPath) &&
                   File.ReadAllText(SettingPath).Trim().Equals("en", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

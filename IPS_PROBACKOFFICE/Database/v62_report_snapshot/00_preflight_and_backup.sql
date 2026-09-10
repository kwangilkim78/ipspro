-- MySQL 8.0.33 / 읽기 전용. 별도 SQL 연결에서 파일 전체를 실행합니다.
SELECT VERSION() AS mysql_version, @@event_scheduler AS event_scheduler,
       @@system_time_zone AS system_time_zone, @@session.time_zone AS session_timezone,
       @@binlog_format AS binlog_format, CURRENT_USER() AS execution_account;
SHOW GRANTS;
-- 아래 두 결과의 CREATE VIEW 전체를 파일로 저장해 두세요. 본 설치는 이 뷰들을 변경하지 않습니다.
SHOW CREATE VIEW ips_ew.v_trading_report;
SHOW CREATE VIEW ips_ew.v_trading_report_backoffice;
SHOW COLUMNS FROM ips_ew.v_trading_report_backoffice;
-- 최초 설치 전에는 아래 이름에 해당하는 객체가 없어야 합니다.
SELECT TABLE_NAME, TABLE_TYPE FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'ips_ew' AND TABLE_NAME IN
 ('bo_report_snapshot_runs','bo_report_snapshot_state','bo_report_trade_rows','v_bo_report_trades');
SELECT ROUTINE_NAME FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'ips_ew' AND ROUTINE_NAME IN
 ('sp_bo_report_refresh','sp_bo_report_restore_previous');
SELECT EVENT_NAME, STATUS FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = 'ips_ew' AND EVENT_NAME = 'ev_bo_report_refresh';

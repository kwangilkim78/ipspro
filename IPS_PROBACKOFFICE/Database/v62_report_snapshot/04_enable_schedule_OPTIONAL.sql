-- 선택 실행: 02/03 확인 후에만 15분 주기 전체 갱신을 켭니다. EVENT 권한 필요.
-- 갱신 비용을 측정하기 전 1분 주기로 줄이지 마세요. 기존 전역 event_scheduler 설정은 변경하지 않습니다.
-- 같은 갱신이 아직 실행 중이면 프로시저 잠금이 다음 실행을 거부합니다.
SET @bo_event_timezone = @@session.time_zone;
SET SESSION time_zone = '+09:00';
CREATE EVENT IF NOT EXISTS ips_ew.ev_bo_report_refresh
ON SCHEDULE EVERY 15 MINUTE STARTS CURRENT_TIMESTAMP + INTERVAL 15 MINUTE
ON COMPLETION PRESERVE DISABLE
COMMENT 'Backoffice full snapshot refresh; original views unchanged'
DO CALL ips_ew.sp_bo_report_refresh(FALSE);
ALTER EVENT ips_ew.ev_bo_report_refresh
ON SCHEDULE EVERY 15 MINUTE STARTS CURRENT_TIMESTAMP + INTERVAL 15 MINUTE
ENABLE;
SET SESSION time_zone = @bo_event_timezone;
SELECT @@event_scheduler AS scheduler;
SELECT EVENT_NAME, STATUS, INTERVAL_VALUE, INTERVAL_FIELD, LAST_EXECUTED
FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = 'ips_ew' AND EVENT_NAME = 'ev_bo_report_refresh';

-- 자동 갱신만 중지/제거합니다. 생성한 보고서 테이블·스냅샷·원본 데이터는 모두 보존합니다.
DROP EVENT IF EXISTS ips_ew.ev_bo_report_refresh;
-- 이미 실행 중인 작업은 취소되지 않습니다. NULL이면 실행 중인 갱신/복원이 없습니다.
SELECT IS_USED_LOCK('ips_ew.bo_report_snapshot') AS running_connection_id;

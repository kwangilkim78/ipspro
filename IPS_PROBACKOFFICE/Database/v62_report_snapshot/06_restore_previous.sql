-- 05로 자동 갱신을 먼저 중지하고 running_connection_id가 NULL인지 확인한 뒤 실행하세요.
-- 직전 성공 스냅샷으로 전환합니다. 최초 1회 적재만 했다면 복원할 이전 세대가 없어 오류가 납니다.
-- 이 파일을 다시 실행하면 현재/직전이 다시 바뀝니다. 한 번만 실행 후 03으로 확인하세요.
CALL ips_ew.sp_bo_report_restore_previous();
SELECT * FROM ips_ew.bo_report_snapshot_state;

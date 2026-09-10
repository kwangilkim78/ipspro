-- 초기 전체 적재 또는 수동 새로고침. 원본 뷰를 1회 전체 조회하므로 한가한 시간에 실행하세요.
-- CALL 자체에 COMMIT이 있으므로 별도 SQL 연결에서 실행합니다.
-- 원본이 0건이면 실패시키고 기존 공개 결과를 유지합니다.
CALL ips_ew.sp_bo_report_refresh(FALSE);
SELECT s.active_snapshot_id, s.previous_snapshot_id, r.status, r.row_count,
       r.started_at_utc, r.completed_at_utc,
       TIMESTAMPDIFF(MICROSECOND, r.started_at_utc, r.completed_at_utc) / 1000000 AS elapsed_seconds
FROM ips_ew.bo_report_snapshot_state s
LEFT JOIN ips_ew.bo_report_snapshot_runs r ON r.snapshot_id = s.active_snapshot_id;

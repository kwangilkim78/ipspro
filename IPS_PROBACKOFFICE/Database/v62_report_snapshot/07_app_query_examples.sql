-- 조회 예제일 뿐, 기존 프로그램의 쿼리 변경/배포를 대신하지 않습니다.
-- 한국시간 날짜는 이미 변환되어 있으므로 bo_executed_at_kst에 9시간을 더하지 않습니다.
SET @bo_from_kst = '2026-09-01 08:00:00';
SET @bo_to_kst = '2026-10-01 06:00:00';
-- 한 달 전체를 1회 조회 후 앱에서 기관별로 분리할 때 사용합니다.
SELECT * FROM ips_ew.v_bo_report_trades
WHERE bo_executed_at_kst >= @bo_from_kst AND bo_executed_at_kst < @bo_to_kst
  AND UPPER(TRIM(status)) = 'FILLED'
ORDER BY bo_executed_at_kst, trade_id, FIELD(LEG_TYPE, 'NEAR', 'FAR');

-- 여러 쿼리로 전체저장할 때는 같은 연결의 읽기 전용 트랜잭션에서 전부 읽은 뒤 COMMIT합니다.
-- 데이터 수신을 마친 후 트랜잭션을 끝내고 Excel을 생성합니다.
-- 여러 연결에서 active_snapshot_id를 각각 새로 읽으면 서로 다른 세대가 섞일 수 있습니다.
-- 거래 중복 제거/기관별 BUY SELL 변환/수수료 면제와 스위치 계산은 기존 앱 규칙을 재사용합니다.
-- 앱 계정은 v_bo_report_trades 및 bo_report_snapshot_state/runs SELECT만 필요합니다.
-- GRANT는 실제 사용자/Host를 SHOW GRANTS로 확인한 뒤 관리자가 별도로 설정하세요.

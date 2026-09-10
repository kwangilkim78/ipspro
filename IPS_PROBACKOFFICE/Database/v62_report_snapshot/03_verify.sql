-- 읽기 전용 검증. 최신 원본과 비교하므로 적재 후 거래/정정이 생기면 차이가 날 수 있습니다.
-- 동일시점 완전 일치 검증은 거래 변경이 없는 시간에 02 후 실행하세요.
SET @bo_verify_timezone = @@session.time_zone;
SET SESSION time_zone = '+09:00';
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY;
SELECT s.active_snapshot_id, s.previous_snapshot_id, r.status, r.row_count,
       DATE_ADD(r.completed_at_utc, INTERVAL 9 HOUR) AS refreshed_at_kst,
       TIMESTAMPDIFF(SECOND, r.completed_at_utc, UTC_TIMESTAMP()) AS age_seconds,
       (SELECT COUNT(*) FROM ips_ew.v_bo_report_trades) AS actual_rows
FROM ips_ew.bo_report_snapshot_state s
LEFT JOIN ips_ew.bo_report_snapshot_runs r ON r.snapshot_id = s.active_snapshot_id;

-- 동일 원본 행의 중복 개수까지 비교합니다. 0이면 39개 원본 컬럼이 모두 일치합니다.
WITH differences AS (
    SELECT UNHEX(SHA2(CAST(JSON_ARRAY(v.`BOKId`, v.`trade_id`, v.`symbol`, v.`PRODUCT`, v.`TYPE`, v.`LEG_TYPE`, v.`Listed/OTC`, v.`price`, v.`Original Quantity`, v.`Filled Quantity`, v.`Term Filled Quantity`, v.`status`, v.`Settle Date`, v.`Fixing Date`, v.`Trade Date`, v.`Buy.client_order_id`, v.`Buy.created`, v.`Buy.modified`, v.`Buy.Executing_Trader`, v.`Buy.Executing_Organisation`, v.`Buy.Counterparty_User`, v.`Buy.Counterparty_Organisation`, v.`Buy.brokerage_commission`, v.`Buy.brokerage_currency`, v.`Sell.client_order_id`, v.`Sell.created`, v.`Sell.modified`, v.`Sell.Executing_Trader`, v.`Sell.Executing_Organisation`, v.`Sell.Counterparty_User`, v.`Sell.Counterparty_Organisation`, v.`Sell.brokerage_commission`, v.`Sell.brokerage_currency`, v.`leg_price`, v.`leg_settle_date`, v.`leg_fixing_date`, v.`is_switch_trade`, v.`Switch Client`, v.`Switch Bank`) AS CHAR CHARACTER SET utf8mb4), 256)) AS row_hash, 1 AS delta
    FROM ips_ew.v_trading_report_backoffice v
    UNION ALL
    SELECT bo_source_hash, -1 FROM ips_ew.v_bo_report_trades
), mismatches AS (
    SELECT row_hash, SUM(delta) AS delta FROM differences
    GROUP BY row_hash HAVING SUM(delta) <> 0
)
SELECT COUNT(*) AS mismatched_row_patterns,
       COALESCE(SUM(ABS(delta)), 0) AS mismatched_row_count
FROM mismatches;

-- 복제 저장 자체에 누락/변환이 없는지 확인합니다. 반드시 0이어야 합니다.
SELECT COUNT(*) AS stored_hash_errors FROM ips_ew.v_bo_report_trades v
WHERE NOT (bo_source_hash <=> UNHEX(SHA2(CAST(JSON_ARRAY(v.`BOKId`, v.`trade_id`, v.`symbol`, v.`PRODUCT`, v.`TYPE`, v.`LEG_TYPE`, v.`Listed/OTC`, v.`price`, v.`Original Quantity`, v.`Filled Quantity`, v.`Term Filled Quantity`, v.`status`, v.`Settle Date`, v.`Fixing Date`, v.`Trade Date`, v.`Buy.client_order_id`, v.`Buy.created`, v.`Buy.modified`, v.`Buy.Executing_Trader`, v.`Buy.Executing_Organisation`, v.`Buy.Counterparty_User`, v.`Buy.Counterparty_Organisation`, v.`Buy.brokerage_commission`, v.`Buy.brokerage_currency`, v.`Sell.client_order_id`, v.`Sell.created`, v.`Sell.modified`, v.`Sell.Executing_Trader`, v.`Sell.Executing_Organisation`, v.`Sell.Counterparty_User`, v.`Sell.Counterparty_Organisation`, v.`Sell.brokerage_commission`, v.`Sell.brokerage_currency`, v.`leg_price`, v.`leg_settle_date`, v.`leg_fixing_date`, v.`is_switch_trade`, v.`Switch Client`, v.`Switch Bank`) AS CHAR CHARACTER SET utf8mb4), 256)));

-- 날짜/상품별 건수·수량. 매수/매도 또는 레그를 합친 체결 건수와는 다른 '보고서 행' 기준입니다.
SELECT DATE(bo_executed_at_kst) AS kst_date, PRODUCT, LEG_TYPE,
       COUNT(*) AS report_rows, SUM(`Filled Quantity`) AS report_quantity
FROM ips_ew.v_bo_report_trades
GROUP BY DATE(bo_executed_at_kst), PRODUCT, LEG_TYPE
ORDER BY kst_date DESC, PRODUCT, LEG_TYPE;

SELECT snapshot_id, status, row_count, started_at_utc, completed_at_utc, error_text
FROM ips_ew.bo_report_snapshot_runs ORDER BY snapshot_id DESC LIMIT 10;
COMMIT;
SET SESSION time_zone = @bo_verify_timezone;

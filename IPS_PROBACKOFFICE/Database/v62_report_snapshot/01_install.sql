-- MySQL 8.0.33 전용, 최초 1회 설치. SQL 편집기의 '스크립트 실행'으로 실행합니다.
-- 오류 발생 시 실행을 중단하세요. 기존 이름의 객체를 DROP/덮어쓰기하지 않습니다.
-- 00_preflight_and_backup.sql 먼저 실행. 자동 갱신/초기 적재/앱 전환은 여기서 하지 않습니다.
-- 실행 계정: CREATE, INDEX, CREATE VIEW, CREATE ROUTINE 및 원본 뷰 SELECT 권한 필요.
-- 프로시저 DEFINER는 설치 계정입니다. 해당 계정의 필요한 권한을 계속 유지하세요.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET @bo_install_sql_mode = @@session.sql_mode;
SET SESSION sql_mode = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

CREATE TABLE ips_ew.bo_report_snapshot_runs (
    snapshot_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    started_at_utc DATETIME(6) NOT NULL,
    completed_at_utc DATETIME(6) NULL,
    status VARCHAR(16) NOT NULL,
    row_count BIGINT UNSIGNED NULL,
    error_text TEXT NULL,
    KEY ix_bo_run_started (started_at_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE ips_ew.bo_report_snapshot_state (
    singleton_id TINYINT NOT NULL PRIMARY KEY,
    active_snapshot_id BIGINT UNSIGNED NULL,
    previous_snapshot_id BIGINT UNSIGNED NULL,
    activated_at_utc DATETIME(6) NULL,
    CONSTRAINT ck_bo_singleton CHECK (singleton_id = 1)
) ENGINE=InnoDB;
INSERT INTO ips_ew.bo_report_snapshot_state(singleton_id) VALUES (1);

-- 원본 뷰의 39개 컬럼을 보존합니다. created/modified는 기존 KST 세션에서 보이는 값 그대로 DATETIME으로 저장합니다.
-- BOKId는 고유키가 아닙니다. 동일 거래의 기관/레그 행을 합치거나 중복 제거하지 않습니다.
CREATE TABLE ips_ew.bo_report_trade_rows (
    bo_row_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    bo_snapshot_id BIGINT UNSIGNED NOT NULL,
    bo_executed_at_source DATETIME(6) NOT NULL,
    bo_executed_at_kst DATETIME(6) NOT NULL,
    bo_buy_org VARCHAR(255) NULL,
    bo_sell_org VARCHAR(255) NULL,
    bo_source_hash BINARY(32) NOT NULL,
    `BOKId` BIGINT UNSIGNED NULL,
    `trade_id` VARCHAR(55) NULL,
    `symbol` VARCHAR(255) NULL,
    `PRODUCT` VARCHAR(255) NULL,
    `TYPE` VARCHAR(255) NULL,
    `LEG_TYPE` VARCHAR(4) NULL,
    `Listed/OTC` VARCHAR(1) NULL,
    `price` DECIMAL(14,7) NULL,
    `Original Quantity` DECIMAL(20,7) NULL,
    `Filled Quantity` DECIMAL(20,7) NULL,
    `Term Filled Quantity` DECIMAL(20,7) NULL,
    `status` VARCHAR(255) NULL,
    `Settle Date` DATE NULL,
    `Fixing Date` DATE NULL,
    `Trade Date` DATE NULL,
    `Buy.client_order_id` VARCHAR(255) NULL,
    `Buy.created` DATETIME(6) NULL,
    `Buy.modified` DATETIME NULL,
    `Buy.Executing_Trader` VARCHAR(255) NULL,
    `Buy.Executing_Organisation` VARCHAR(255) NULL,
    `Buy.Counterparty_User` VARCHAR(255) NULL,
    `Buy.Counterparty_Organisation` VARCHAR(255) NULL,
    `Buy.brokerage_commission` VARCHAR(255) NULL,
    `Buy.brokerage_currency` VARCHAR(255) NULL,
    `Sell.client_order_id` VARCHAR(255) NULL,
    `Sell.created` DATETIME(6) NULL,
    `Sell.modified` DATETIME NULL,
    `Sell.Executing_Trader` VARCHAR(255) NULL,
    `Sell.Executing_Organisation` VARCHAR(255) NULL,
    `Sell.Counterparty_User` VARCHAR(255) NULL,
    `Sell.Counterparty_Organisation` VARCHAR(255) NULL,
    `Sell.brokerage_commission` VARCHAR(255) NULL,
    `Sell.brokerage_currency` VARCHAR(255) NULL,
    `leg_price` DECIMAL(20,7) NULL,
    `leg_settle_date` DATE NULL,
    `leg_fixing_date` DATE NULL,
    `is_switch_trade` BIGINT NULL,
    `Switch Client` VARCHAR(255) NULL,
    `Switch Bank` VARCHAR(255) NULL,
    KEY ix_bo_date (bo_snapshot_id, bo_executed_at_kst),
    KEY ix_bo_buy_date (bo_snapshot_id, bo_buy_org, bo_executed_at_kst),
    KEY ix_bo_sell_date (bo_snapshot_id, bo_sell_org, bo_executed_at_kst),
    KEY ix_bo_trade (bo_snapshot_id, trade_id, PRODUCT(32)),
    KEY ix_bo_hash (bo_snapshot_id, bo_source_hash)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 새 앱이 사용할 조회 뷰입니다. 기존 v_trading_report*는 그대로 둡니다.
CREATE ALGORITHM=MERGE SQL SECURITY DEFINER VIEW ips_ew.v_bo_report_trades AS
SELECT r.*
FROM ips_ew.bo_report_trade_rows r
JOIN ips_ew.bo_report_snapshot_state s
  ON s.singleton_id = 1 AND r.bo_snapshot_id = s.active_snapshot_id;

DELIMITER $$
CREATE PROCEDURE ips_ew.sp_bo_report_refresh(IN p_allow_empty BOOLEAN)
SQL SECURITY DEFINER
MODIFIES SQL DATA
main: BEGIN
    DECLARE v_locked INT DEFAULT 0;
    DECLARE v_run BIGINT UNSIGNED DEFAULT NULL;
    DECLARE v_previous BIGINT UNSIGNED DEFAULT NULL;
    DECLARE v_count BIGINT DEFAULT 0;
    DECLARE v_timezone VARCHAR(64) DEFAULT NULL;
    DECLARE v_error TEXT;
    DECLARE v_state CHAR(5);
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1 v_error = MESSAGE_TEXT, v_state = RETURNED_SQLSTATE;
        ROLLBACK;
        IF v_run IS NOT NULL THEN
            START TRANSACTION;
            UPDATE ips_ew.bo_report_snapshot_runs
            SET status = 'FAILED', completed_at_utc = UTC_TIMESTAMP(6),
                error_text = CONCAT(v_state, ': ', v_error)
            WHERE snapshot_id = v_run;
            COMMIT;
        END IF;
        IF v_timezone IS NOT NULL THEN SET SESSION time_zone = v_timezone; END IF;
        IF v_locked = 1 THEN DO RELEASE_LOCK('ips_ew.bo_report_snapshot'); END IF;
        RESIGNAL;
    END;

    -- CALLは未コミットの別業務がない専用接続で実行してください。
    SET v_locked = GET_LOCK('ips_ew.bo_report_snapshot', 0);
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Report refresh/restore already running';
    END IF;
    SET v_timezone = @@session.time_zone;
    SET SESSION time_zone = '+09:00';
    START TRANSACTION;
    -- 接続断/サーバ再起動で終了した前回の記録。古い公開結果は維持されています。
    UPDATE ips_ew.bo_report_snapshot_runs
    SET status = 'ABORTED', completed_at_utc = UTC_TIMESTAMP(6),
        error_text = 'Previous refresh connection ended before completion'
    WHERE status = 'RUNNING';
    INSERT INTO ips_ew.bo_report_snapshot_runs(started_at_utc, status)
    VALUES (UTC_TIMESTAMP(6), 'RUNNING');
    SET v_run = LAST_INSERT_ID();
    COMMIT;

    -- INSERT SELECTが原本を共有ロックしないREAD COMMITTEDで読み取ります。
    -- binlog_format=ROWのMySQL 8.0を想定。実環境の設定は00で確認してください。
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    START TRANSACTION;
    SELECT active_snapshot_id INTO v_previous
    FROM ips_ew.bo_report_snapshot_state WHERE singleton_id = 1 FOR UPDATE;

    INSERT INTO ips_ew.bo_report_trade_rows (
        bo_snapshot_id, bo_executed_at_source, bo_executed_at_kst,
        bo_buy_org, bo_sell_org, bo_source_hash,
        `BOKId`,
        `trade_id`,
        `symbol`,
        `PRODUCT`,
        `TYPE`,
        `LEG_TYPE`,
        `Listed/OTC`,
        `price`,
        `Original Quantity`,
        `Filled Quantity`,
        `Term Filled Quantity`,
        `status`,
        `Settle Date`,
        `Fixing Date`,
        `Trade Date`,
        `Buy.client_order_id`,
        `Buy.created`,
        `Buy.modified`,
        `Buy.Executing_Trader`,
        `Buy.Executing_Organisation`,
        `Buy.Counterparty_User`,
        `Buy.Counterparty_Organisation`,
        `Buy.brokerage_commission`,
        `Buy.brokerage_currency`,
        `Sell.client_order_id`,
        `Sell.created`,
        `Sell.modified`,
        `Sell.Executing_Trader`,
        `Sell.Executing_Organisation`,
        `Sell.Counterparty_User`,
        `Sell.Counterparty_Organisation`,
        `Sell.brokerage_commission`,
        `Sell.brokerage_currency`,
        `leg_price`,
        `leg_settle_date`,
        `leg_fixing_date`,
        `is_switch_trade`,
        `Switch Client`,
        `Switch Bank`
    )
    SELECT v_run, GREATEST(COALESCE(v.`Buy.created`, '1970-01-01'), COALESCE(v.`Sell.created`, '1970-01-01')), DATE_ADD(GREATEST(COALESCE(v.`Buy.created`, '1970-01-01'), COALESCE(v.`Sell.created`, '1970-01-01')), INTERVAL 9 HOUR),
        LOWER(TRIM(v.`Buy.Executing_Organisation`)),
        LOWER(TRIM(v.`Sell.Executing_Organisation`)),
        UNHEX(SHA2(CAST(JSON_ARRAY(v.`BOKId`, v.`trade_id`, v.`symbol`, v.`PRODUCT`, v.`TYPE`, v.`LEG_TYPE`, v.`Listed/OTC`, v.`price`, v.`Original Quantity`, v.`Filled Quantity`, v.`Term Filled Quantity`, v.`status`, v.`Settle Date`, v.`Fixing Date`, v.`Trade Date`, v.`Buy.client_order_id`, v.`Buy.created`, v.`Buy.modified`, v.`Buy.Executing_Trader`, v.`Buy.Executing_Organisation`, v.`Buy.Counterparty_User`, v.`Buy.Counterparty_Organisation`, v.`Buy.brokerage_commission`, v.`Buy.brokerage_currency`, v.`Sell.client_order_id`, v.`Sell.created`, v.`Sell.modified`, v.`Sell.Executing_Trader`, v.`Sell.Executing_Organisation`, v.`Sell.Counterparty_User`, v.`Sell.Counterparty_Organisation`, v.`Sell.brokerage_commission`, v.`Sell.brokerage_currency`, v.`leg_price`, v.`leg_settle_date`, v.`leg_fixing_date`, v.`is_switch_trade`, v.`Switch Client`, v.`Switch Bank`) AS CHAR CHARACTER SET utf8mb4), 256)),
        v.`BOKId`,
        v.`trade_id`,
        v.`symbol`,
        v.`PRODUCT`,
        v.`TYPE`,
        v.`LEG_TYPE`,
        v.`Listed/OTC`,
        v.`price`,
        v.`Original Quantity`,
        v.`Filled Quantity`,
        v.`Term Filled Quantity`,
        v.`status`,
        v.`Settle Date`,
        v.`Fixing Date`,
        v.`Trade Date`,
        v.`Buy.client_order_id`,
        v.`Buy.created`,
        v.`Buy.modified`,
        v.`Buy.Executing_Trader`,
        v.`Buy.Executing_Organisation`,
        v.`Buy.Counterparty_User`,
        v.`Buy.Counterparty_Organisation`,
        v.`Buy.brokerage_commission`,
        v.`Buy.brokerage_currency`,
        v.`Sell.client_order_id`,
        v.`Sell.created`,
        v.`Sell.modified`,
        v.`Sell.Executing_Trader`,
        v.`Sell.Executing_Organisation`,
        v.`Sell.Counterparty_User`,
        v.`Sell.Counterparty_Organisation`,
        v.`Sell.brokerage_commission`,
        v.`Sell.brokerage_currency`,
        v.`leg_price`,
        v.`leg_settle_date`,
        v.`leg_fixing_date`,
        v.`is_switch_trade`,
        v.`Switch Client`,
        v.`Switch Bank`
    FROM ips_ew.v_trading_report_backoffice v;
    SET v_count = ROW_COUNT();
    IF v_count = 0 AND COALESCE(p_allow_empty, FALSE) = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Empty source rejected; previous snapshot retained';
    END IF;

    UPDATE ips_ew.bo_report_snapshot_runs
    SET status = 'SUCCESS', row_count = v_count, completed_at_utc = UTC_TIMESTAMP(6)
    WHERE snapshot_id = v_run;
    UPDATE ips_ew.bo_report_snapshot_state
    SET previous_snapshot_id = v_previous, active_snapshot_id = v_run,
        activated_at_utc = UTC_TIMESTAMP(6)
    WHERE singleton_id = 1;
    -- 현재/직전 2세대만 보관하며, 공개 전환과 오래된 데이터 정리를 함께 확정합니다.
    DELETE FROM ips_ew.bo_report_trade_rows
    WHERE bo_snapshot_id <> v_run AND bo_snapshot_id <> COALESCE(v_previous, v_run);
    UPDATE ips_ew.bo_report_snapshot_runs
    SET status = 'PURGED'
    WHERE status = 'SUCCESS' AND snapshot_id <> v_run
      AND snapshot_id <> COALESCE(v_previous, v_run);
    COMMIT;
    SET SESSION time_zone = v_timezone;
    DO RELEASE_LOCK('ips_ew.bo_report_snapshot');
END$$

CREATE PROCEDURE ips_ew.sp_bo_report_restore_previous()
SQL SECURITY DEFINER
MODIFIES SQL DATA
BEGIN
    DECLARE v_locked INT DEFAULT 0;
    DECLARE v_active BIGINT UNSIGNED;
    DECLARE v_previous BIGINT UNSIGNED;
    DECLARE v_expected BIGINT;
    DECLARE v_actual BIGINT;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        IF v_locked = 1 THEN DO RELEASE_LOCK('ips_ew.bo_report_snapshot'); END IF;
        RESIGNAL;
    END;
    SET v_locked = GET_LOCK('ips_ew.bo_report_snapshot', 0);
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Report refresh/restore already running';
    END IF;
    START TRANSACTION;
    SELECT active_snapshot_id, previous_snapshot_id INTO v_active, v_previous
    FROM ips_ew.bo_report_snapshot_state WHERE singleton_id = 1 FOR UPDATE;
    IF v_previous IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'No previous snapshot available';
    END IF;
    SELECT MAX(row_count) INTO v_expected FROM ips_ew.bo_report_snapshot_runs
    WHERE snapshot_id = v_previous AND status = 'SUCCESS';
    SELECT COUNT(*) INTO v_actual FROM ips_ew.bo_report_trade_rows
    WHERE bo_snapshot_id = v_previous;
    IF v_expected IS NULL OR v_actual <> v_expected THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Previous snapshot is incomplete';
    END IF;
    UPDATE ips_ew.bo_report_snapshot_state
    SET active_snapshot_id = v_previous, previous_snapshot_id = v_active,
        activated_at_utc = UTC_TIMESTAMP(6)
    WHERE singleton_id = 1;
    COMMIT;
    DO RELEASE_LOCK('ips_ew.bo_report_snapshot');
END$$
DELIMITER ;
SET SESSION sql_mode = @bo_install_sql_mode;
SELECT 'Installed. Run 02_initial_refresh.sql next; no source view was changed.' AS result;

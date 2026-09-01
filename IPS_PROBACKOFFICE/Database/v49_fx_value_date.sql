USE `ips_ew`;

CREATE TABLE IF NOT EXISTS `fx_value_date` (
    `trade_date` date NOT NULL COMMENT '기준 거래일',
    `tenor` varchar(10) NOT NULL COMMENT 'S/N 또는 1M',
    `start_date` date NOT NULL COMMENT '밸류 시작일',
    `end_date` date NOT NULL COMMENT '밸류 종료일',
    `day_count` int NOT NULL COMMENT '기간 일수',
    `day_adjustment` int NOT NULL DEFAULT 0 COMMENT '휴일 등에 따른 조정 일수',
    `raw_text` varchar(200) DEFAULT NULL COMMENT '엑셀 원본 문자열',
    `imported_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`trade_date`, `tenor`),
    KEY `idx_fx_value_date_start` (`start_date`),
    KEY `idx_fx_value_date_end` (`end_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='FX 테너별 밸류일자';

INSERT INTO `backoffice_user_screen_permission` (`login_id`, `screen_key`, `can_view`)
SELECT `login_id`, 'FX_VALUE_DATE', 1
FROM `backoffice_user`
WHERE `is_active` = 1
ON DUPLICATE KEY UPDATE `can_view` = VALUES(`can_view`);


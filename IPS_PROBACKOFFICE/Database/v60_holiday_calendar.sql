USE ips_ew;

CREATE TABLE IF NOT EXISTS holiday_calendar
(
    holiday_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    holiday_date DATE NOT NULL,
    country_code VARCHAR(2) NOT NULL COMMENT 'KR 또는 US',
    holiday_name VARCHAR(100) NOT NULL,
    is_holiday TINYINT(1) NOT NULL DEFAULT 1 COMMENT '1=휴일, 0=기본 휴일을 영업일로 해제',
    memo VARCHAR(255) NULL,
    created_by VARCHAR(50) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (holiday_id),
    UNIQUE KEY uk_holiday_country_date (country_code, holiday_date),
    KEY ix_holiday_date (holiday_date),
    CONSTRAINT ck_holiday_country CHECK (country_code IN ('KR', 'US'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='한국·미국 휴일 및 영업일 예외 관리';

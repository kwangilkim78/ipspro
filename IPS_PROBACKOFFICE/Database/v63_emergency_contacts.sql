-- 비상연락망 화면 최초 조회 시에도 동일한 스크립트를 실행합니다.
-- ETL 기관 테이블은 읽기 전용이며, 기관별 복수 담당자를 허용합니다.
CREATE TABLE IF NOT EXISTS ips_ew.institution_emergency_contact (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    external_institution_id BIGINT NOT NULL,
    category VARCHAR(20) NOT NULL,
    contact_name VARCHAR(100) NOT NULL,
    work_phone VARCHAR(50) NOT NULL DEFAULT '',
    mobile_phone VARCHAR(50) NOT NULL DEFAULT '',
    email VARCHAR(254) NOT NULL DEFAULT '',
    contact_priority INT NOT NULL DEFAULT 1,
    remarks VARCHAR(1000) NOT NULL DEFAULT '',
    created_by VARCHAR(100) NOT NULL,
    updated_by VARCHAR(100) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    INDEX ix_emergency_institution_category (external_institution_id, category, contact_priority),
    CONSTRAINT chk_emergency_category CHECK (category IN ('딜러', 'IT', '백오피스')),
    CONSTRAINT chk_emergency_priority CHECK (contact_priority BETWEEN 1 AND 99)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

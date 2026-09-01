-- v29 테이블을 이미 만든 경우 한 번만 실행합니다.
ALTER TABLE backoffice_user
ADD COLUMN must_change_password TINYINT(1) NOT NULL DEFAULT 1
    AFTER password_hash,
ADD COLUMN password_changed_at DATETIME NULL
    AFTER last_login_at;

-- 기존 관리자 kikim은 최초 변경 강제 대상에서 제외할 경우 실행합니다.
UPDATE backoffice_user
SET must_change_password = 0
WHERE login_id = 'kikim';

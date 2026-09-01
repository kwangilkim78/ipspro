-- 역할 구분을 사용하지 않고 아이디별 화면 권한만 사용합니다.
UPDATE backoffice_user
SET role_code = 'USER';

-- 기존 사용자가 업데이트 직후 잠기지 않도록 누락된 화면 권한을 전체 허용으로 생성합니다.
-- 프로그램에서 각 사용자별 체크를 해제해 저장하면 이후 그 설정이 적용됩니다.
INSERT IGNORE INTO backoffice_user_screen_permission
    (login_id, screen_key, can_view)
SELECT
    u.login_id,
    screens.screen_key,
    1
FROM backoffice_user u
CROSS JOIN (
    SELECT 'OPERATIONS' AS screen_key
    UNION ALL SELECT 'MASTER_DATA'
    UNION ALL SELECT 'CREDIT_MATRIX'
    UNION ALL SELECT 'BROKERAGE_FEE'
    UNION ALL SELECT 'EXECUTIVE_REPORT'
    UNION ALL SELECT 'FEE_REVENUE'
    UNION ALL SELECT 'INVOICE'
    UNION ALL SELECT 'FIX_MESSAGE'
    UNION ALL SELECT 'TRADE_IMPORT'
    UNION ALL SELECT 'USER_MANAGEMENT'
) screens;

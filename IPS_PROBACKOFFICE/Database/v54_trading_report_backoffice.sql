-- EW 제공 테이블은 조회만 하고 ips_ew에 백오피스 전용 뷰를 생성한다.
CREATE OR REPLACE ALGORITHM = UNDEFINED
VIEW ips_ew.v_trading_report_backoffice AS
WITH switch_info AS
(
    SELECT
        f.trade_id,
        MAX(CASE
            WHEN LOWER(COALESCE(u.username, '')) LIKE '%switch%'
             AND LOWER(COALESCE(om.cover_username, '')) NOT LIKE '%switch%'
                THEN NULLIF(TRIM(om.cover_username), '')
            WHEN LOWER(COALESCE(om.cover_username, '')) LIKE '%switch%'
             AND LOWER(COALESCE(u.username, '')) NOT LIKE '%switch%'
                THEN NULLIF(TRIM(u.username), '')
            ELSE NULL
        END) AS switch_client,
        MAX(CASE
            WHEN LOWER(COALESCE(u.username, '')) LIKE '%switch%'
             AND LOWER(COALESCE(om.cover_username, '')) NOT LIKE '%switch%'
                THEN NULLIF(TRIM(om.cover_organization_name), '')
            WHEN LOWER(COALESCE(om.cover_username, '')) LIKE '%switch%'
             AND LOWER(COALESCE(u.username, '')) NOT LIKE '%switch%'
                THEN NULLIF(TRIM(o.name), '')
            ELSE NULL
        END) AS switch_bank,
        MAX(CASE
            WHEN LOWER(COALESCE(u.username, '')) LIKE '%switch%'
              OR LOWER(COALESCE(om.cover_username, '')) LIKE '%switch%'
                THEN 1
            ELSE 0
        END) AS is_switch_trade
    FROM mextrader_admin_prod.orders_matchers om
    JOIN mextrader_admin_prod.users u
      ON u.id = om.user_id
    JOIN mextrader_admin_prod.organizations o
      ON o.id = om.organization_id
    JOIN mex_prod.fills f
      ON f.client_order_id = om.client_order_id
    WHERE COALESCE(om.cover_client_order_id, '') <> ''
    GROUP BY f.trade_id
)
SELECT
    vr.*,
    COALESCE(si.is_switch_trade, 0) AS is_switch_trade,
    si.switch_client AS `Switch Client`,
    si.switch_bank AS `Switch Bank`
FROM ips_ew.v_trading_report vr
LEFT JOIN switch_info si
  ON si.trade_id = vr.trade_id;

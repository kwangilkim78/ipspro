CREATE OR REPLACE ALGORITHM=UNDEFINED DEFINER=`root`@`10.10.2.61` SQL SECURITY DEFINER VIEW `ips_ew`.`v_trading_report_backoffice` AS
WITH switch_info AS (
 SELECT f.trade_id,
 MAX(CASE
   WHEN LOWER(COALESCE(u.username,'')) LIKE '%switch%' AND LOWER(COALESCE(om.cover_username,'')) NOT LIKE '%switch%' THEN NULLIF(TRIM(om.cover_username),'')
   WHEN LOWER(COALESCE(om.cover_username,'')) LIKE '%switch%' AND LOWER(COALESCE(u.username,'')) NOT LIKE '%switch%' THEN NULLIF(TRIM(u.username),'')
   ELSE NULL END) AS switch_client,
 MAX(CASE
   WHEN LOWER(COALESCE(u.username,'')) LIKE '%switch%' AND LOWER(COALESCE(om.cover_username,'')) NOT LIKE '%switch%' THEN NULLIF(TRIM(om.cover_organization_name),'')
   WHEN LOWER(COALESCE(om.cover_username,'')) LIKE '%switch%' AND LOWER(COALESCE(u.username,'')) NOT LIKE '%switch%' THEN NULLIF(TRIM(o.name),'')
   ELSE NULL END) AS switch_bank,
 MAX(CASE WHEN LOWER(COALESCE(u.username,'')) LIKE '%switch%' OR LOWER(COALESCE(om.cover_username,'')) LIKE '%switch%' THEN 1 ELSE 0 END) AS is_switch_trade
 FROM mextrader_admin_prod.orders_matchers om
 JOIN mextrader_admin_prod.users u ON u.id=om.user_id
 JOIN mextrader_admin_prod.organizations o ON o.id=om.organization_id
 JOIN ips_ew.v_fills_execution f ON f.client_order_id=om.client_order_id
 WHERE COALESCE(om.cover_client_order_id,'')<>''
 GROUP BY f.trade_id
)
SELECT vr.`BOKId` AS `BOKId`,
 vr.`trade_id` AS `trade_id`,
 vr.`symbol` AS `symbol`,
 vr.`PRODUCT` AS `PRODUCT`,
 vr.`TYPE` AS `TYPE`,
 vr.`LEG_TYPE` AS `LEG_TYPE`,
 vr.`Listed/OTC` AS `Listed/OTC`,
 vr.`price` AS `price`,
 vr.`Original Quantity` AS `Original Quantity`,
 vr.`Filled Quantity` AS `Filled Quantity`,
 vr.`Term Filled Quantity` AS `Term Filled Quantity`,
 vr.`status` AS `status`,
 vr.`Settle Date` AS `Settle Date`,
 vr.`Fixing Date` AS `Fixing Date`,
 vr.`Trade Date` AS `Trade Date`,
 vr.`Buy.client_order_id` AS `Buy.client_order_id`,
 vr.`Buy.created` AS `Buy.created`,
 vr.`Buy.modified` AS `Buy.modified`,
 vr.`Buy.Executing_Trader` AS `Buy.Executing_Trader`,
 vr.`Buy.Executing_Organisation` AS `Buy.Executing_Organisation`,
 vr.`Buy.Counterparty_User` AS `Buy.Counterparty_User`,
 vr.`Buy.Counterparty_Organisation` AS `Buy.Counterparty_Organisation`,
 vr.`Buy.brokerage_commission` AS `Buy.brokerage_commission`,
 vr.`Buy.brokerage_currency` AS `Buy.brokerage_currency`,
 vr.`Sell.client_order_id` AS `Sell.client_order_id`,
 vr.`Sell.created` AS `Sell.created`,
 vr.`Sell.modified` AS `Sell.modified`,
 vr.`Sell.Executing_Trader` AS `Sell.Executing_Trader`,
 vr.`Sell.Executing_Organisation` AS `Sell.Executing_Organisation`,
 vr.`Sell.Counterparty_User` AS `Sell.Counterparty_User`,
 vr.`Sell.Counterparty_Organisation` AS `Sell.Counterparty_Organisation`,
 vr.`Sell.brokerage_commission` AS `Sell.brokerage_commission`,
 vr.`Sell.brokerage_currency` AS `Sell.brokerage_currency`,
 vr.`leg_price` AS `leg_price`,
 vr.`leg_settle_date` AS `leg_settle_date`,
 vr.`leg_fixing_date` AS `leg_fixing_date`,
 COALESCE(si.is_switch_trade,0) AS is_switch_trade,
 si.switch_client AS `Switch Client`, si.switch_bank AS `Switch Bank`
FROM ips_ew.v_trading_report_fee_source vr LEFT JOIN switch_info si ON si.trade_id=vr.trade_id;

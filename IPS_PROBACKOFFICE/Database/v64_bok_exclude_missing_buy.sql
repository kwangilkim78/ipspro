-- ips_ew.v_trading_report source

CREATE OR REPLACE
ALGORITHM = UNDEFINED VIEW `ips_ew`.`v_trading_report` AS with `raw_trade_rows` as (
select
    `ips_ew`.`f`.`trade_id` AS `trade_id`,
    `om`.`client_order_id` AS `client_order_id`,
    `om`.`created` AS `created`,
    `om`.`modified` AS `modified`,
    `om`.`side` AS `side`,
    `i`.`symbol` AS `symbol`,
    `i`.`inst_type` AS `inst_type`,
    `i`.`tenor` AS `tenor`,
    `om`.`product_type` AS `product_type`,
    `om`.`price` AS `price`,
    `om`.`qty_original` AS `qty_original`,
    `om`.`qty_filled` AS `qty_filled`,
    `om`.`term_qty_filled` AS `term_qty_filled`,
    `om`.`status` AS `status`,
    `om`.`settle_date` AS `settle_date`,
    `om`.`fixing_date` AS `fixing_date`,
    `om`.`trade_date` AS `trade_date`,
    `om`.`last_px_near` AS `last_px_near`,
    `om`.`settle_date_near` AS `settle_date_near`,
    `om`.`fixing_date_near` AS `fixing_date_near`,
    `om`.`tenor_near` AS `tenor_near`,
    `om`.`last_px_far` AS `last_px_far`,
    `om`.`settle_date_far` AS `settle_date_far`,
    `om`.`fixing_date_far` AS `fixing_date_far`,
    `om`.`tenor_far` AS `tenor_far`,
    `u`.`username` AS `executing_trader`,
    `o`.`name` AS `executing_organisation`,
    `om`.`cover_username` AS `counterparty_user`,
    `om`.`cover_organization_name` AS `counterparty_organisation`,
    `om`.`brokerage_commission` AS `brokerage_commission`,
    `om`.`brokerage_currency` AS `brokerage_currency`,
    (case
        when ((lower(coalesce(`u`.`username`, '')) like '%switch%')
        or (lower(coalesce(`om`.`cover_username`, '')) like '%switch%')) then 1
        else 0
    end) AS `is_switch_flag`,
    (case
        when (lower(coalesce(`u`.`username`, '')) like '%switch%') then 1
        else 0
    end) AS `exec_is_switch`,
    (case
        when (lower(coalesce(`om`.`cover_username`, '')) like '%switch%') then 1
        else 0
    end) AS `cpty_is_switch`
from
    ((((`mextrader_admin_prod`.`orders_matchers` `om`
join `mextrader_admin_prod`.`instruments` `i` on
    ((`om`.`instrument_id` = `i`.`id`)))
join `mextrader_admin_prod`.`organizations` `o` on
    ((`om`.`organization_id` = `o`.`id`)))
join `mextrader_admin_prod`.`users` `u` on
    ((`u`.`id` = `om`.`user_id`)))
join `ips_ew`.`v_fills_execution` `f` on
    ((`om`.`client_order_id` = `ips_ew`.`f`.`client_order_id`)))
where
    (coalesce(`om`.`cover_client_order_id`, '') <> '')),
`trade_flags` as (
select
    `raw_trade_rows`.`trade_id` AS `trade_id`,
    max(`raw_trade_rows`.`is_switch_flag`) AS `is_switch_trade`
from
    `raw_trade_rows`
group by
    `raw_trade_rows`.`trade_id`),
`group_rows` as (
select
    `r`.`trade_id` AS `trade_id`,
    `tf`.`is_switch_trade` AS `is_switch_trade`,
    (case
        when ((`tf`.`is_switch_trade` = 1)
            and (`r`.`exec_is_switch` = 1)) then `r`.`counterparty_user`
        when ((`tf`.`is_switch_trade` = 1)
            and (`r`.`cpty_is_switch` = 1)) then `r`.`executing_trader`
        else NULL
    end) AS `external_user`,
    (case
        when ((`tf`.`is_switch_trade` = 1)
            and (`r`.`exec_is_switch` = 1)) then `r`.`counterparty_organisation`
        when ((`tf`.`is_switch_trade` = 1)
            and (`r`.`cpty_is_switch` = 1)) then `r`.`executing_organisation`
        else NULL
    end) AS `external_org`,
    (case
        when (`tf`.`is_switch_trade` = 0) then convert(`r`.`trade_id` using utf8mb3)
        else concat(convert(`r`.`trade_id` using utf8mb3), '|',(case when (`r`.`exec_is_switch` = 1) then coalesce(`r`.`counterparty_user`, '') else coalesce(`r`.`executing_trader`, '') end))
    end) AS `report_group_key`,
    `r`.`client_order_id` AS `client_order_id`,
    `r`.`created` AS `created`,
    `r`.`modified` AS `modified`,
    `r`.`side` AS `side`,
    `r`.`symbol` AS `symbol`,
    `r`.`inst_type` AS `inst_type`,
    `r`.`tenor` AS `tenor`,
    `r`.`product_type` AS `product_type`,
    `r`.`price` AS `price`,
    `r`.`qty_original` AS `qty_original`,
    `r`.`qty_filled` AS `qty_filled`,
    `r`.`term_qty_filled` AS `term_qty_filled`,
    `r`.`status` AS `status`,
    `r`.`settle_date` AS `settle_date`,
    `r`.`fixing_date` AS `fixing_date`,
    `r`.`trade_date` AS `trade_date`,
    `r`.`last_px_near` AS `last_px_near`,
    `r`.`settle_date_near` AS `settle_date_near`,
    `r`.`fixing_date_near` AS `fixing_date_near`,
    `r`.`tenor_near` AS `tenor_near`,
    `r`.`last_px_far` AS `last_px_far`,
    `r`.`settle_date_far` AS `settle_date_far`,
    `r`.`fixing_date_far` AS `fixing_date_far`,
    `r`.`tenor_far` AS `tenor_far`,
    `r`.`executing_trader` AS `executing_trader`,
    `r`.`executing_organisation` AS `executing_organisation`,
    `r`.`counterparty_user` AS `counterparty_user`,
    `r`.`counterparty_organisation` AS `counterparty_organisation`,
    `r`.`brokerage_commission` AS `brokerage_commission`,
    `r`.`brokerage_currency` AS `brokerage_currency`,
    `r`.`exec_is_switch` AS `exec_is_switch`,
    `r`.`cpty_is_switch` AS `cpty_is_switch`
from
    (`raw_trade_rows` `r`
join `trade_flags` `tf` on
    ((`tf`.`trade_id` = `r`.`trade_id`)))),
`ranked_rows` as (
select
    `gr`.`trade_id` AS `trade_id`,
    `gr`.`is_switch_trade` AS `is_switch_trade`,
    `gr`.`external_user` AS `external_user`,
    `gr`.`external_org` AS `external_org`,
    `gr`.`report_group_key` AS `report_group_key`,
    `gr`.`client_order_id` AS `client_order_id`,
    `gr`.`created` AS `created`,
    `gr`.`modified` AS `modified`,
    `gr`.`side` AS `side`,
    `gr`.`symbol` AS `symbol`,
    `gr`.`inst_type` AS `inst_type`,
    `gr`.`tenor` AS `tenor`,
    `gr`.`product_type` AS `product_type`,
    `gr`.`price` AS `price`,
    `gr`.`qty_original` AS `qty_original`,
    `gr`.`qty_filled` AS `qty_filled`,
    `gr`.`term_qty_filled` AS `term_qty_filled`,
    `gr`.`status` AS `status`,
    `gr`.`settle_date` AS `settle_date`,
    `gr`.`fixing_date` AS `fixing_date`,
    `gr`.`trade_date` AS `trade_date`,
    `gr`.`last_px_near` AS `last_px_near`,
    `gr`.`settle_date_near` AS `settle_date_near`,
    `gr`.`fixing_date_near` AS `fixing_date_near`,
    `gr`.`tenor_near` AS `tenor_near`,
    `gr`.`last_px_far` AS `last_px_far`,
    `gr`.`settle_date_far` AS `settle_date_far`,
    `gr`.`fixing_date_far` AS `fixing_date_far`,
    `gr`.`tenor_far` AS `tenor_far`,
    `gr`.`executing_trader` AS `executing_trader`,
    `gr`.`executing_organisation` AS `executing_organisation`,
    `gr`.`counterparty_user` AS `counterparty_user`,
    `gr`.`counterparty_organisation` AS `counterparty_organisation`,
    `gr`.`brokerage_commission` AS `brokerage_commission`,
    `gr`.`brokerage_currency` AS `brokerage_currency`,
    `gr`.`exec_is_switch` AS `exec_is_switch`,
    `gr`.`cpty_is_switch` AS `cpty_is_switch`,
    row_number() OVER (PARTITION BY `gr`.`report_group_key`,
    `gr`.`side`
ORDER BY
    (case
        when ((`gr`.`is_switch_trade` = 1)
            and (`gr`.`exec_is_switch` = 1)) then 0
        when ((`gr`.`is_switch_trade` = 1)
            and (`gr`.`cpty_is_switch` = 1)) then 1
        else 2
    end),
    (case
        when (not((`gr`.`client_order_id` like 'F%'))) then 0
        else 1
    end),
    `gr`.`modified` desc,
    `gr`.`created` desc ) AS `rn`
from
    `group_rows` `gr`),
`trade_agg` as (
select
    `ranked_rows`.`report_group_key` AS `report_group_key`,
    `ranked_rows`.`trade_id` AS `trade_id`,
    max(`ranked_rows`.`symbol`) AS `symbol`,
    max(`ranked_rows`.`inst_type`) AS `inst_type`,
    max(`ranked_rows`.`tenor`) AS `tenor`,
    max(`ranked_rows`.`product_type`) AS `product_type`,
    max(`ranked_rows`.`price`) AS `price`,
    max(`ranked_rows`.`qty_original`) AS `Original Quantity`,
    max(`ranked_rows`.`qty_filled`) AS `Filled Quantity`,
    max(`ranked_rows`.`term_qty_filled`) AS `Term Filled Quantity`,
    max(`ranked_rows`.`status`) AS `status`,
    max(`ranked_rows`.`settle_date`) AS `Settle Date`,
    max(`ranked_rows`.`fixing_date`) AS `Fixing Date`,
    max(`ranked_rows`.`trade_date`) AS `Trade Date`,
    max(`ranked_rows`.`last_px_near`) AS `Near Leg price`,
    max(`ranked_rows`.`settle_date_near`) AS `Near Leg Settle`,
    max(`ranked_rows`.`fixing_date_near`) AS `Near Leg Fixing`,
    max(`ranked_rows`.`tenor_near`) AS `tenor_near`,
    max(`ranked_rows`.`last_px_far`) AS `Far Leg price`,
    max(`ranked_rows`.`settle_date_far`) AS `Far Leg Settle`,
    max(`ranked_rows`.`fixing_date_far`) AS `Far Leg Fixing`,
    max(`ranked_rows`.`tenor_far`) AS `tenor_far`,
    max(`ranked_rows`.`created`) AS `sort_created`
from
    `ranked_rows`
group by
    `ranked_rows`.`report_group_key`,
    `ranked_rows`.`trade_id`),
`buy_side` as (
select
    `ranked_rows`.`trade_id` AS `trade_id`,
    `ranked_rows`.`is_switch_trade` AS `is_switch_trade`,
    `ranked_rows`.`external_user` AS `external_user`,
    `ranked_rows`.`external_org` AS `external_org`,
    `ranked_rows`.`report_group_key` AS `report_group_key`,
    `ranked_rows`.`client_order_id` AS `client_order_id`,
    `ranked_rows`.`created` AS `created`,
    `ranked_rows`.`modified` AS `modified`,
    `ranked_rows`.`side` AS `side`,
    `ranked_rows`.`symbol` AS `symbol`,
    `ranked_rows`.`inst_type` AS `inst_type`,
    `ranked_rows`.`tenor` AS `tenor`,
    `ranked_rows`.`product_type` AS `product_type`,
    `ranked_rows`.`price` AS `price`,
    `ranked_rows`.`qty_original` AS `qty_original`,
    `ranked_rows`.`qty_filled` AS `qty_filled`,
    `ranked_rows`.`term_qty_filled` AS `term_qty_filled`,
    `ranked_rows`.`status` AS `status`,
    `ranked_rows`.`settle_date` AS `settle_date`,
    `ranked_rows`.`fixing_date` AS `fixing_date`,
    `ranked_rows`.`trade_date` AS `trade_date`,
    `ranked_rows`.`last_px_near` AS `last_px_near`,
    `ranked_rows`.`settle_date_near` AS `settle_date_near`,
    `ranked_rows`.`fixing_date_near` AS `fixing_date_near`,
    `ranked_rows`.`tenor_near` AS `tenor_near`,
    `ranked_rows`.`last_px_far` AS `last_px_far`,
    `ranked_rows`.`settle_date_far` AS `settle_date_far`,
    `ranked_rows`.`fixing_date_far` AS `fixing_date_far`,
    `ranked_rows`.`tenor_far` AS `tenor_far`,
    `ranked_rows`.`executing_trader` AS `executing_trader`,
    `ranked_rows`.`executing_organisation` AS `executing_organisation`,
    `ranked_rows`.`counterparty_user` AS `counterparty_user`,
    `ranked_rows`.`counterparty_organisation` AS `counterparty_organisation`,
    `ranked_rows`.`brokerage_commission` AS `brokerage_commission`,
    `ranked_rows`.`brokerage_currency` AS `brokerage_currency`,
    `ranked_rows`.`exec_is_switch` AS `exec_is_switch`,
    `ranked_rows`.`cpty_is_switch` AS `cpty_is_switch`,
    `ranked_rows`.`rn` AS `rn`
from
    `ranked_rows`
where
    ((`ranked_rows`.`side` = 'BUY')
        and (`ranked_rows`.`rn` = 1))),
`sell_side` as (
select
    `ranked_rows`.`trade_id` AS `trade_id`,
    `ranked_rows`.`is_switch_trade` AS `is_switch_trade`,
    `ranked_rows`.`external_user` AS `external_user`,
    `ranked_rows`.`external_org` AS `external_org`,
    `ranked_rows`.`report_group_key` AS `report_group_key`,
    `ranked_rows`.`client_order_id` AS `client_order_id`,
    `ranked_rows`.`created` AS `created`,
    `ranked_rows`.`modified` AS `modified`,
    `ranked_rows`.`side` AS `side`,
    `ranked_rows`.`symbol` AS `symbol`,
    `ranked_rows`.`inst_type` AS `inst_type`,
    `ranked_rows`.`tenor` AS `tenor`,
    `ranked_rows`.`product_type` AS `product_type`,
    `ranked_rows`.`price` AS `price`,
    `ranked_rows`.`qty_original` AS `qty_original`,
    `ranked_rows`.`qty_filled` AS `qty_filled`,
    `ranked_rows`.`term_qty_filled` AS `term_qty_filled`,
    `ranked_rows`.`status` AS `status`,
    `ranked_rows`.`settle_date` AS `settle_date`,
    `ranked_rows`.`fixing_date` AS `fixing_date`,
    `ranked_rows`.`trade_date` AS `trade_date`,
    `ranked_rows`.`last_px_near` AS `last_px_near`,
    `ranked_rows`.`settle_date_near` AS `settle_date_near`,
    `ranked_rows`.`fixing_date_near` AS `fixing_date_near`,
    `ranked_rows`.`tenor_near` AS `tenor_near`,
    `ranked_rows`.`last_px_far` AS `last_px_far`,
    `ranked_rows`.`settle_date_far` AS `settle_date_far`,
    `ranked_rows`.`fixing_date_far` AS `fixing_date_far`,
    `ranked_rows`.`tenor_far` AS `tenor_far`,
    `ranked_rows`.`executing_trader` AS `executing_trader`,
    `ranked_rows`.`executing_organisation` AS `executing_organisation`,
    `ranked_rows`.`counterparty_user` AS `counterparty_user`,
    `ranked_rows`.`counterparty_organisation` AS `counterparty_organisation`,
    `ranked_rows`.`brokerage_commission` AS `brokerage_commission`,
    `ranked_rows`.`brokerage_currency` AS `brokerage_currency`,
    `ranked_rows`.`exec_is_switch` AS `exec_is_switch`,
    `ranked_rows`.`cpty_is_switch` AS `cpty_is_switch`,
    `ranked_rows`.`rn` AS `rn`
from
    `ranked_rows`
where
    ((`ranked_rows`.`side` = 'SELL')
        and (`ranked_rows`.`rn` = 1))),
`final_report` as (
select
    row_number() OVER (PARTITION BY cast((`ta`.`sort_created` + interval 9 hour) as date)
ORDER BY
    `ta`.`sort_created`,
    `ta`.`trade_id` ) AS `BOKId`,
    `ta`.`trade_id` AS `trade_id`,
    `ta`.`report_group_key` AS `report_group_key`,
    `ta`.`symbol` AS `symbol`,
    `ta`.`inst_type` AS `inst_type`,
    `ta`.`tenor` AS `tenor`,
    `ta`.`product_type` AS `product_type`,
    `ta`.`price` AS `price`,
    `ta`.`Original Quantity` AS `Original Quantity`,
    `ta`.`Filled Quantity` AS `Filled Quantity`,
    `ta`.`Term Filled Quantity` AS `Term Filled Quantity`,
    `ta`.`status` AS `status`,
    `ta`.`Settle Date` AS `Settle Date`,
    `ta`.`Fixing Date` AS `Fixing Date`,
    `ta`.`Trade Date` AS `Trade Date`,
    `b`.`client_order_id` AS `Buy.client_order_id`,
    `b`.`created` AS `Buy.created`,
    `b`.`modified` AS `Buy.modified`,
    `b`.`executing_trader` AS `Buy.Executing_Trader`,
    `b`.`executing_organisation` AS `Buy.Executing_Organisation`,
    `b`.`counterparty_user` AS `Buy.Counterparty_User`,
    `b`.`counterparty_organisation` AS `Buy.Counterparty_Organisation`,
    `b`.`brokerage_commission` AS `Buy.brokerage_commission`,
    `b`.`brokerage_currency` AS `Buy.brokerage_currency`,
    `s`.`client_order_id` AS `Sell.client_order_id`,
    `s`.`created` AS `Sell.created`,
    `s`.`modified` AS `Sell.modified`,
    `s`.`executing_trader` AS `Sell.Executing_Trader`,
    `s`.`executing_organisation` AS `Sell.Executing_Organisation`,
    `s`.`counterparty_user` AS `Sell.Counterparty_User`,
    `s`.`counterparty_organisation` AS `Sell.Counterparty_Organisation`,
    `s`.`brokerage_commission` AS `Sell.brokerage_commission`,
    `s`.`brokerage_currency` AS `Sell.brokerage_currency`,
    `ta`.`Near Leg price` AS `Near Leg price`,
    `ta`.`Near Leg Settle` AS `Near Leg Settle`,
    `ta`.`Near Leg Fixing` AS `Near Leg Fixing`,
    `ta`.`tenor_near` AS `tenor_near`,
    `ta`.`Far Leg price` AS `Far Leg price`,
    `ta`.`Far Leg Settle` AS `Far Leg Settle`,
    `ta`.`Far Leg Fixing` AS `Far Leg Fixing`,
    `ta`.`tenor_far` AS `tenor_far`,
    `ta`.`sort_created` AS `sort_created`
from
    ((`trade_agg` `ta`
left join `buy_side` `b` on
    ((`b`.`report_group_key` = `ta`.`report_group_key`)))
left join `sell_side` `s` on
    ((`s`.`report_group_key` = `ta`.`report_group_key`)))
where
    NULLIF(TRIM(`b`.`client_order_id`), '') IS NOT NULL),
`leg_report` as (
select
    `final_report`.`BOKId` AS `BOKId`,
    `final_report`.`trade_id` AS `trade_id`,
    `final_report`.`report_group_key` AS `report_group_key`,
    `final_report`.`symbol` AS `symbol`,
    `final_report`.`inst_type` AS `inst_type`,
    `final_report`.`tenor` AS `tenor`,
    `final_report`.`product_type` AS `product_type`,
    `final_report`.`price` AS `price`,
    `final_report`.`Original Quantity` AS `Original Quantity`,
    `final_report`.`Filled Quantity` AS `Filled Quantity`,
    `final_report`.`Term Filled Quantity` AS `Term Filled Quantity`,
    `final_report`.`status` AS `status`,
    `final_report`.`Settle Date` AS `Settle Date`,
    `final_report`.`Fixing Date` AS `Fixing Date`,
    `final_report`.`Trade Date` AS `Trade Date`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.client_order_id`
        else `final_report`.`Buy.client_order_id`
    end) AS `Buy.client_order_id`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.created`
        else `final_report`.`Buy.created`
    end) AS `Buy.created`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.modified`
        else `final_report`.`Buy.modified`
    end) AS `Buy.modified`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.Executing_Trader`
        else `final_report`.`Buy.Executing_Trader`
    end) AS `Buy.Executing_Trader`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.Executing_Organisation`
        else `final_report`.`Buy.Executing_Organisation`
    end) AS `Buy.Executing_Organisation`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.Counterparty_User`
        else `final_report`.`Buy.Counterparty_User`
    end) AS `Buy.Counterparty_User`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.Counterparty_Organisation`
        else `final_report`.`Buy.Counterparty_Organisation`
    end) AS `Buy.Counterparty_Organisation`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.brokerage_commission`
        else `final_report`.`Buy.brokerage_commission`
    end) AS `Buy.brokerage_commission`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Sell.brokerage_currency`
        else `final_report`.`Buy.brokerage_currency`
    end) AS `Buy.brokerage_currency`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.client_order_id`
        else `final_report`.`Sell.client_order_id`
    end) AS `Sell.client_order_id`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.created`
        else `final_report`.`Sell.created`
    end) AS `Sell.created`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.modified`
        else `final_report`.`Sell.modified`
    end) AS `Sell.modified`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.Executing_Trader`
        else `final_report`.`Sell.Executing_Trader`
    end) AS `Sell.Executing_Trader`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.Executing_Organisation`
        else `final_report`.`Sell.Executing_Organisation`
    end) AS `Sell.Executing_Organisation`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.Counterparty_User`
        else `final_report`.`Sell.Counterparty_User`
    end) AS `Sell.Counterparty_User`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.Counterparty_Organisation`
        else `final_report`.`Sell.Counterparty_Organisation`
    end) AS `Sell.Counterparty_Organisation`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.brokerage_commission`
        else `final_report`.`Sell.brokerage_commission`
    end) AS `Sell.brokerage_commission`,
    (case
        when ((`final_report`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))
            or ((`final_report`.`product_type` = 'NDF')
                and (`final_report`.`tenor` in ('MAR', 'TOMMAR')))) then `final_report`.`Buy.brokerage_currency`
        else `final_report`.`Sell.brokerage_currency`
    end) AS `Sell.brokerage_currency`,
    (case
        when ((`final_report`.`product_type` in ('OUTRIGHT', 'NDF'))
            and (`final_report`.`tenor` not in ('MAR', 'TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))) then ''
        else 'NEAR'
    end) AS `leg_type`,
    coalesce(`final_report`.`Near Leg price`, `final_report`.`price`) AS `leg_price`,
    coalesce(`final_report`.`Near Leg Settle`, `final_report`.`Settle Date`, `final_report`.`Far Leg Settle`) AS `leg_settle_date`,
    coalesce(`final_report`.`Near Leg Fixing`, `final_report`.`Fixing Date`, `final_report`.`Far Leg Fixing`) AS `leg_fixing_date`,
    coalesce(`final_report`.`tenor_near`, `final_report`.`tenor`) AS `leg_tenor`,
    `final_report`.`sort_created` AS `sort_created`
from
    `final_report`
union all
select
    `final_report`.`BOKId` AS `BOKId`,
    `final_report`.`trade_id` AS `trade_id`,
    `final_report`.`report_group_key` AS `report_group_key`,
    `final_report`.`symbol` AS `symbol`,
    `final_report`.`inst_type` AS `inst_type`,
    `final_report`.`tenor` AS `tenor`,
    `final_report`.`product_type` AS `product_type`,
    `final_report`.`price` AS `price`,
    `final_report`.`Original Quantity` AS `Original Quantity`,
    `final_report`.`Filled Quantity` AS `Filled Quantity`,
    `final_report`.`Term Filled Quantity` AS `Term Filled Quantity`,
    `final_report`.`status` AS `status`,
    `final_report`.`Settle Date` AS `Settle Date`,
    `final_report`.`Fixing Date` AS `Fixing Date`,
    `final_report`.`Trade Date` AS `Trade Date`,
    `final_report`.`Buy.client_order_id` AS `Buy.client_order_id`,
    `final_report`.`Buy.created` AS `Buy.created`,
    `final_report`.`Buy.modified` AS `Buy.modified`,
    `final_report`.`Buy.Executing_Trader` AS `Buy.Executing_Trader`,
    `final_report`.`Buy.Executing_Organisation` AS `Buy.Executing_Organisation`,
    `final_report`.`Buy.Counterparty_User` AS `Buy.Counterparty_User`,
    `final_report`.`Buy.Counterparty_Organisation` AS `Buy.Counterparty_Organisation`,
    `final_report`.`Buy.brokerage_commission` AS `Buy.brokerage_commission`,
    `final_report`.`Buy.brokerage_currency` AS `Buy.brokerage_currency`,
    `final_report`.`Sell.client_order_id` AS `Sell.client_order_id`,
    `final_report`.`Sell.created` AS `Sell.created`,
    `final_report`.`Sell.modified` AS `Sell.modified`,
    `final_report`.`Sell.Executing_Trader` AS `Sell.Executing_Trader`,
    `final_report`.`Sell.Executing_Organisation` AS `Sell.Executing_Organisation`,
    `final_report`.`Sell.Counterparty_User` AS `Sell.Counterparty_User`,
    `final_report`.`Sell.Counterparty_Organisation` AS `Sell.Counterparty_Organisation`,
    `final_report`.`Sell.brokerage_commission` AS `Sell.brokerage_commission`,
    `final_report`.`Sell.brokerage_currency` AS `Sell.brokerage_currency`,
    (case
        when ((`final_report`.`product_type` in ('OUTRIGHT', 'NDF'))
            and (`final_report`.`tenor` not in ('MAR', 'TOMMAR', 'NDF_MAR', 'NDF_TOMMAR'))) then ''
        else 'FAR'
    end) AS `leg_type`,
    `final_report`.`Far Leg price` AS `leg_price`,
    `final_report`.`Far Leg Settle` AS `leg_settle_date`,
    `final_report`.`Far Leg Fixing` AS `leg_fixing_date`,
    coalesce(`final_report`.`tenor_far`, `final_report`.`tenor`) AS `leg_tenor`,
    `final_report`.`sort_created` AS `sort_created`
from
    `final_report`),
`mapped_leg_report` as (
select
    `lr`.`BOKId` AS `BOKId`,
    `lr`.`trade_id` AS `trade_id`,
    `lr`.`report_group_key` AS `report_group_key`,
    `lr`.`symbol` AS `symbol`,
    `lr`.`inst_type` AS `inst_type`,
    `lr`.`tenor` AS `tenor`,
    `lr`.`product_type` AS `product_type`,
    `lr`.`price` AS `price`,
    `lr`.`Original Quantity` AS `Original Quantity`,
    `lr`.`Filled Quantity` AS `Filled Quantity`,
    `lr`.`Term Filled Quantity` AS `Term Filled Quantity`,
    `lr`.`status` AS `status`,
    `lr`.`Settle Date` AS `Settle Date`,
    `lr`.`Fixing Date` AS `Fixing Date`,
    `lr`.`Trade Date` AS `Trade Date`,
    `lr`.`Buy.client_order_id` AS `Buy.client_order_id`,
    `lr`.`Buy.created` AS `Buy.created`,
    `lr`.`Buy.modified` AS `Buy.modified`,
    `lr`.`Buy.Executing_Trader` AS `Buy.Executing_Trader`,
    `lr`.`Buy.Executing_Organisation` AS `Buy.Executing_Organisation`,
    `lr`.`Buy.Counterparty_User` AS `Buy.Counterparty_User`,
    `lr`.`Buy.Counterparty_Organisation` AS `Buy.Counterparty_Organisation`,
    `lr`.`Buy.brokerage_commission` AS `Buy.brokerage_commission`,
    `lr`.`Buy.brokerage_currency` AS `Buy.brokerage_currency`,
    `lr`.`Sell.client_order_id` AS `Sell.client_order_id`,
    `lr`.`Sell.created` AS `Sell.created`,
    `lr`.`Sell.modified` AS `Sell.modified`,
    `lr`.`Sell.Executing_Trader` AS `Sell.Executing_Trader`,
    `lr`.`Sell.Executing_Organisation` AS `Sell.Executing_Organisation`,
    `lr`.`Sell.Counterparty_User` AS `Sell.Counterparty_User`,
    `lr`.`Sell.Counterparty_Organisation` AS `Sell.Counterparty_Organisation`,
    `lr`.`Sell.brokerage_commission` AS `Sell.brokerage_commission`,
    `lr`.`Sell.brokerage_currency` AS `Sell.brokerage_currency`,
    `lr`.`leg_type` AS `leg_type`,
    `lr`.`leg_price` AS `leg_price`,
    `lr`.`leg_settle_date` AS `leg_settle_date`,
    `lr`.`leg_fixing_date` AS `leg_fixing_date`,
    `lr`.`leg_tenor` AS `leg_tenor`,
    `lr`.`sort_created` AS `sort_created`,
    (case
        when ((`lr`.`tenor` in ('BASIS_MAR', 'BASIS_TOMMAR'))
            and (`lr`.`leg_type` = 'FAR')) then 'DF'
        when (`lr`.`product_type` = 'OUTRIGHT') then 'DF'
        when (`lr`.`product_type` = 'NDF') then 'NDF'
        when ((`lr`.`product_type` = 'SWAP')
            and (`lr`.`tenor` in ('MAR', 'TOMMAR', 'NDF_MAR', 'NDF_TOMMAR', 'BASIS_MAR', 'BASIS_TOMMAR'))) then 'NDF'
        else `lr`.`product_type`
    end) AS `report_product`,
    (case
        when (`lr`.`product_type` = 'OUTRIGHT') then '1'
        when (`lr`.`tenor` = 'SP') then '1'
        when (`lr`.`tenor` = '1M') then '3'
        when ((`lr`.`tenor` = 'MAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'MAR')
            and (`lr`.`leg_type` = 'FAR')) then '3'
        when ((`lr`.`tenor` = 'TOMMAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'TOMMAR')
            and (`lr`.`leg_type` = 'FAR')) then '3'
        when ((`lr`.`tenor` = 'NDF_MAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'NDF_MAR')
            and (`lr`.`leg_type` = 'FAR')) then '3'
        when ((`lr`.`tenor` = 'NDF_TOMMAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'NDF_TOMMAR')
            and (`lr`.`leg_type` = 'FAR')) then '3'
        when ((`lr`.`tenor` = 'BASIS_MAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'BASIS_MAR')
            and (`lr`.`leg_type` = 'FAR')) then '1'
        when ((`lr`.`tenor` = 'BASIS_TOMMAR')
            and (`lr`.`leg_type` = 'NEAR')) then '3'
        when ((`lr`.`tenor` = 'BASIS_TOMMAR')
            and (`lr`.`leg_type` = 'FAR')) then '1'
        else NULL
    end) AS `Listed/OTC`
from
    `leg_report` `lr`)
select
    `mapped_leg_report`.`BOKId` AS `BOKId`,
    `mapped_leg_report`.`trade_id` AS `trade_id`,
    `mapped_leg_report`.`symbol` AS `symbol`,
    `mapped_leg_report`.`report_product` AS `PRODUCT`,
    `mapped_leg_report`.`leg_tenor` AS `TYPE`,
    `mapped_leg_report`.`leg_type` AS `LEG_TYPE`,
    `mapped_leg_report`.`Listed/OTC` AS `Listed/OTC`,
    `mapped_leg_report`.`price` AS `price`,
    `mapped_leg_report`.`Original Quantity` AS `Original Quantity`,
    `mapped_leg_report`.`Filled Quantity` AS `Filled Quantity`,
    `mapped_leg_report`.`Term Filled Quantity` AS `Term Filled Quantity`,
    `mapped_leg_report`.`status` AS `status`,
    str_to_date(cast(`mapped_leg_report`.`Settle Date` as char charset utf8mb4), '%Y%m%d') AS `Settle Date`,
    str_to_date(cast(`mapped_leg_report`.`Fixing Date` as char charset utf8mb4), '%Y%m%d') AS `Fixing Date`,
    str_to_date(cast(`mapped_leg_report`.`Trade Date` as char charset utf8mb4), '%Y%m%d') AS `Trade Date`,
    `mapped_leg_report`.`Buy.client_order_id` AS `Buy.client_order_id`,
    `mapped_leg_report`.`Buy.created` AS `Buy.created`,
    `mapped_leg_report`.`Buy.modified` AS `Buy.modified`,
    `mapped_leg_report`.`Buy.Executing_Trader` AS `Buy.Executing_Trader`,
    `mapped_leg_report`.`Buy.Executing_Organisation` AS `Buy.Executing_Organisation`,
    `mapped_leg_report`.`Buy.Counterparty_User` AS `Buy.Counterparty_User`,
    `mapped_leg_report`.`Buy.Counterparty_Organisation` AS `Buy.Counterparty_Organisation`,
    `mapped_leg_report`.`Buy.brokerage_commission` AS `Buy.brokerage_commission`,
    `mapped_leg_report`.`Buy.brokerage_currency` AS `Buy.brokerage_currency`,
    `mapped_leg_report`.`Sell.client_order_id` AS `Sell.client_order_id`,
    `mapped_leg_report`.`Sell.created` AS `Sell.created`,
    `mapped_leg_report`.`Sell.modified` AS `Sell.modified`,
    `mapped_leg_report`.`Sell.Executing_Trader` AS `Sell.Executing_Trader`,
    `mapped_leg_report`.`Sell.Executing_Organisation` AS `Sell.Executing_Organisation`,
    `mapped_leg_report`.`Sell.Counterparty_User` AS `Sell.Counterparty_User`,
    `mapped_leg_report`.`Sell.Counterparty_Organisation` AS `Sell.Counterparty_Organisation`,
    `mapped_leg_report`.`Sell.brokerage_commission` AS `Sell.brokerage_commission`,
    `mapped_leg_report`.`Sell.brokerage_currency` AS `Sell.brokerage_currency`,
    `mapped_leg_report`.`leg_price` AS `leg_price`,
    (case
        when ((`mapped_leg_report`.`leg_settle_date` is null)
            or (`mapped_leg_report`.`leg_settle_date` = '')) then NULL
        when (cast(`mapped_leg_report`.`leg_settle_date` as char charset utf8mb4) like '%-%') then str_to_date(substr(cast(`mapped_leg_report`.`leg_settle_date` as char charset utf8mb4), 1, 10), '%Y-%m-%d')
        else str_to_date(cast(`mapped_leg_report`.`leg_settle_date` as char charset utf8mb4), '%Y%m%d')
    end) AS `leg_settle_date`,
    (case
        when ((`mapped_leg_report`.`leg_fixing_date` is null)
            or (`mapped_leg_report`.`leg_fixing_date` = '')) then NULL
        when (cast(`mapped_leg_report`.`leg_fixing_date` as char charset utf8mb4) like '%-%') then str_to_date(substr(cast(`mapped_leg_report`.`leg_fixing_date` as char charset utf8mb4), 1, 10), '%Y-%m-%d')
        else str_to_date(cast(`mapped_leg_report`.`leg_fixing_date` as char charset utf8mb4), '%Y%m%d')
    end) AS `leg_fixing_date`
from
    `mapped_leg_report`
where
    (`mapped_leg_report`.`leg_price` is not null)
order by
    `mapped_leg_report`.`sort_created` desc,
    `mapped_leg_report`.`trade_id`,
    field(`mapped_leg_report`.`leg_type`, 'NEAR', 'FAR');

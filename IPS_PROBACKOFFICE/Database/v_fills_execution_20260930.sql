-- User-provided definition, preserved without changing execution pairing.
CREATE OR REPLACE ALGORITHM=UNDEFINED VIEW ips_ew.v_fills_execution AS
WITH ranked_fills AS (
SELECT
    f.`created` AS `created`,
    f.`modified` AS `modified`,
    f.`trade_id` AS `trade_id`,
    f.`fk_orders` AS `fk_orders`,
    f.`side` AS `side`,
    f.`qty` AS `qty`,
    f.`qty_fracc` AS `qty_fracc`,
    f.`price` AS `price`,
    f.`aggressive` AS `aggressive`,
    f.`instrument` AS `instrument`,
    f.`source` AS `source`,
    f.`tenor` AS `tenor`,
    f.`product` AS `product`,
    f.`settle_date` AS `settle_date`,
    f.`client_order_id` AS `client_order_id`,
    f.`orig_client_order_id` AS `orig_client_order_id`,
    f.`counter_party` AS `counter_party`,
    ROW_NUMBER() OVER (PARTITION BY f.trade_id, UPPER(TRIM(f.side)) ORDER BY f.created, f.modified, f.fk_orders, f.client_order_id) AS execution_seq,
    COUNT(0) OVER (PARTITION BY f.trade_id, UPPER(TRIM(f.side))) AS side_count
FROM mex_prod.fills f
)
SELECT
    ranked_fills.`created` AS `created`,
    ranked_fills.`modified` AS `modified`,
    CASE WHEN ranked_fills.side_count > 1 THEN CONCAT(ranked_fills.trade_id, '|', CONVERT(LPAD(ranked_fills.execution_seq, 4, '0') USING latin1)) ELSE ranked_fills.trade_id END AS trade_id,
    ranked_fills.`fk_orders` AS `fk_orders`,
    ranked_fills.`side` AS `side`,
    ranked_fills.`qty` AS `qty`,
    ranked_fills.`qty_fracc` AS `qty_fracc`,
    ranked_fills.`price` AS `price`,
    ranked_fills.`aggressive` AS `aggressive`,
    ranked_fills.`instrument` AS `instrument`,
    ranked_fills.`source` AS `source`,
    ranked_fills.`tenor` AS `tenor`,
    ranked_fills.`product` AS `product`,
    ranked_fills.`settle_date` AS `settle_date`,
    ranked_fills.`client_order_id` AS `client_order_id`,
    ranked_fills.`orig_client_order_id` AS `orig_client_order_id`,
    ranked_fills.`counter_party` AS `counter_party`
FROM ranked_fills;

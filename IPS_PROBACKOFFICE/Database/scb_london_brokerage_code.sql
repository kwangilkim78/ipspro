-- SCB London 기관코드는 하이픈을 포함하므로 문자열로 저장한다.
-- 문자열 기관코드를 지원하는 백오피스 버전과 함께 적용한다.
ALTER TABLE ips_ew.brokerage_fee_schedule
    MODIFY COLUMN inst_code VARCHAR(20) NOT NULL;

UPDATE ips_ew.brokerage_fee_schedule
SET inst_code = '1123-1'
WHERE inst_code = '1123' AND institution_name = 'SCB London';

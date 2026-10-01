# 2026-09-30 뷰 SQL 저장본

사용자가 DB 도구에서 적용한 SQL과 적용 후 조회 검증 결과를 보관한다.
현재 연결 계정에는 SHOW VIEW 권한이 없으므로 서버에서 다시 추출한 덤프가 아니라, 제공받은 정의와 적용한 SQL 기준이다.

## 적용된 정의와 실행 순서

1. `v64_bok_exclude_missing_buy.sql`: `ips_ew.v_trading_report`. 매수 주문 ID가 NULL 또는 빈 문자열인 행을 BOK 번호 생성 전에 제외한다.
2. `v65_01_create_fee_source.sql`: `ips_ew.v_trading_report_fee_source`. 제외 전 보고서 로직을 별도 뷰로 보존하여 매수와 연결되지 않은 매도 수수료도 유지한다.
3. `v65_02_update_backoffice.sql`: `ips_ew.v_trading_report_backoffice`. 수수료 원본 뷰를 참조하며 기존 스위치 거래 정보를 결합한다.

각 파일을 위 순서대로 별도로 실행한다. 기존 `v65_fee_source_preserve_sell.sql`은 2번과 3번을 합친 파일이므로 중복 실행할 필요가 없다.

## 검증 결과

- 2026-09-30 한국시간 08:00부터 다음 날 08:00까지 조회.
- 적용 후 수수료 보고서 결과 18행, 수수료 합계 230,000원.
- 농협 매도 6행, 수수료 합계 90,000원.
- 사용자 프로그램에서도 수수료 정상 표시 확인.
- 행 수는 수수료용 보고서 행 수이며, 매수·매도가 연결된 실제 거래 건수와 같다는 의미는 아니다.

## 남아 있는 제한

`v_fills_execution`의 매수·매도 체결 ID 불일치 자체는 수정하지 않았다. BOK에서는 매수 없는 행을 제외하고, 수수료 집계에서는 그 매도 행을 보존하는 방식이다. 같은 원본의 후속 수정 시 두 보고서의 목적을 구분해야 한다.

## 이전 정의 보관

- `v_fills_execution_20260930.sql`: 사용자가 제공한 현재 fills 뷰 정의. 이번 수수료 수정에서는 변경하지 않았으며, 신규 환경에서는 다른 보고서 뷰보다 먼저 생성한다.
- `v64_bok_exclude_missing_buy_rollback.sql`: 사용자가 제공한 BOK 제외 조건 적용 전 정의.
- `v65_fee_source_preserve_sell_rollback.sql`: 수수료 보고서가 BOK 뷰를 참조하던 이전 정의. 실행하면 이번에 복구한 수수료 누락이 다시 발생할 수 있다.

뷰 변경은 관리자 권한이 필요하다. backoffice 정의의 DEFINER는 제공받은 `root`@`10.10.2.61`을 유지했다.

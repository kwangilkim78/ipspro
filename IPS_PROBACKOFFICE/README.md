# IPS_PROBACKOFFICE

## v43 메뉴형 화면 (2026-08-11)

- 최상위 탭을 왼쪽 업무 메뉴와 오른쪽 작업 화면 구조로 변경
- 리포트, 수수료, 기준정보, 시스템 그룹으로 메뉴 분류
- 아이디별 화면 권한에 따라 허용된 메뉴만 표시
- 선택 메뉴 강조, 현재 화면 제목, 한글/영문 전환 및 화면 감사 로그 유지
- BOK, KB EOD, 뉴욕종가를 각각 독립된 왼쪽 메뉴 화면으로 분리

## 백오피스 로그인

- `Database/backoffice_login_tables.sql`을 MariaDB `ips_ew`에 먼저 실행합니다.
- 최초 활성 사용자가 없으면 `kikim / 1234`가 `ADMIN`으로 자동 생성됩니다.
- ADMIN은 `백오피스 사용자 관리` 탭에서 직원 계정을 직접 발급합니다.
- 신규 직원은 최초 로그인 시 관리자가 발급한 초기 비밀번호를 반드시 변경해야 합니다.
- 로그인 후 상단 `비밀번호 변경` 버튼에서도 본인 비밀번호를 변경할 수 있습니다.
- 로그인창의 `아이디/비밀번호 저장`, `자동 로그인`을 선택할 수 있습니다.
- 저장된 로그인 정보는 Windows 사용자 전용 DPAPI 암호화 파일로 보관됩니다.
- 프로그램에서 로그아웃한 직후에는 저장 설정과 관계없이 로그인창을 표시합니다.
- 권한은 `ADMIN`, `OPS`, `VIEWER`를 사용합니다.
- 비밀번호는 PBKDF2-SHA256 해시로 저장되고 주요 실행 내역은 `backoffice_audit_log`에 기록됩니다.

`ips_pro`와 `InvoiceReportPrinter_switch_fee`를 하나의 WinForms 실행 프로젝트로 통합한 버전입니다.

## 포함 화면

- Invoice: 월별 거래 조회, SWITCH FEE 포함 인보이스 생성
- BOK report
- KB BANK EOD
- 환율입력: 저장 및 SFTP 전송
- 기관별 유저정보: 기관 선택 후 등록 사용자 조회
- 전체 기관 한도 매트릭스: 활성 기관 전체를 행·열에 배치하고 기관 쌍별 상품 한도를 한 화면에 표시

## 실행 및 배포

Visual Studio 2022 이상에서 `IPS_PROBACKOFFICE.sln`을 열어 빌드합니다. 다른 PC에 배포할 때는 게시(Publish) 결과 폴더 전체를 복사해야 하며, `appsettings.json`, `Templates`, `excel_template`, `reference_rates.csv`가 함께 있어야 합니다.

SFTP 전송 기능을 사용하려면 실행 계정에 `IPSPROBO_SFTP_PASSWORD` 환경변수를 설정해야 합니다. 필요하면 `IPSPROBO_SFTP_HOST`, `IPSPROBO_SFTP_USER`도 설정할 수 있습니다.

환율 저장 경로 `Y:\전자거래사업본부\뉴욕종가`와 DB/SFTP 접속 환경은 대상 PC에서도 접근 가능해야 합니다.

CREATE TABLE IF NOT EXISTS `backoffice_user` (
  `user_id` bigint NOT NULL AUTO_INCREMENT,
  `login_id` varchar(50) NOT NULL,
  `user_name` varchar(100) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `must_change_password` tinyint(1) NOT NULL DEFAULT '1',
  `role_code` varchar(30) NOT NULL,
  `email` varchar(150) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `last_login_at` datetime DEFAULT NULL,
  `password_changed_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `uk_backoffice_user_login_id` (`login_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `backoffice_audit_log` (
  `log_id` bigint NOT NULL AUTO_INCREMENT,
  `user_id` bigint NOT NULL,
  `action_type` varchar(50) NOT NULL,
  `target_name` varchar(200) DEFAULT NULL,
  `action_detail` text,
  `action_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `client_ip` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`log_id`),
  KEY `ix_backoffice_audit_user_date` (`user_id`,`action_at`),
  CONSTRAINT `fk_backoffice_audit_user`
    FOREIGN KEY (`user_id`) REFERENCES `backoffice_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `backoffice_user_screen_permission` (
  `login_id` varchar(50) NOT NULL,
  `screen_key` varchar(50) NOT NULL,
  `can_view` tinyint(1) NOT NULL DEFAULT '0',
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`login_id`,`screen_key`),
  CONSTRAINT `fk_backoffice_screen_permission_user`
    FOREIGN KEY (`login_id`) REFERENCES `backoffice_user` (`login_id`)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 테이블이 비어 있으면 프로그램 최초 실행 시 아래 계정을 자동 생성합니다.
-- ID: kikim / 초기 비밀번호: 1234
-- 최초 계정은 USER로 생성되며 모든 화면 권한이 자동 등록됩니다.
-- 비밀번호는 평문이 아닌 PBKDF2 해시로 저장됩니다.

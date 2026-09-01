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

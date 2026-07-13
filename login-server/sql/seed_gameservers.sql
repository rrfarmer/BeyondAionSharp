-- Seed the login server's `gameservers` table so a game server may register.
-- The login server authorizes an inbound GS by looking up its id in this table and
-- comparing the password (plaintext, ordinal) + IP mask. Without a matching row the
-- GS receives NOT_AUTHED and never registers.
--
-- Defaults below match the checked-in game-server config:
--   gameserver.network.id               = 1     (NetworkConfig.GAMESERVER_ID)
--   gameserver.network.login.password   = 1234  (game-server/config/network/network.properties)
--   mask = '*'                          -> allow any source IP (GS connects from 127.0.0.1/::1 in local dev)
--
-- Run against the aion_ls database, e.g.:
--   docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_ls < login-server/sql/seed_gameservers.sql

INSERT INTO `gameservers` (`id`, `mask`, `password`)
VALUES (1, '*', '1234')
ON DUPLICATE KEY UPDATE `mask` = VALUES(`mask`), `password` = VALUES(`password`);

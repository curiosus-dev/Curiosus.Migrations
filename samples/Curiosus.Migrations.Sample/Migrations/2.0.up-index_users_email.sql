-- CREATE INDEX CONCURRENTLY can't run in a transaction and can take long on a big table.
-- CURIOSUS: TRANSACTION = OFF
-- CURIOSUS: LONG-RUNNING = TRUE
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_users_email_normalized ON users (email_normalized);

-- Migration: scope form_defaults rows by user + form
-- Run after applying 001_schema_postgres.sql in environments with existing data.

ALTER TABLE form_defaults ADD COLUMN IF NOT EXISTS user_id uuid NULL REFERENCES users(id) ON DELETE CASCADE;

-- Backfill legacy rows by assigning them to the admin user (or fall back to the first user).
UPDATE form_defaults
SET user_id = COALESCE(
    (SELECT id FROM users WHERE user_name = 'admin' LIMIT 1),
    (SELECT id FROM users LIMIT 1)
)
WHERE user_id IS NULL;

ALTER TABLE form_defaults
    ALTER COLUMN user_id SET NOT NULL;

ALTER TABLE form_defaults DROP CONSTRAINT IF EXISTS uq_form_default;
ALTER TABLE form_defaults
    ADD CONSTRAINT uq_form_default UNIQUE (user_id, form_id, field_name);

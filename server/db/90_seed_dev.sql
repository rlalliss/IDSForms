-- Development-only seed: set or reset admin password using pgcrypto (bcrypt)
-- Change the plaintext below before running in any environment.

-- Ensure extensions are available
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Create admin if not exists
INSERT INTO users (id, user_name, password_hash)
SELECT uuid_generate_v4(), 'admin', crypt('Passw0rd!', gen_salt('bf', 11))
WHERE NOT EXISTS (SELECT 1 FROM users WHERE user_name = 'admin');

-- Ensure profile exists
INSERT INTO user_profiles (user_id, full_name, email)
SELECT id, 'Admin User', 'admin@example.com' FROM users WHERE user_name = 'admin'
ON CONFLICT (user_id) DO NOTHING;

-- Reset admin password (safe to re-run)
UPDATE users SET password_hash = crypt('aa', gen_salt('bf', 11))
WHERE user_name = 'admin';

-- Output the admin user id for convenience
-- SELECT id, user_name FROM users WHERE user_name = 'admin';


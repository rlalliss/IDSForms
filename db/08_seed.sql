SET search_path = app, public;

INSERT INTO users (id, email, display_name, company, role)
VALUES
  (uuid_generate_v4(), 'admin@example.com', 'Admin User', 'DemoCo', 'Admin'),
  (uuid_generate_v4(), 'user@example.com',  'Test User',  'DemoCo', 'User')
ON CONFLICT DO NOTHING;

INSERT INTO forms (id, slug, title, pdf_path, is_active, version, created_by)
VALUES
  (uuid_generate_v4(), 'test-drive-agreement', 'Test Drive Agreement', '/pdfs/test-drive-agreement.pdf', true, 1,
   (SELECT id FROM users WHERE email = 'admin@example.com' LIMIT 1))
ON CONFLICT DO NOTHING;

INSERT INTO user_defaults (user_id, company, full_name, email_from, phone, extras_json)
SELECT u.id, 'DemoCo', u.display_name, u.email, '+1-555-0100', '{}'::jsonb
FROM users u WHERE u.email = 'user@example.com'
ON CONFLICT (user_id) DO NOTHING;

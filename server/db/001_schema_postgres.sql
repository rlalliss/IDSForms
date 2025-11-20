-- PostgreSQL schema for IDSForms / PDF app
-- Requires: CREATE EXTENSION "uuid-ossp"; permission on DB

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users and profiles
CREATE TABLE IF NOT EXISTS users (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  user_name text NOT NULL UNIQUE,
  password_hash text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS user_profiles (
  user_id uuid PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
  full_name text NOT NULL,
  company text NULL,
  email text NOT NULL,
  secondary_emails text NULL
);

-- Forms and related metadata
CREATE TABLE IF NOT EXISTS forms (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  slug text NOT NULL UNIQUE,
  title text NOT NULL,
  description text NULL,
  keywords text NULL,
  version int NOT NULL DEFAULT 1,
  category text NULL,
  pdf_blob_path text NOT NULL,
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS form_fields (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  form_id uuid NOT NULL REFERENCES forms(id) ON DELETE CASCADE,
  pdf_field_name text NOT NULL,
  label text NOT NULL,
  type text NOT NULL DEFAULT 'text',
  placeholder text NULL,
  required boolean NOT NULL DEFAULT false,
  regex text NULL,
  min int NULL,
  max int NULL,
  options_json jsonb NULL,
  default_value text NULL,
  group_name text NULL,
  order_index int NOT NULL DEFAULT 0,
  CONSTRAINT uq_form_field UNIQUE (form_id, pdf_field_name)
);

CREATE TABLE IF NOT EXISTS form_defaults (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  form_id uuid NOT NULL REFERENCES forms(id) ON DELETE CASCADE,
  field_name text NOT NULL,
  field_value text NULL,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_form_default UNIQUE (user_id, form_id, field_name)
);

CREATE TABLE IF NOT EXISTS user_defaults (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  form_id uuid NULL REFERENCES forms(id) ON DELETE CASCADE,
  form_slug text NULL,
  field_name text NOT NULL,
  field_value text NULL,
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_user_defaults_global ON user_defaults(user_id) WHERE form_slug IS NULL;
CREATE INDEX IF NOT EXISTS idx_user_defaults_by_slug ON user_defaults(user_id, form_slug);

CREATE TABLE IF NOT EXISTS email_templates (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  form_id uuid NOT NULL UNIQUE REFERENCES forms(id) ON DELETE CASCADE,
  subject text NOT NULL,
  body_html text NOT NULL,
  "to" text NOT NULL,
  cc text NULL,
  bcc text NULL,
  from_override text NULL,
  attach_flattened boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS submissions (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  form_id uuid NOT NULL REFERENCES forms(id) ON DELETE SET NULL,
  user_id uuid NOT NULL REFERENCES users(id) ON DELETE SET NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  payload_json jsonb NOT NULL,
  pdf_path text NOT NULL,
  email_message_id text NULL
);
CREATE INDEX IF NOT EXISTS idx_submissions_form ON submissions(form_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_submissions_user ON submissions(user_id, created_at DESC);

-- Signature workflow
-- Optional: DB storage for templates
CREATE TABLE IF NOT EXISTS form_files (
  id uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  form_id uuid NOT NULL REFERENCES forms(id) ON DELETE CASCADE,
  file_name text NOT NULL,
  content bytea NOT NULL,
  content_type text NOT NULL,
  version int NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT now()
);

-- Seed data (change password hash before production)
INSERT INTO users (id, user_name, password_hash)
VALUES (uuid_generate_v4(), 'admin', '$2a$11$REPLACE_WITH_REAL_BCRYPT')
ON CONFLICT (user_name) DO NOTHING;

INSERT INTO user_profiles (user_id, full_name, email)
SELECT id, 'Admin User', 'admin@example.com' FROM users WHERE user_name = 'admin'
ON CONFLICT (user_id) DO NOTHING;

INSERT INTO forms (id, slug, title, pdf_blob_path, is_active)
VALUES (uuid_generate_v4(), 'sample-form', 'Sample Form', 'blob://pdfs/sample.pdf', true)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO email_templates (id, form_id, subject, body_html, "to")
SELECT uuid_generate_v4(), id, 'Form {{Title}} Submission', '<p>A form was submitted by {{CustomerName}}.</p>', 'dest@example.com'
FROM forms WHERE slug = 'sample-form'
ON CONFLICT (form_id) DO NOTHING;

INSERT INTO form_fields (id, form_id, pdf_field_name, label, type, required, order_index)
SELECT uuid_generate_v4(), f.id, 'CustomerName', 'Customer Name', 'text', true, 1 FROM forms f WHERE f.slug = 'sample-form'
ON CONFLICT DO NOTHING;
INSERT INTO form_fields (id, form_id, pdf_field_name, label, type, required, order_index)
SELECT uuid_generate_v4(), f.id, 'CustomerSignature', 'Customer Signature', 'signature', true, 2 FROM forms f WHERE f.slug = 'sample-form'
ON CONFLICT DO NOTHING;

INSERT INTO form_defaults (id, user_id, form_id, field_name, field_value)
SELECT uuid_generate_v4(), u.id, f.id, 'Dealership', 'ACME Motors'
FROM forms f
JOIN users u ON u.user_name = 'admin'
WHERE f.slug = 'sample-form'
ON CONFLICT DO NOTHING;

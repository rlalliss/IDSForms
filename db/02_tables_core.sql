SET search_path = app, public;

CREATE TABLE IF NOT EXISTS users (
  id            uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  email         citext UNIQUE NOT NULL,
  display_name  text NOT NULL,
  company       text,
  role          text NOT NULL DEFAULT 'User',
  is_active     boolean NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS forms (
  id            uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  slug          text UNIQUE NOT NULL,
  title         text NOT NULL,
  pdf_path      text NOT NULL,
  is_active     boolean NOT NULL DEFAULT true,
  version       int NOT NULL DEFAULT 1,
  created_by    uuid REFERENCES users(id) ON DELETE SET NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS submissions (
  id             uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  form_id        uuid NOT NULL REFERENCES forms(id) ON DELETE RESTRICT,
  user_id        uuid REFERENCES users(id) ON DELETE SET NULL,
  status         text NOT NULL DEFAULT 'Ready',
  data_json      jsonb NOT NULL,
  email_to       text[],
  email_cc       text[],
  email_status   text,
  submitted_at   timestamptz NOT NULL DEFAULT now(),
  updated_at     timestamptz NOT NULL DEFAULT now(),
  customer_name  text,
  form_title     text
);

CREATE TABLE IF NOT EXISTS audit_log (
  id            bigserial PRIMARY KEY,
  at            timestamptz NOT NULL DEFAULT now(),
  actor_user_id uuid REFERENCES users(id) ON DELETE SET NULL,
  action        text NOT NULL,
  entity_type   text NOT NULL,
  entity_id     uuid,
  meta_json     jsonb
);

CREATE TABLE IF NOT EXISTS user_defaults (
  user_id       uuid PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
  company       text,
  full_name     text,
  email_from    text,
  phone         text,
  extras_json   jsonb,
  updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS attachments (
  id            uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
  submission_id uuid NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
  kind          text NOT NULL,
  file_name     text NOT NULL,
  content_type  text NOT NULL,
  storage_path  text NOT NULL,
  size_bytes    bigint CHECK (size_bytes >= 0),
  created_at    timestamptz NOT NULL DEFAULT now()
);

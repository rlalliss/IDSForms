SET search_path = app, public;

CREATE INDEX IF NOT EXISTS idx_users_email ON users (email);
CREATE INDEX IF NOT EXISTS idx_forms_active ON forms (is_active) WHERE is_active;

CREATE INDEX IF NOT EXISTS idx_submissions_form ON submissions (form_id);
CREATE INDEX IF NOT EXISTS idx_submissions_user ON submissions (user_id);
CREATE INDEX IF NOT EXISTS idx_submissions_status ON submissions (status);
CREATE INDEX IF NOT EXISTS idx_submissions_submitted_at ON submissions (submitted_at DESC);

CREATE INDEX IF NOT EXISTS idx_audit_entity ON audit_log (entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_at ON audit_log (at DESC);

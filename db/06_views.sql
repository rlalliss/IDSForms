SET search_path = app, public;

CREATE OR REPLACE VIEW vw_recent_submissions AS
SELECT
  s.id,
  s.form_id,
  s.user_id,
  s.status,
  s.submitted_at,
  s.updated_at,
  COALESCE(s.form_title, f.title) AS form_title,
  COALESCE(s.customer_name, '')   AS customer_name,
  u.display_name                  AS user_display_name
FROM submissions s
LEFT JOIN forms f ON f.id = s.form_id
LEFT JOIN users u ON u.id = s.user_id
ORDER BY s.submitted_at DESC;

CREATE OR REPLACE VIEW vw_form_popularity AS
SELECT
  f.id,
  f.title,
  COUNT(s.id) AS submission_count,
  MAX(s.submitted_at) AS last_submitted_at
FROM forms f
LEFT JOIN submissions s ON s.form_id = f.id
GROUP BY f.id, f.title
ORDER BY submission_count DESC, last_submitted_at DESC;

SET search_path = app, public;

CREATE OR REPLACE FUNCTION fn_dashboard_stats(p_user_id uuid DEFAULT NULL)
RETURNS TABLE (
  forms_available int,
  submissions_today int,
  pending_emails int
) LANGUAGE sql STABLE AS
$dir\06_views.sql
  WITH
  forms_ct AS (
    SELECT COUNT(*)::int AS c FROM forms WHERE is_active
  ),
  subs_today AS (
    SELECT COUNT(*)::int AS c
    FROM submissions
    WHERE submitted_at::date = (now() AT TIME ZONE 'UTC')::date
      AND (p_user_id IS NULL OR user_id = p_user_id)
  ),
  pending AS (
    SELECT COUNT(*)::int AS c
    FROM submissions
    WHERE status IN ('Ready')
  )
  SELECT f.c, t.c, p.c
  FROM forms_ct f, subs_today t, pending p;
$dir\06_views.sql;

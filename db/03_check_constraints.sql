SET search_path = app, public;

ALTER TABLE users
  ADD CONSTRAINT IF NOT EXISTS users_role_chk
  CHECK (role IN ('User','Admin'));

ALTER TABLE submissions
  ADD CONSTRAINT IF NOT EXISTS submissions_status_chk
  CHECK (status IN ('Ready','Emailed','Signed','Error'));

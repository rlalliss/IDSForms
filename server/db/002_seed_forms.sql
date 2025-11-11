-- Seed sample forms into the `forms` table
-- Safe to run multiple times (ON CONFLICT DO NOTHING)
-- Requires the schema from 001_schema_postgres.sql to be applied first.

INSERT INTO forms (id, slug, title, description, keywords, version, category, pdf_blob_path, is_active)
VALUES
  (uuid_generate_v4(), 'test-drive', 'Test Drive Agreement', 'Agreement for vehicle test drives', 'demo,drive,agreement', 1, 'Sales', 'https://idsblob.file.core.windows.net/pdf/Test_Drive_Agreement_Fillable_v11_consistent.pdf', true),
ON CONFLICT (slug) DO NOTHING;

-- Optionally verify rows
-- SELECT slug, title, category, pdf_blob_path FROM forms ORDER BY title;


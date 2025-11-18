select * from email_templates;
select * from form_defaults;
select * from form_fields;
select * from forms;
select * from signature_requirements;
select * from submission_signatures;
select * from submissions;
select * from user_defaults;
select * from user_profiles;
select * from users;

--delete from submissions

update forms set pdf_blob_path = replace(pdf_blob_path, 'https://idsblob.file.core.windows.net/pdf/', 'pdfs/')
where pdf_blob_path like 'C:\pdfs\%';

INSERT INTO forms (id, slug, title, description, keywords, version, category, pdf_blob_path, is_active)
VALUES
  (uuid_generate_v4(), 'mvc', 'MOTOR VEHICLE CONTRACT', 'Motor Vehicle Contract', 'vehicle,contract,mvc', 1, 'Sales', 'pdfs/MVC.pdf', true)

INSERT INTO forms (id, slug, title, description, keywords, version, category, pdf_blob_path, is_active)
VALUES
  (uuid_generate_v4(), 'test-drive', 'TEST DRIVE AGREEMENT', 'Test Drive Agreement', 'demo,drive,agreement', 1, 'Sales', 'pdfs/Test Drive Agreement.pdf', true)

INSERT INTO form_defaults (id, user_id, form_id, field_name, field_value)
SELECT uuid_generate_v4(), u.id, f.id, 'Dealership', 'ACME Motors'
FROM forms f
JOIN users u ON u.user_name = 'admin'
WHERE f.slug = 'sample-form'
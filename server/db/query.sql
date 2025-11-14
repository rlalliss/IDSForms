select * from forms

update forms set pdf_blob_path = replace(pdf_blob_path, 'https://idsblob.file.core.windows.net/pdf/', 'pdfs/')
where pdf_blob_path like 'C:\pdfs\%';
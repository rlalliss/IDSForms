import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api';
import { customerToPrefillPayload, readCustomerDraft } from '../customerStorage';
import PdfFormViewer, { PdfFormViewerHandle } from '../components/PdfFormViewer';

type FieldMeta = { pdfFieldName: string; label: string; type: string; required: boolean; orderIndex: number };
type MetaResp = { slug: string; title: string; pdfBlobPath: string; fields: FieldMeta[] };

export default function Fill() {
  const { slug: slugParam } = useParams<{ slug: string }>();
  const [search] = useSearchParams();
  const [slug, setSlug] = useState(slugParam || search.get('slug') || '');
  const [meta, setMeta] = useState<MetaResp | null>(null);
  const [prefillValues, setPrefillValues] = useState<Record<string, string>>({});
  const [flatten, setFlatten] = useState(true);
  const [toOverride, setToOverride] = useState('');
  const [ccOverride, setCcOverride] = useState('');
  const [bccOverride, setBccOverride] = useState('');
  const [pdfData, setPdfData] = useState<ArrayBuffer | null>(null);
  const [pdfLoading, setPdfLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const viewerRef = useRef<PdfFormViewerHandle>(null);
  const customerOverrides = useMemo(() => {
    const payload = customerToPrefillPayload(readCustomerDraft());
    return Object.keys(payload).length ? payload : undefined;
  }, []);

  async function load() {
    if (!slug) return;
    setLoading(true); setError('');
    try {
      const [metaRes, prefillRes] = await Promise.all([
        api.get<MetaResp>(`/forms/${encodeURIComponent(slug)}`),
        api.post<Record<string, string>>(
          `/forms/${encodeURIComponent(slug)}/prefill`,
          customerOverrides ? { customer: customerOverrides } : {}
        )
      ]);
      setMeta(metaRes.data);
      setPrefillValues(prefillRes.data);
      await generatePrefilledPdf(metaRes.data.slug, prefillRes.data);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally { setLoading(false); }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [slug]);

  async function generatePrefilledPdf(targetSlug?: string, valuesToUse?: Record<string, string>) {
    const activeSlug = targetSlug || slug;
    if (!activeSlug) return;
    setPdfLoading(true);
    setError('');
    try {
      const payloadValues = valuesToUse || prefillValues;
      const res = await api.post(
        `/forms/${encodeURIComponent(activeSlug)}/preview`,
        { values: payloadValues, customer: customerOverrides },
        { responseType: 'arraybuffer' }
      );
      setPdfData(res.data as ArrayBuffer);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally {
      setPdfLoading(false);
    }
  }

  async function submit() {
    if (!slug) return;
    if (!viewerRef.current) {
      setError('PDF is still loading. Please wait a moment and try again.');
      return;
    }
    setLoading(true); setError('');
    try {
      const bytes = await viewerRef.current.save();
      const blob = new Blob([bytes], { type: 'application/pdf' });
      const formData = new FormData();
      formData.append('pdf', blob, `${slug}-filled.pdf`);
      formData.append('flatten', String(flatten));
      if (toOverride) formData.append('toOverride', toOverride);
      if (ccOverride) formData.append('ccOverride', ccOverride);
      if (bccOverride) formData.append('bccOverride', bccOverride);
      if (customerOverrides) formData.append('customer', JSON.stringify(customerOverrides));

      const res = await api.post(`/forms/${encodeURIComponent(slug)}/submit-pdf`, formData);
      alert(`Submitted. Email Message Id: ${res.data?.emailMessageId || 'n/a'}`);
    } catch (e: any) { setError(e.response?.data?.message || e.message || String(e)); } finally { setLoading(false); }
  }

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <h2>Fill Form</h2>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input placeholder="Enter slug…" value={slug} onChange={(e) => setSlug(e.target.value)} style={{ minWidth: 240 }} />
        <button onClick={load} disabled={!slug || loading}>Load</button>
        <span style={{ marginLeft: 'auto' }} />
        <label><input type="checkbox" checked={flatten} onChange={(e) => setFlatten(e.target.checked)} /> Flatten</label>
      </div>

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {loading && <p>Loading…</p>}

      {meta && (
        <div style={{ marginTop: 16 }}>
          <h3>{meta.title}</h3>
          <p>Customer details and saved defaults have been merged below. Use the PDF viewer to complete the form directly.</p>

          <div className="pdf-panel">
            {pdfLoading && <p>Preparing PDF…</p>}
            {!pdfLoading && pdfData && (
              <div className="pdf-viewer-shell">
                <PdfFormViewer ref={viewerRef} data={pdfData} />
              </div>
            )}
            {!pdfLoading && !pdfData && <p>Load a form to view the PDF.</p>}
          </div>

          <div className="pdf-actions">
            <button onClick={() => generatePrefilledPdf(meta.slug)} disabled={pdfLoading || loading}>Regenerate Prefilled PDF</button>
            <div className="pdf-email-controls">
              <div>
                <label style={{ display: 'block' }}>To Override</label>
                <input value={toOverride} onChange={(e) => setToOverride(e.target.value)} style={{ width: '100%' }} />
              </div>
              <div>
                <label style={{ display: 'block' }}>Cc Override</label>
                <input value={ccOverride} onChange={(e) => setCcOverride(e.target.value)} style={{ width: '100%' }} />
              </div>
              <div>
                <label style={{ display: 'block' }}>Bcc Override</label>
                <input value={bccOverride} onChange={(e) => setBccOverride(e.target.value)} style={{ width: '100%' }} />
              </div>
            </div>
            <button onClick={submit} disabled={loading || pdfLoading || !pdfData}>Submit & Email</button>
          </div>
        </div>
      )}
    </div>
  );
}

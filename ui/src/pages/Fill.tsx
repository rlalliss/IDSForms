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
  const [printing, setPrinting] = useState(false);
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
      const blob = toPdfBlob(bytes);
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

  async function printFilledPdf() {
    if (!slug) return;
    if (!viewerRef.current) {
      setError('PDF is still loading. Please wait a moment and try again.');
      return;
    }
    setPrinting(true);
    setError('');
    try {
      const bytes = await viewerRef.current.save();
      const blob = toPdfBlob(bytes);
      const url = URL.createObjectURL(blob);

      const frame = document.createElement('iframe');
      frame.style.position = 'fixed';
      frame.style.top = '0';
      frame.style.left = '0';
      frame.style.width = '0';
      frame.style.height = '0';
      frame.style.border = '0';
      frame.src = url;
      frame.onload = () => {
        frame.contentWindow?.focus();
        frame.contentWindow?.print();
        setTimeout(() => {
          URL.revokeObjectURL(url);
          frame.remove();
        }, 800);
      };
      document.body.appendChild(frame);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally {
      setPrinting(false);
    }
  }

  function toPdfBlob(bytes: Uint8Array | ArrayBuffer) {
    const buffer = bytes instanceof ArrayBuffer
      ? bytes.slice(0)
      : bytes.slice().buffer; // slice() ensures we end up with a plain ArrayBuffer (not SharedArrayBuffer)
    return new Blob([buffer], { type: 'application/pdf' });
  }

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <div style={{ display: 'none' }}>
        <h2>Fill Form</h2>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <input placeholder="Enter slug…" value={slug} onChange={(e) => setSlug(e.target.value)} style={{ minWidth: 240 }} />
          <button onClick={load} disabled={!slug || loading}>Load</button>
          <span style={{ marginLeft: 'auto' }} />
          <label><input type="checkbox" checked={flatten} onChange={(e) => setFlatten(e.target.checked)} /> Flatten</label>
        </div>
      </div>

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {loading && <p>Loading…</p>}

      {meta && (
        <div style={{ marginTop: 16 }}>
          <div style={{ display: 'none' }}>
            <h3>{meta.title}</h3>
            <p>Customer details and saved defaults have been merged below. Use the PDF viewer to complete the form directly.</p>
          </div>

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
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <button onClick={printFilledPdf} disabled={printing || pdfLoading || loading || !pdfData}>
                <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                  <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M6 9V4h12v5" />
                    <path d="M6 17h12v3H6z" />
                    <rect x="4" y="9" width="16" height="8" rx="2" ry="2" />
                    <path d="M8 13h8" />
                  </svg>
                <span>Print</span>
                </span>
              </button>
              <button onClick={submit} disabled={loading || pdfLoading || !pdfData}>
                <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                  <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <rect x="3" y="6" width="18" height="12" rx="2" ry="2" />
                    <path d="m4 7 8 6 8-6" />
                    <path d="m4 18 6-4" />
                    <path d="m20 18-6-4" />
                  </svg>
                  <span>Email</span>
                </span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

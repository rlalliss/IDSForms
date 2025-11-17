import React, { useEffect, useMemo, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api';

type FieldMeta = { pdfFieldName: string; label: string; type: string; required: boolean; orderIndex: number };
type MetaResp = { slug: string; title: string; pdfBlobPath: string; fields: FieldMeta[] };

export default function Fill() {
  const { slug: slugParam } = useParams<{ slug: string }>();
  const [search] = useSearchParams();
  const [slug, setSlug] = useState(slugParam || search.get('slug') || '');
  const [meta, setMeta] = useState<MetaResp | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [flatten, setFlatten] = useState(true);
  const [toOverride, setToOverride] = useState('');
  const [ccOverride, setCcOverride] = useState('');
  const [bccOverride, setBccOverride] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function load() {
    if (!slug) return;
    setLoading(true); setError('');
    try {
      const [metaRes, prefillRes] = await Promise.all([
        api.get<MetaResp>(`/forms/${encodeURIComponent(slug)}`),
        api.get<Record<string, string>>(`/forms/${encodeURIComponent(slug)}/prefill`)
      ]);
      setMeta(metaRes.data);
      setValues(prefillRes.data);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally { setLoading(false); }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [slug]);

  function setValue(name: string, val: string) { setValues(v => ({ ...v, [name]: val })); }

  async function preview() {
    if (!slug) return;
    setLoading(true); setError('');
    try {
      const res = await api.post(`/forms/${encodeURIComponent(slug)}/preview`, { values }, { responseType: 'blob' });
      const blob = res.data as Blob;
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e: any) { setError(e.response?.data?.message || e.message || String(e)); } finally { setLoading(false); }
  }

  async function submit() {
    if (!slug) return;
    setLoading(true); setError('');
    try {
      const res = await api.post(`/forms/${encodeURIComponent(slug)}/submit`, {
        values,
        flatten,
        toOverride,
        ccOverride,
        bccOverride
      });
      alert(`Submitted. Email Message Id: ${res.data?.emailMessageId || 'n/a'}`);
    } catch (e: any) { setError(e.response?.data?.message || e.message || String(e)); } finally { setLoading(false); }
  }

  // Basic required detection
  const requiredMissing = useMemo(() => {
    if (!meta) return [] as string[];
    const misses: string[] = [];
    for (const f of (meta.fields || [])) {
      if (f.required) {
        const v = values[f.pdfFieldName];
        if (!v || !String(v).trim()) misses.push(f.label || f.pdfFieldName);
      }
    }
    return misses;
  }, [meta, values]);

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
          <div style={{ display: 'grid', gap: 12 }}>
            {(meta.fields || []).map(f => (
              <div key={f.pdfFieldName}>
                <label style={{ display: 'block', fontWeight: 600 }}>
                  {f.label} {f.required ? <span style={{ color: 'crimson' }}>*</span> : null}
                </label>
                {f.type === 'textarea' ? (
                  <textarea value={values[f.pdfFieldName] || ''} onChange={(e) => setValue(f.pdfFieldName, e.target.value)} rows={3} style={{ width: '100%' }} />
                ) : (
                  <input value={values[f.pdfFieldName] || ''} onChange={(e) => setValue(f.pdfFieldName, e.target.value)} style={{ width: '100%' }} />
                )}
              </div>
            ))}
          </div>

          <div style={{ marginTop: 20, display: 'grid', gap: 8 }}>
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

          {requiredMissing.length > 0 && (
            <p style={{ color: 'crimson', marginTop: 8 }}>Missing required: {requiredMissing.join(', ')}</p>
          )}

          <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
            <button onClick={preview} disabled={loading}>Preview PDF</button>
            <button onClick={submit} disabled={loading || requiredMissing.length > 0}>Submit & Email</button>
          </div>
        </div>
      )}
    </div>
  );
}

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api';

type StatusItem = {
  id: string;
  name: string;
  pdfFieldName: string;
  signerRole: string;
  orderIndex: number;
  required: boolean;
  signedAt?: string | null;
  signerName?: string | null;
  signerEmail?: string | null;
};

type StatusResponse = { items: StatusItem[] };

export default function Signatures() {
  const { slug: slugParam } = useParams<{ slug: string }>();
  const [search] = useSearchParams();
  const [slug, setSlug] = useState(slugParam || search.get('slug') || '');
  const [submissionId, setSubmissionId] = useState<string>(search.get('submissionId') || '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>('');
  const [status, setStatus] = useState<StatusResponse | null>(null);

  // Canvas state for signature drawing
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawing = useRef(false);
  const [canSubmit, setCanSubmit] = useState(false);

  const nextUnsigned = useMemo(() => {
    if (!status) return undefined;
    return status.items.find((i) => !i.signedAt && i.required);
  }, [status]);

  useEffect(() => {
    if (slug && submissionId) {
      refreshStatus();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slug, submissionId]);

  async function refreshStatus() {
    if (!slug || !submissionId) return;
    setLoading(true);
    setError('');
    try {
      const res = await api.get<StatusResponse>(`/forms/${encodeURIComponent(slug)}/signing/${submissionId}/status`);
      setStatus(res.data);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally {
      setLoading(false);
    }
  }

  async function startSession() {
    if (!slug) {
      setError('Please enter a form slug.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const res = await api.post(`/forms/${encodeURIComponent(slug)}/signing/start`, {
        values: {},
        flatten: true,
      });
      setSubmissionId(res.data.submissionId);
      setStatus(res.data.status as StatusResponse);
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally {
      setLoading(false);
    }
  }

  // Signature drawing handlers
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.strokeStyle = '#000';

    function resize() {
      const rect = canvas.getBoundingClientRect();
      const data = ctx.getImageData(0, 0, canvas.width, canvas.height);
      canvas.width = Math.max(600, Math.floor(rect.width));
      canvas.height = 200;
      ctx.putImageData(data, 0, 0);
    }

    resize();
    window.addEventListener('resize', resize);
    return () => window.removeEventListener('resize', resize);
  }, []);

  function getPos(e: MouseEvent | TouchEvent, canvas: HTMLCanvasElement) {
    const rect = canvas.getBoundingClientRect();
    let x = 0, y = 0;
    if (e instanceof MouseEvent) {
      x = e.clientX - rect.left; y = e.clientY - rect.top;
    } else if ((e as TouchEvent).touches && (e as TouchEvent).touches[0]) {
      x = (e as TouchEvent).touches[0].clientX - rect.left; y = (e as TouchEvent).touches[0].clientY - rect.top;
    }
    return { x, y };
  }

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const onDown = (e: any) => { drawing.current = true; const p = getPos(e, canvas); ctx.beginPath(); ctx.moveTo(p.x, p.y); setCanSubmit(true); };
    const onMove = (e: any) => { if (!drawing.current) return; const p = getPos(e, canvas); ctx.lineTo(p.x, p.y); ctx.stroke(); };
    const onUp = () => { drawing.current = false; };

    canvas.addEventListener('mousedown', onDown as any);
    canvas.addEventListener('mousemove', onMove as any);
    window.addEventListener('mouseup', onUp as any);
    canvas.addEventListener('touchstart', onDown as any, { passive: true } as any);
    canvas.addEventListener('touchmove', onMove as any, { passive: true } as any);
    window.addEventListener('touchend', onUp as any);
    return () => {
      canvas.removeEventListener('mousedown', onDown as any);
      canvas.removeEventListener('mousemove', onMove as any);
      window.removeEventListener('mouseup', onUp as any);
      canvas.removeEventListener('touchstart', onDown as any);
      canvas.removeEventListener('touchmove', onMove as any);
      window.removeEventListener('touchend', onUp as any);
    };
  }, []);

  function clearCanvas() {
    const canvas = canvasRef.current; const ctx = canvas?.getContext('2d');
    if (canvas && ctx) { ctx.clearRect(0, 0, canvas.width, canvas.height); setCanSubmit(false); }
  }

  async function submitSignature() {
    if (!slug || !submissionId || !nextUnsigned) return;
    const canvas = canvasRef.current; if (!canvas) return;
    const dataUrl = canvas.toDataURL('image/png');
    setLoading(true);
    setError('');
    try {
      await api.post(`/forms/${encodeURIComponent(slug)}/signing/${submissionId}/capture`, {
        signatureRequirementId: nextUnsigned.id,
        dataUrl,
      });
      clearCanvas();
      await refreshStatus();
    } catch (e: any) {
      setError(e.response?.data?.message || e.message || String(e));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <h2>Form Signatures</h2>

      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
        <label>
          Slug:
          <input value={slug} onChange={(e) => setSlug(e.target.value)} placeholder="sample-form" style={{ marginLeft: 8 }} />
        </label>
        <label>
          Submission Id:
          <input value={submissionId} onChange={(e) => setSubmissionId(e.target.value)} placeholder="(empty to start new)" style={{ marginLeft: 8 }} />
        </label>
        <button onClick={startSession} disabled={!slug || loading}>Start Session</button>
        <button onClick={refreshStatus} disabled={!slug || !submissionId || loading}>Refresh Status</button>
      </div>

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {loading && <p>Loading…</p>}

      {status && (
        <div style={{ marginTop: 16 }}>
          <h3>Required Signatures</h3>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>Name</th>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>Field</th>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>Role</th>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>Required</th>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>Signed</th>
                <th style={{ textAlign: 'left', borderBottom: '1px solid #ddd' }}>By</th>
              </tr>
            </thead>
            <tbody>
              {status.items.map((i) => (
                <tr key={i.id}>
                  <td style={{ padding: '4px 0' }}>{i.name}</td>
                  <td>{i.pdfFieldName}</td>
                  <td>{i.signerRole}</td>
                  <td>{i.required ? 'Yes' : 'No'}</td>
                  <td>{i.signedAt ? new Date(i.signedAt).toLocaleString() : '-'}</td>
                  <td>{i.signerName || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {nextUnsigned ? (
            <div style={{ marginTop: 24 }}>
              <h3>Capture Signature: {nextUnsigned.name}</h3>
              <div style={{ border: '1px solid #ccc', borderRadius: 4, padding: 8 }}>
                <canvas ref={canvasRef} style={{ width: '100%', height: 200, display: 'block' }} />
              </div>
              <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                <button onClick={clearCanvas}>Clear</button>
                <button onClick={submitSignature} disabled={!canSubmit || loading}>Submit Signature</button>
              </div>
              <p style={{ color: '#666' }}>Use mouse or touch to sign in the box above.</p>
            </div>
          ) : (
            <p style={{ marginTop: 24 }}>All required signatures are complete.</p>
          )}
        </div>
      )}
    </div>
  );
}

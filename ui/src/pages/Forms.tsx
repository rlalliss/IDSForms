import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import AppShell from '../components/AppShell';
import { readCustomerDraft, writeCustomerDraft } from '../customerStorage';
import type { CustomerDraft } from '../customerStorage';

const apiBase = api.defaults.baseURL?.replace(/\/$/, '') ?? '';

type FormItem = { slug: string; title: string; category?: string | null; description?: string | null };

type SignaturePadProps = {
  value: string;
  onChange: (dataUrl: string) => void;
};

function SignaturePad({ value, onChange }: SignaturePadProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const ctxRef = useRef<CanvasRenderingContext2D | null>(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);

  const setupCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.scale(dpr, dpr);
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    ctx.lineWidth = 2;
    ctx.strokeStyle = '#0f172a';
    ctxRef.current = ctx;
    ctx.clearRect(0, 0, rect.width, rect.height);
    if (value) {
      const img = new Image();
      img.onload = () => {
        ctx.clearRect(0, 0, rect.width, rect.height);
        ctx.drawImage(img, 0, 0, rect.width, rect.height);
      };
      img.src = value;
    }
  }, [value]);

  useEffect(() => {
    setupCanvas();
  }, [setupCanvas]);

  const getPoint = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return null;
    const rect = canvas.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top
    };
  };

  function handlePointerDown(event: React.PointerEvent<HTMLCanvasElement>) {
    event.preventDefault();
    const point = getPoint(event);
    if (!point) return;
    drawingRef.current = true;
    lastPointRef.current = point;
  }

  function handlePointerMove(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current) return;
    event.preventDefault();
    const point = getPoint(event);
    const ctx = ctxRef.current;
    const last = lastPointRef.current;
    if (!point || !ctx || !last) return;
    ctx.beginPath();
    ctx.moveTo(last.x, last.y);
    ctx.lineTo(point.x, point.y);
    ctx.stroke();
    lastPointRef.current = point;
  }

  function finishStroke() {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    lastPointRef.current = null;
    const canvas = canvasRef.current;
    if (!canvas) return;
    onChange(canvas.toDataURL('image/png'));
  }

  return (
    <canvas
      ref={canvasRef}
      className="signature-pad__canvas"
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={finishStroke}
      onPointerLeave={finishStroke}
      onPointerCancel={finishStroke}
      style={{ touchAction: 'none' }}
    />
  );
}

export default function Forms() {
  const nav = useNavigate();
  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [items, setItems] = useState<FormItem[]>([]);
  const [slug, setSlug] = useState('');
  const [customer, setCustomer] = useState<CustomerDraft>(() => readCustomerDraft());
  const [saveMessage, setSaveMessage] = useState('');
  const [selectedForms, setSelectedForms] = useState<FormItem[]>([]);
  const selectedSlugs = useMemo(() => new Set(selectedForms.map((f) => f.slug)), [selectedForms]);

  function handleCustomerChange(field: keyof CustomerDraft, value: string) {
    setCustomer((prev) => ({ ...prev, [field]: value }));
  }

  function handleCustomerSave(e: React.FormEvent) {
    e.preventDefault();
    try {
      writeCustomerDraft(customer);
      setSaveMessage('Customer details saved for this session.');
      setTimeout(() => setSaveMessage(''), 3500);
    } catch {
      setSaveMessage('');
    }
  }

  async function search() {
    setLoading(true);
    setError('');
    try {
      const res = await api.get('/forms', { params: q ? { q } : undefined });
      setItems(res.data as FormItem[]);
    } catch (e: any) {
      setError(e.message || String(e));
      // eslint-disable-next-line no-console
      console.error('Forms search failed', e);
    } finally {
      setLoading(false);
    }
  }

  function handleSignatureChange(dataUrl: string) {
    setCustomer((prev) => {
      const next = { ...prev, CustomerSignatureDataUrl: dataUrl };
      try {
        writeCustomerDraft(next);
        setSaveMessage(dataUrl ? 'Signature saved for this session.' : 'Signature cleared.');
        setTimeout(() => setSaveMessage(''), 3500);
      } catch {
        setSaveMessage('');
      }
      return next;
    });
  }

  function handleAddToList(form: FormItem) {
    setSelectedForms((prev) => (prev.some((item) => item.slug === form.slug) ? prev : [...prev, form]));
  }

  function handleRemoveFromList(slugValue: string) {
    setSelectedForms((prev) => prev.filter((item) => item.slug !== slugValue));
  }

  function handleFillSelected() {
    if (selectedForms.length === 0) {
      return;
    }
    selectedForms.forEach((form) => {
      const fillPath = `/fill/${encodeURIComponent(form.slug)}`;
      if (typeof window !== 'undefined') {
        window.open(fillPath, '_blank', 'noopener,noreferrer');
      }
    });
  }

  useEffect(() => {
    search();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const heroContent = useMemo(() => (
    <div className="forms-hero forms-hero--single">
      <form className="forms-hero__form" onSubmit={handleCustomerSave}>
        <fieldset className="forms-hero__fieldset">
          <legend>Customer details</legend>
        <div className="forms-hero__columns">
          <div className="forms-hero__column">
            <div className="form-field">
              <label htmlFor="cust-name">Customer name</label>
              <input
                id="cust-name"
                className="input-control"
                value={customer.CustomerName}
                onChange={(e) => handleCustomerChange('CustomerName', e.target.value)}
                placeholder="Ava Thompson"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-email">Email</label>
              <input
                id="cust-email"
                className="input-control"
                type="email"
                value={customer.CustomerEmail}
                onChange={(e) => handleCustomerChange('CustomerEmail', e.target.value)}
                placeholder="ava@example.com"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-home">Home phone</label>
              <input
                id="cust-home"
                className="input-control"
                type="tel"
                value={customer.CustomerHomePhone}
                onChange={(e) => handleCustomerChange('CustomerHomePhone', e.target.value)}
                placeholder="555-123-9876"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-work">Work phone</label>
              <input
                id="cust-work"
                className="input-control"
                type="tel"
                value={customer.CustomerWorkPhone}
                onChange={(e) => handleCustomerChange('CustomerWorkPhone', e.target.value)}
                placeholder="555-987-6543"
              />
            </div>
          </div>
          <div className="forms-hero__column">
            <div className="form-field">
              <label htmlFor="cust-street">Street address</label>
              <input
                id="cust-street"
                className="input-control"
                value={customer.CustomerStreet}
                onChange={(e) => handleCustomerChange('CustomerStreet', e.target.value)}
                placeholder="123 Main St"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-city">City</label>
              <input
                id="cust-city"
                className="input-control"
                value={customer.CustomerCity}
                onChange={(e) => handleCustomerChange('CustomerCity', e.target.value)}
                placeholder="Anytown"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-state">State</label>
              <input
                id="cust-state"
                className="input-control"
                value={customer.CustomerState}
                onChange={(e) => handleCustomerChange('CustomerState', e.target.value)}
                placeholder="CA"
              />
            </div>
            <div className="form-field">
              <label htmlFor="cust-zip">ZIP</label>
              <input
                id="cust-zip"
                className="input-control"
                value={customer.CustomerZip}
                onChange={(e) => handleCustomerChange('CustomerZip', e.target.value)}
                placeholder="90001"
              />
            </div>
          </div>
        </div>
        </fieldset>
        <div className="signature-card">
          <div className="signature-card__header">
            <label>Customer signature</label>
            <p className="signature-card__hint">Draw once and reuse across every PDF you fill from this page.</p>
          </div>
          <SignaturePad value={customer.CustomerSignatureDataUrl} onChange={handleSignatureChange} />
          <div className="signature-card__footer">
            <button
              type="button"
              className="btn btn--ghost"
              onClick={() => handleSignatureChange('')}
              disabled={!customer.CustomerSignatureDataUrl}
            >
              Clear signature
            </button>
            <span className="signature-card__status">
              {customer.CustomerSignatureDataUrl ? 'Signature saved for this session.' : 'Use your mouse or finger to sign above.'}
            </span>
          </div>
        </div>
        <button type="submit" className="btn btn--primary btn--full">
          Save quick details
        </button>
        {saveMessage && <p className="forms-hero__status">{saveMessage}</p>}
      </form>
    </div>
  ), [customer, saveMessage]);

  return (
    <AppShell
      title="Forms catalog"
      subtitle="Browse, fill, and send the latest templates."
      heroContent={heroContent}
      actions={
        <button className="btn btn--primary" onClick={() => nav('/fill')}>
          Start submission
        </button>
      }
    >
      <section className="panel">
        <div className="forms-controls">
          <div className="form-field">
            <label>Search</label>
            <input
              className="input-control"
              placeholder="Search forms…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && search()}
            />
          </div>
          <button className="btn btn--primary" onClick={search} disabled={loading}>
            {loading ? 'Searching…' : 'Search'}
          </button>
          <div className="form-field">
            <label>Jump to sign</label>
            <input
              className="input-control"
              placeholder="Enter slug to sign…"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
            />
          </div>
          <button className="btn btn--outline" onClick={() => slug && nav(`/sign/${encodeURIComponent(slug)}`)} disabled={!slug}>
            Go sign
          </button>
        </div>
        {error && <p className="error-text">{error}</p>}
      </section>

      <section className="panel" style={{ padding: 0 }}>
        {items.length === 0 && !loading ? (
          <div className="empty-state">No forms found.</div>
        ) : (
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Description</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((f) => {
                  const alreadySelected = selectedSlugs.has(f.slug);
                  return (
                    <tr key={f.slug}>
                      <td>
                        <div className="form-description">
                          <p className="form-description__title">{f.title}</p>
                          <div className="form-description__meta">
                            <span>{f.category || 'Uncategorized'}</span>
                            <span>{f.slug}</span>
                          </div>
                          <p className="form-description__text">{f.description || 'No description provided.'}</p>
                        </div>
                      </td>
                      <td>
                        <div className="form-actions">
                          <button
                            onClick={() => handleAddToList(f)}
                            className="link-action"
                            disabled={alreadySelected}
                          >
                            {alreadySelected ? 'Added' : 'Add to List'}
                          </button>
                          <a
                            className="link-action"
                            href={`${apiBase}/forms/${encodeURIComponent(f.slug)}/pdf`}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Preview
                          </a>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel" style={{ padding: 0 }}>
        <div className="forms-list-panel__header">
          <div>
            <h3>Forms to fill</h3>
            <p>Add templates above, then launch fill for every selection.</p>
          </div>
          <button className="btn btn--primary" onClick={handleFillSelected} disabled={selectedForms.length === 0}>
            {selectedForms.length > 0
              ? `Fill ${selectedForms.length} ${selectedForms.length === 1 ? 'form' : 'forms'}`
              : 'Fill list'}
          </button>
        </div>
        {selectedForms.length === 0 ? (
          <div className="empty-state">No forms have been added to the list yet.</div>
        ) : (
          <div className="table-wrapper">
            <table className="table">
              <thead>
                <tr>
                  <th>Description</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {selectedForms.map((f) => (
                  <tr key={`selected-${f.slug}`}>
                    <td>
                      <div className="form-description">
                        <p className="form-description__title">{f.title}</p>
                        <div className="form-description__meta">
                          <span>{f.category || 'Uncategorized'}</span>
                          <span>{f.slug}</span>
                        </div>
                        <p className="form-description__text">{f.description || 'No description provided.'}</p>
                      </div>
                    </td>
                    <td>
                      <div className="form-actions">
                        <button onClick={() => nav(`/fill/${encodeURIComponent(f.slug)}`)} className="link-action">
                          Fill
                        </button>
                        <button onClick={() => handleRemoveFromList(f.slug)} className="link-action">
                          Remove
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}

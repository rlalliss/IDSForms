import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import AppShell from '../components/AppShell';
import { readCustomerDraft, writeCustomerDraft } from '../customerStorage';
import type { CustomerDraft } from '../customerStorage';

const apiBase = api.defaults.baseURL?.replace(/\/$/, '') ?? '';

type FormItem = { slug: string; title: string; category?: string | null; description?: string | null };

export default function Forms() {
  const nav = useNavigate();
  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [items, setItems] = useState<FormItem[]>([]);
  const [slug, setSlug] = useState('');
  const [customer, setCustomer] = useState<CustomerDraft>(() => readCustomerDraft());
  const [saveMessage, setSaveMessage] = useState('');

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
                  <th>Title</th>
                  <th>Slug</th>
                  <th>Category</th>
                  <th>Description</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((f) => (
                  <tr key={f.slug}>
                    <td style={{ fontWeight: 600 }}>{f.title}</td>
                    <td>{f.slug}</td>
                    <td>{f.category || '-'}</td>
                    <td>{f.description || '-'}</td>
                    <td>
                      <div className="form-actions">
                        <button onClick={() => nav(`/fill/${encodeURIComponent(f.slug)}`)} className="link-action">
                          Fill
                        </button>
                        <button onClick={() => nav(`/sign/${encodeURIComponent(f.slug)}`)} className="link-action">
                          Sign
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
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}

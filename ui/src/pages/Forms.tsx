import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';
import AppShell from '../components/AppShell';

const apiBase = api.defaults.baseURL?.replace(/\/$/, '') ?? '';

type FormItem = { slug: string; title: string; category?: string | null; description?: string | null };

export default function Forms() {
  const nav = useNavigate();
  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [items, setItems] = useState<FormItem[]>([]);
  const [slug, setSlug] = useState('');

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

  return (
    <AppShell
      title="Forms catalog"
      subtitle="Browse, fill, and send the latest templates."
      breadcrumbs={[{ label: 'Workspace' }, { label: 'Forms' }]}
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

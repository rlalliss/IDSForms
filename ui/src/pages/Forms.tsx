import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api';

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
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 16 }}>
      <h2>Forms</h2>

      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          placeholder="Search forms…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && search()}
          style={{ minWidth: 250 }}
        />
        <button onClick={search} disabled={loading}>Search</button>
        <span style={{ marginLeft: 'auto' }} />
        <input
          placeholder="Enter slug to sign…"
          value={slug}
          onChange={(e) => setSlug(e.target.value)}
          style={{ minWidth: 220 }}
        />
        <button onClick={() => slug && nav(`/sign/${encodeURIComponent(slug)}`)} disabled={!slug}>Go Sign</button>
      </div>

      {error && <p style={{ color: 'crimson' }}>{error}</p>}
      {loading && <p>Loading…</p>}

      <div style={{ marginTop: 16 }}>
        {items.length === 0 && !loading ? (
          <p>No forms found.</p>
        ) : (
          <div className="overflow-hidden rounded-xl border">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-2 text-left font-medium text-gray-600">Title</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-600">Slug</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-600">Category</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-600">Description</th>
                  <th className="px-4 py-2 text-left font-medium text-gray-600">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {items.map((f) => (
                  <tr key={f.slug} className="hover:bg-gray-50">
                    <td className="px-4 py-2">{f.title}</td>
                    <td className="px-4 py-2 text-gray-600">{f.slug}</td>
                    <td className="px-4 py-2 text-gray-600">{f.category || '-'}</td>
                    <td className="px-4 py-2 text-gray-600">{f.description || '-'}</td>
                    <td className="px-4 py-2 space-x-3">
                      <button className="text-blue-600 hover:underline" onClick={() => nav(`/fill/${encodeURIComponent(f.slug)}`)}>
                        Fill
                      </button>
                      <button className="text-blue-600 hover:underline" onClick={() => nav(`/sign/${encodeURIComponent(f.slug)}`)}>
                        Sign
                      </button>
                      <a
                        className="text-blue-600 hover:underline"
                        href={`/api/forms/${encodeURIComponent(f.slug)}/pdf`}
                        target="_blank" rel="noreferrer"
                      >
                        Preview
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../api/client';
import ProtectedLayout from '../components/ProtectedLayout';
import type { ReceiptSummary } from '../types';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString();
}

function formatCurrency(amount: number): string {
  return `$${amount.toFixed(2)}`;
}

export default function ReceiptList() {
  const [receipts, setReceipts] = useState<ReceiptSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiClient
      .get<ReceiptSummary[]>('/receipts')
      .then((res) => setReceipts(res.data))
      .catch(() => setError('Failed to load receipts.'))
      .finally(() => setLoading(false));
  }, []);

  return (
    <ProtectedLayout>
      <main className="page">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h1 className="page-title" style={{ marginBottom: 0 }}>
            Receipts
          </h1>
          <Link to="/receipts/upload" className="btn">
            Upload Receipt
          </Link>
        </div>

        <div className="card" style={{ marginTop: '1.5rem' }}>
          {loading && <p className="loading">Loading…</p>}
          {error && <p className="error-msg">{error}</p>}

          {!loading && !error && receipts.length === 0 && (
            <p className="empty-state">No receipts yet. Upload your first receipt!</p>
          )}

          {!loading && !error && receipts.length > 0 && (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>Store</th>
                    <th>Date</th>
                    <th>Total</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {receipts.map((r) => (
                    <tr key={r.id}>
                      <td>
                        <Link to={`/receipts/${r.id}`}>{r.storeName || '—'}</Link>
                      </td>
                      <td>{formatDate(r.createdAt)}</td>
                      <td>{formatCurrency(r.totalAmount)}</td>
                      <td>
                        <span className={`badge badge-${r.status.toLowerCase()}`}>{r.status}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </main>
    </ProtectedLayout>
  );
}

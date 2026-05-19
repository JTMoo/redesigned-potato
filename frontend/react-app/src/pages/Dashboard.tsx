import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import apiClient from '../api/client';
import ProtectedLayout from '../components/ProtectedLayout';
import type { Notification, ReceiptSummary } from '../types';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString();
}

function formatCurrency(amount: number): string {
  return `$${amount.toFixed(2)}`;
}

export default function Dashboard() {
  const { user } = useAuth();
  const [receipts, setReceipts] = useState<ReceiptSummary[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [receiptsRes, notificationsRes] = await Promise.all([
          apiClient.get<ReceiptSummary[]>('/receipts'),
          apiClient.get<Notification[]>('/notifications'),
        ]);
        setReceipts(receiptsRes.data.slice(0, 5));
        setUnreadCount(notificationsRes.data.filter((n) => !n.isRead).length);
      } catch {
        setError('Failed to load dashboard data.');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const totalSavings = 0; // populated once matching data is integrated

  return (
    <ProtectedLayout>
      <main className="page">
        <h1 className="page-title">Welcome, {user?.name}</h1>

        <div className="summary-grid">
          <div className="summary-card">
            <div className="summary-card-value">{receipts.length}</div>
            <div className="summary-card-label">Recent Receipts</div>
          </div>
          <div className="summary-card">
            <div className="summary-card-value">{formatCurrency(totalSavings)}</div>
            <div className="summary-card-label">Total Savings Found</div>
          </div>
          <div className="summary-card">
            <div className="summary-card-value">{unreadCount}</div>
            <div className="summary-card-label">Unread Notifications</div>
          </div>
        </div>

        <div className="card">
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: '1rem',
            }}
          >
            <h2 className="section-title" style={{ marginBottom: 0 }}>
              Recent Receipts
            </h2>
            <Link to="/receipts/upload" className="btn" style={{ fontSize: '0.85rem', padding: '0.4rem 1rem' }}>
              Upload Receipt
            </Link>
          </div>

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

        <Link to="/receipts">View all receipts →</Link>
      </main>
    </ProtectedLayout>
  );
}

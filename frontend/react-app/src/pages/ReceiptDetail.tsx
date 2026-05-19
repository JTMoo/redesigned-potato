import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import apiClient from '../api/client';
import ProtectedLayout from '../components/ProtectedLayout';
import type { Receipt, DealMatch } from '../types';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString();
}

function formatCurrency(amount: number): string {
  return `$${amount.toFixed(2)}`;
}

export default function ReceiptDetail() {
  const { id } = useParams<{ id: string }>();

  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [matches, setMatches] = useState<DealMatch[]>([]);
  const [receiptLoading, setReceiptLoading] = useState(true);
  const [matchesLoading, setMatchesLoading] = useState(true);
  const [receiptError, setReceiptError] = useState<string | null>(null);
  const [matchesError, setMatchesError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;

    apiClient
      .get<Receipt>(`/receipts/${id}`)
      .then((res) => setReceipt(res.data))
      .catch(() => setReceiptError('Receipt not found or you do not have access.'))
      .finally(() => setReceiptLoading(false));

    apiClient
      .get<DealMatch[]>(`/matches/${id}`)
      .then((res) => setMatches(res.data))
      .catch(() => setMatchesError('Failed to load deal matches.'))
      .finally(() => setMatchesLoading(false));
  }, [id]);

  return (
    <ProtectedLayout>
      <main className="page">
        <div style={{ marginBottom: '1rem' }}>
          <Link to="/receipts">← Back to Receipts</Link>
        </div>

        <h1 className="page-title">Receipt Detail</h1>

        {receiptLoading && <p className="loading">Loading receipt…</p>}
        {receiptError && <p className="error-msg">{receiptError}</p>}

        {receipt && (
          <>
            <div className="card">
              <div className="detail-grid">
                <div className="detail-field">
                  <label>Store</label>
                  <p>{receipt.storeName || '—'}</p>
                </div>
                <div className="detail-field">
                  <label>Date</label>
                  <p>{formatDate(receipt.createdAt)}</p>
                </div>
                <div className="detail-field">
                  <label>Total Amount</label>
                  <p>{formatCurrency(receipt.totalAmount)}</p>
                </div>
                <div className="detail-field">
                  <label>Status</label>
                  <p>
                    <span className={`badge badge-${receipt.status.toLowerCase()}`}>{receipt.status}</span>
                  </p>
                </div>
              </div>

              <h3 className="section-title">Extracted Items</h3>
              {receipt.items.length === 0 ? (
                <p className="empty-state">No items extracted yet.</p>
              ) : (
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Description</th>
                        <th>Qty</th>
                        <th>Unit Price</th>
                        <th>Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      {receipt.items.map((item) => (
                        <tr key={item.id}>
                          <td>{item.description}</td>
                          <td>{item.quantity}</td>
                          <td>{formatCurrency(item.unitPrice)}</td>
                          <td>{formatCurrency(item.totalPrice)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="card">
              <h3 className="section-title">Matching Deals</h3>

              {matchesLoading && <p className="loading">Loading matches…</p>}
              {matchesError && <p className="error-msg">{matchesError}</p>}

              {!matchesLoading && !matchesError && matches.length === 0 && (
                <p className="empty-state">No deals found matching your items.</p>
              )}

              {!matchesLoading && !matchesError && matches.length > 0 && (
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Deal</th>
                        <th>Matched Item</th>
                        <th>Discount</th>
                      </tr>
                    </thead>
                    <tbody>
                      {matches.map((m) => (
                        <tr key={m.id}>
                          <td>{m.dealTitle}</td>
                          <td>{m.matchedItemDescription}</td>
                          <td>{formatCurrency(m.discountAmount)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </main>
    </ProtectedLayout>
  );
}

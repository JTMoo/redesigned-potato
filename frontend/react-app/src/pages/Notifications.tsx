import { useEffect, useState } from 'react';
import apiClient from '../api/client';
import ProtectedLayout from '../components/ProtectedLayout';
import type { Notification } from '../types';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString();
}

export default function Notifications() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiClient
      .get<Notification[]>('/notifications')
      .then((res) => setNotifications(res.data))
      .catch(() => setError('Failed to load notifications.'))
      .finally(() => setLoading(false));
  }, []);

  const handleMarkRead = async (notification: Notification) => {
    if (notification.isRead) return;

    try {
      await apiClient.patch(`/notifications/${notification.id}/read`);
      setNotifications((prev) =>
        prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)),
      );
    } catch {
      // silently ignore — UI will stay showing as unread
    }
  };

  return (
    <ProtectedLayout>
      <main className="page">
        <h1 className="page-title">Notifications</h1>

        <div className="card" style={{ padding: 0 }}>
          {loading && <p className="loading">Loading…</p>}
          {error && <p className="error-msg" style={{ margin: '1rem' }}>{error}</p>}

          {!loading && !error && notifications.length === 0 && (
            <p className="empty-state">No notifications yet.</p>
          )}

          {!loading &&
            !error &&
            notifications.map((n) => (
              <div
                key={n.id}
                className={`notification-item${n.isRead ? '' : ' unread'}`}
                onClick={() => handleMarkRead(n)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === 'Enter' && handleMarkRead(n)}
              >
                <span className={`notification-dot${n.isRead ? ' read' : ''}`} />
                <div className="notification-message">
                  <div>{n.message}</div>
                  <div className="notification-date">{formatDate(n.createdAt)}</div>
                </div>
                {!n.isRead && (
                  <span
                    style={{
                      fontSize: '0.78rem',
                      color: '#2563eb',
                      fontWeight: 600,
                      whiteSpace: 'nowrap',
                    }}
                  >
                    Mark read
                  </span>
                )}
              </div>
            ))}
        </div>
      </main>
    </ProtectedLayout>
  );
}

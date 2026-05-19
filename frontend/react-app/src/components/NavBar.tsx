import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import apiClient from '../api/client';
import type { Notification } from '../types';

export default function NavBar() {
  const { logout } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    apiClient
      .get<Notification[]>('/notifications')
      .then((res) => {
        const count = res.data.filter((n) => !n.isRead).length;
        setUnreadCount(count);
      })
      .catch(() => {
        // silently ignore — badge just won't show
      });
  }, []);

  return (
    <nav className="nav">
      <span className="nav-brand">Expense Tracker</span>
      <Link to="/dashboard" className="nav-link">
        Dashboard
      </Link>
      <Link to="/receipts" className="nav-link">
        Receipts
      </Link>
      <Link to="/receipts/upload" className="nav-link">
        Upload
      </Link>
      <Link to="/notifications" className="nav-link">
        Notifications
        {unreadCount > 0 && <span className="nav-badge">{unreadCount}</span>}
      </Link>
      <button type="button" className="nav-logout" onClick={logout}>
        Logout
      </button>
    </nav>
  );
}

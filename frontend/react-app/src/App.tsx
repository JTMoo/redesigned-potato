import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import LoginPage from './auth/LoginPage';
import OAuthCallback from './auth/OAuthCallback';
import Dashboard from './pages/Dashboard';
import ReceiptUpload from './pages/ReceiptUpload';
import ReceiptList from './pages/ReceiptList';
import ReceiptDetail from './pages/ReceiptDetail';
import Notifications from './pages/Notifications';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth();
  return token ? <>{children}</> : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/auth/callback" element={<OAuthCallback />} />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <Dashboard />
          </ProtectedRoute>
        }
      />
      <Route
        path="/receipts"
        element={
          <ProtectedRoute>
            <ReceiptList />
          </ProtectedRoute>
        }
      />
      <Route
        path="/receipts/upload"
        element={
          <ProtectedRoute>
            <ReceiptUpload />
          </ProtectedRoute>
        }
      />
      <Route
        path="/receipts/:id"
        element={
          <ProtectedRoute>
            <ReceiptDetail />
          </ProtectedRoute>
        }
      />
      <Route
        path="/notifications"
        element={
          <ProtectedRoute>
            <Notifications />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}

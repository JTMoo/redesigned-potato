import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export default function OAuthCallback() {
  const { login } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    // Token is passed as a URL fragment (#token) so it is never sent to any
    // server in HTTP requests and does not appear in access logs.
    const token = window.location.hash.slice(1);
    const decoded = token ? decodeURIComponent(token) : null;
    if (decoded) {
      login(decoded);
      navigate('/', { replace: true });
    } else {
      navigate('/login', { replace: true });
    }
  }, [login, navigate]);

  return <p>Signing you in…</p>;
}

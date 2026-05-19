import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiClient from '../api/client';
import ProtectedLayout from '../components/ProtectedLayout';
import type { Receipt } from '../types';

export default function ReceiptUpload() {
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [storeName, setStoreName] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!file) {
      setError('Please select an image file.');
      return;
    }

    const formData = new FormData();
    formData.append('file', file);
    if (storeName.trim()) {
      formData.append('storeName', storeName.trim());
    }

    setLoading(true);
    setError(null);

    try {
      const res = await apiClient.post<Receipt>('/receipts', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      navigate(`/receipts/${res.data.id}`);
    } catch {
      setError('Upload failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <ProtectedLayout>
      <main className="page">
        <h1 className="page-title">Upload Receipt</h1>

        <div className="card" style={{ maxWidth: 540 }}>
          {error && <p className="error-msg">{error}</p>}

          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="file">Receipt Image</label>
              <input
                id="file"
                type="file"
                accept="image/*"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="storeName">Store Name (optional)</label>
              <input
                id="storeName"
                type="text"
                value={storeName}
                onChange={(e) => setStoreName(e.target.value)}
                placeholder="e.g. Whole Foods"
              />
            </div>

            <button type="submit" className="btn" disabled={loading}>
              {loading ? 'Uploading…' : 'Upload Receipt'}
            </button>
          </form>
        </div>
      </main>
    </ProtectedLayout>
  );
}

export default function LoginPage() {
  const handleSignIn = () => {
    window.location.href = '/api/auth/google';
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', marginTop: '20vh' }}>
      <h1>Expense Tracker</h1>
      <button onClick={handleSignIn} type="button">
        Sign in with Google
      </button>
    </div>
  );
}

import { useAuth } from "../auth/AuthContext";

export default function Dashboard() {
  const { user, logout } = useAuth();

  return (
    <div style={{ padding: "2rem" }}>
      <h1>Dashboard</h1>
      <p>Welcome, {user?.name}</p>
      <p>{user?.email}</p>
      <button onClick={logout} type="button">
        Sign out
      </button>
    </div>
  );
}

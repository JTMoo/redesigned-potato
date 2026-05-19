import NavBar from './NavBar';

interface ProtectedLayoutProps {
  children: React.ReactNode;
}

export default function ProtectedLayout({ children }: ProtectedLayoutProps) {
  return (
    <>
      <NavBar />
      {children}
    </>
  );
}

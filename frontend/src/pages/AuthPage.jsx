import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { errorMessage, registerAccount } from '../services/api';

export default function AuthPage({ register = false }) {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ nome: '', email: '', password: '', confirmPassword: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [created, setCreated] = useState(false);
  async function submit(event) {
    event.preventDefault();
    if (busy) return;
    setError('');
    if (register && form.password !== form.confirmPassword) { setError('As senhas devem ser iguais.'); return; }
    setBusy(true);
    try {
      if (register) { await registerAccount(form); setCreated(true); setForm({ nome: '', email: '', password: '', confirmPassword: '' }); }
      else { await login({ email: form.email, password: form.password }); navigate('/minhas-analises'); }
    } catch (failure) { setError(errorMessage(failure)); }
    finally { setBusy(false); }
  }
  if (created) return <div className="alert alert-success">Conta criada. <Link to="/login">Entrar na plataforma</Link>.</div>;
  return <section className="search-panel mx-auto">
    <h1 className="h2">{register ? 'Criar conta' : 'Entrar'}</h1>
    <p className="text-body-secondary">Sua conta da plataforma é independente do GitHub. Use-a para guardar perfis públicos na sua lista.</p>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    <form onSubmit={submit}>
      {(register ? [['nome', 'Nome', 'text'], ['email', 'E-mail', 'email'], ['password', 'Senha', 'password'], ['confirmPassword', 'Confirmar senha', 'password']]
        : [['email', 'E-mail', 'email'], ['password', 'Senha', 'password']]).map(([key, label, type]) => <div className="mb-3" key={key}>
        <label htmlFor={`auth-${key}`} className="form-label">{label}</label>
        <input id={`auth-${key}`} type={type} className="form-control" required disabled={busy}
          value={form[key]} onChange={e => setForm({ ...form, [key]: e.target.value })}
          maxLength={key === 'email' ? 254 : key === 'nome' ? 120 : 128}
          minLength={register && type === 'password' ? 8 : key === 'nome' ? 2 : undefined}
          autoComplete={key === 'nome' ? 'name' : key === 'email' ? 'username' : register ? 'new-password' : 'current-password'} />
      </div>)}
      {register && <p className="form-text">Senha de 8 a 128 caracteres, com maiúscula, minúscula, número e símbolo.</p>}
      <button className="btn btn-primary" disabled={busy}>{busy ? 'Aguarde…' : register ? 'Criar conta' : 'Entrar'}</button>
      {busy && <p className="form-text" role="status">A primeira conexão pode demorar enquanto o servidor inicia.</p>}
    </form>
    <p className="small text-body-secondary mt-3">Por segurança, a sessão fica apenas nesta aba e termina ao recarregar ou após 30 minutos. Seus perfis salvos permanecem na conta.</p>
    <Link to={register ? '/login' : '/cadastro'}>{register ? 'Já tenho conta' : 'Criar uma conta'}</Link>
  </section>;
}

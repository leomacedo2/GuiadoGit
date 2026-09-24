import { useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { errorMessage, registerAccount } from "../services/api";
import logoGuiadoGit from "../assets/branding/logo-guiadogit.png";

export default function AuthPage({ register = false }) {
  const { login, user } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    nome: "",
    email: "",
    password: "",
    confirmPassword: "",
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [created, setCreated] = useState(false);
  async function submit(event) {
    event.preventDefault();
    if (busy) return;
    setError("");
    if (register && form.password !== form.confirmPassword) {
      setError("As senhas devem ser iguais.");
      return;
    }
    setBusy(true);
    try {
      if (register) {
        await registerAccount(form);
        setCreated(true);
        setForm({ nome: "", email: "", password: "", confirmPassword: "" });
      } else {
        await login({ email: form.email, password: form.password });
        navigate("/minhas-analises", { replace: true });
      }
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  if (user) return <Navigate to="/minhas-analises" replace />;
  if (created)
    return (
      <div className="alert alert-success">
        Conta criada. <Link to="/login">Entrar na plataforma</Link>.
      </div>
    );
  return (
    <section className="auth-panel mx-auto">
      {register ? (
        <h1 className="display-6 fw-bold">Criar conta</h1>
      ) : (
        <div className="text-center mb-3">
          <img src={logoGuiadoGit} alt="GuiaDoGit" className="auth-logo" />
        </div>
      )}

      <p className="text-body-secondary mb-4">
        Analise portfólios GitHub, identifique tecnologias demonstradas e
        acompanhe sua evolução.
      </p>

      <div className="card">
        <div className="card-body p-4">
          {error && (
            <div className="alert alert-danger" role="alert">
              {error}
            </div>
          )}

          <form onSubmit={submit}>
            {(register
              ? [
                  ["nome", "Nome", "text"],
                  ["email", "E-mail", "email"],
                  ["password", "Senha", "password"],
                  ["confirmPassword", "Confirmar senha", "password"],
                ]
              : [
                  ["email", "E-mail", "email"],
                  ["password", "Senha", "password"],
                ]
            ).map(([key, label, type]) => (
              <div className="mb-3" key={key}>
                <label htmlFor={`auth-${key}`} className="form-label">
                  {label}
                </label>

                <input
                  id={`auth-${key}`}
                  type={type}
                  className="form-control"
                  required
                  disabled={busy}
                  value={form[key]}
                  onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                  maxLength={key === "email" ? 254 : key === "nome" ? 120 : 128}
                  minLength={
                    register && type === "password"
                      ? 8
                      : key === "nome"
                        ? 2
                        : undefined
                  }
                  autoComplete={
                    key === "nome"
                      ? "name"
                      : key === "email"
                        ? "username"
                        : register
                          ? "new-password"
                          : "current-password"
                  }
                />
              </div>
            ))}

            {register && (
              <p className="form-text">
                Senha de 8 a 128 caracteres, com maiúscula, minúscula, número e
                símbolo.
              </p>
            )}

            <button className="btn btn-primary w-100" disabled={busy}>
              {busy ? "Aguarde…" : register ? "Criar conta" : "Entrar"}
            </button>

            {busy && (
              <p className="form-text" role="status">
                A primeira conexão pode demorar enquanto o servidor inicia.
              </p>
            )}
          </form>

          <Link
            className="btn btn-outline-primary w-100 mt-3"
            to={register ? "/login" : "/cadastro"}
          >
            {register ? "Já tenho conta" : "Criar conta"}
          </Link>
        </div>
      </div>

      <Link className="btn btn-outline-secondary w-100 mt-3" to="/analisar">
        Continuar como visitante
      </Link>

      <p className="small text-body-secondary mt-3">
        Sua conta da plataforma é independente do GitHub. A sessão pode ser
        restaurada por até 30 dias. Use Sair em dispositivos compartilhados;
        suas análises salvas permanecem.
      </p>
    </section>
  );
}

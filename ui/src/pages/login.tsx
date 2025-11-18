import { useState } from "react";
import { api } from "../api";
import logoUrl from "../assets/IDS-Logo.svg";//ids-logo.png";

export default function Login() {
  const [userName, setUser] = useState("");
  const [password, setPass] = useState("");
  const [error, setError] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  const login = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await api.post("/auth/login", { userName, password });
      window.location.href = "/forms";
    } catch (err: any) {
      setError(err?.response?.data?.message || "Unable to sign in");
    }
  };

  return (
    <div className="auth-layout">
      <div className="auth-card">
        <img src={logoUrl} alt="Independent Dealer Solutions" className="auth-logo" />
        <h2>Welcome back</h2>
        <p>Sign in with your dealership credentials to continue.</p>

        <form className="auth-form" onSubmit={login}>
          <div className="form-field">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              className="input-control"
              placeholder="i.e. finance.manager"
              value={userName}
              onChange={(e) => setUser(e.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label htmlFor="password">Password</label>
            <div className="password-field">
              <input
                id="password"
                className="input-control"
                placeholder="Enter your password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPass(e.target.value)}
                required
              />
              <button
                type="button"
                className="password-toggle"
                onClick={() => setShowPassword((prev) => !prev)}
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? "🙈" : "👁️"}
              </button>
            </div>
          </div>

          {error && <p className="error-text">{error}</p>}

          <button type="submit" className="btn btn--primary btn--full">
            Log in
          </button>
        </form>
      </div>
    </div>
  );
}

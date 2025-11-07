import { useState } from "react";
import { api } from "../api";

export default function Login() {
  const [userName, setUser] = useState(""); const [password, setPass] = useState("");
  const login = async (e: React.FormEvent) => {
    e.preventDefault();
    await api.post("/auth/login", { userName, password });
    window.location.href = "/"; // go to list
  };
  return (
    <form onSubmit={login} style={{maxWidth:360, margin:"60px auto"}}>
      <h2>Sign in</h2>
      <input placeholder="Username" value={userName} onChange={e=>setUser(e.target.value)} required />
      <input placeholder="Password" type="password" value={password} onChange={e=>setPass(e.target.value)} required />
      <button type="submit">Login</button>
    </form>
  );
}

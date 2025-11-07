import { useEffect, useState } from "react";
import { api } from "../api";
import { Link } from "react-router-dom";

type FormInfo = { slug: string; title: string; };
export default function FormList() {
  const [q,setQ] = useState(""); const [forms,setForms] = useState<FormInfo[]>([]);
  const search = async () => {
    const res = await api.get<FormInfo[]>("/forms", { params: { q } });
    setForms(res.data);
  };
  useEffect(()=>{ search(); },[]);
  return (
    <div style={{maxWidth:800, margin:"20px auto"}}>
      <h2>Forms</h2>
      <input value={q} onChange={e=>setQ(e.target.value)} placeholder="Search..." />
      <button onClick={search}>Search</button>
      <ul>{forms.map(f => <li key={f.slug}><Link to={`/forms/${f.slug}`}>{f.title}</Link></li>)}</ul>
    </div>
  );
}

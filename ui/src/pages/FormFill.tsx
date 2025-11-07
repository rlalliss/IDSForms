import { useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { api } from "../api";

type Field = { pdfFieldName:string; label:string; type:string; required:boolean; orderIndex:number; };
type Meta = { slug:string; title:string; fields:Field[]; };

export default function FormFill() {
  const { slug } = useParams();
  const [meta, setMeta] = useState<Meta|null>(null);
  const [values, setValues] = useState<Record<string,string>>({});
  const [to, setTo]   = useState("");
  const [cc, setCc]   = useState("");
  const [bcc, setBcc] = useState("");

  useEffect(() => {
    (async () => {
      const m = await api.get(`/forms/${slug}`);
      setMeta(m.data);
      const p = await api.get(`/forms/${slug}/prefill`);
      setValues(p.data);
    })();
  }, [slug]);

  const setVal = (k:string, v:string) => setValues(prev => ({...prev, [k]: v}));

  const submit = async () => {
    await api.post(`/forms/${slug}/submit`, {
      values, flatten:false,
      toOverride: to || null,
      ccOverride: cc || null,
      bccOverride: bcc || null
    });
    alert("Sent!");
  };

  // simple client-side required check
  const missing = useMemo(() => {
    if (!meta) return [];
    return meta.fields.filter(f => f.required && !values[f.pdfFieldName]).map(f => f.label);
  }, [meta, values]);

  // PREVIEW filled
  const previewFilled = async () => {
    const res = await api.post(`/forms/${slug}/preview`, { values }, { responseType: "blob" });
    const url = URL.createObjectURL(res.data);
    window.open(url, "_blank");
  };

  if (!meta) return <div>Loading…</div>;

  return (
    <div style={{display:"grid", gridTemplateColumns:"1fr 1fr", gap:16, padding:16}}>
      {/* Left: form fields */}
      <div>
        <h2>{meta.title}</h2>

        <div style={{display:"grid", gridTemplateColumns:"1fr 2fr", gap:8}}>
          {meta.fields.sort((a,b)=>a.orderIndex-b.orderIndex).map(f => (
            <label key={f.pdfFieldName} style={{display:"contents"}}>
              <div>{f.label}{f.required && " *"}</div>
              {f.type === "textarea" ? (
                <textarea value={values[f.pdfFieldName] ?? ""} onChange={e=>setVal(f.pdfFieldName, e.target.value)} />
              ) : (
                <input
                  type={["email","tel","number","date"].includes(f.type) ? f.type : "text"}
                  value={values[f.pdfFieldName] ?? ""}
                  onChange={e=>setVal(f.pdfFieldName, e.target.value)}
                />
              )}
            </label>
          ))}
        </div>

        <h3 style={{marginTop:16}}>Recipients</h3>
        <div style={{display:"grid", gridTemplateColumns:"120px 1fr", gap:8}}>
          <div>To</div><input placeholder="a@x.com,b@y.com" value={to} onChange={e=>setTo(e.target.value)} />
          <div>Cc</div><input placeholder="optional" value={cc} onChange={e=>setCc(e.target.value)} />
          <div>Bcc</div><input placeholder="optional" value={bcc} onChange={e=>setBcc(e.target.value)} />
        </div>

        {missing.length > 0 && (
          <div style={{color:"crimson", marginTop:8}}>
            Missing: {missing.join(", ")}
          </div>
        )}

        <div style={{display:"flex", gap:8, marginTop:16}}>
          <button onClick={previewFilled}>Preview filled</button>
          <button onClick={submit} disabled={missing.length>0}>Submit & Email</button>
        </div>
      </div>

      {/* Right: PDF viewer (blank template) */}
      <div>
        <h3>Form Preview</h3>
        <iframe
          title="pdf"
          style={{width:"100%", height:"80vh", border:"1px solid #ddd"}}
          src={`${api.defaults.baseURL}/forms/${slug}/pdf`}
        />
        <div style={{fontSize:12, color:"#666", marginTop:4}}>
          Tip: Click <b>Preview filled</b> to open a live-filled copy in a new tab.
        </div>
      </div>
    </div>
  );
}

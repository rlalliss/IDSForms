import SignaturePad from "signature_pad";
import { useEffect, useRef } from "react";

export default function SignatureCapture({ onDone }: { onDone: (dataUrl: string)=>void }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const padRef = useRef<SignaturePad | null>(null);   // <-- allow null, give initial value

  useEffect(() => {
    if (!canvasRef.current) return;
    const canvas = canvasRef.current;
    // size it for crisp lines
    const ratio = Math.max(window.devicePixelRatio || 1, 1);
    canvas.width = 500 * ratio;
    canvas.height = 200 * ratio;
    canvas.style.width = "500px";
    canvas.style.height = "200px";
    const ctx = canvas.getContext("2d")!;
    ctx.scale(ratio, ratio);

    padRef.current = new SignaturePad(canvas, { minWidth: 0.7, maxWidth: 2.0 });
  }, []);

  return (
    <div>
      <canvas ref={canvasRef} style={{ border: "1px solid #ccc" }} />
      <div style={{ marginTop: 8, display:"flex", gap:8 }}>
        <button onClick={() => padRef.current?.clear()}>Clear</button>
        <button onClick={() => {
          if (padRef.current?.isEmpty()) return;
          onDone(padRef.current!.toDataURL("image/png")); // send to server
        }}>Use Signature</button>
      </div>
    </div>
  );
}

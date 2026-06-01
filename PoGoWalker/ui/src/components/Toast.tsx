import { useEffect } from "react";
import { useStore } from "../store";

export default function Toast() {
  const toast = useStore((s) => s.toast);
  const setToast = useStore((s) => s.setToast);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 4000);
    return () => clearTimeout(t);
  }, [toast, setToast]);

  if (!toast) return null;

  return (
    <div className="absolute bottom-4 left-1/2 -translate-x-1/2 rounded-lg border border-edge bg-panel px-4 py-2 text-sm shadow-lg">
      {toast}
    </div>
  );
}

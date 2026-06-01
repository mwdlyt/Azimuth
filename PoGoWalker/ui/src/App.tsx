import { useEffect } from "react";
import { useStore } from "./store";
import TopBar from "./components/TopBar";
import MapView from "./components/MapView";
import ControlPanel from "./components/ControlPanel";
import Toast from "./components/Toast";

export default function App() {
  const connect = useStore((s) => s.connect);

  useEffect(() => {
    connect();
  }, [connect]);

  return (
    <div className="flex h-full flex-col">
      <TopBar />
      <div className="flex min-h-0 flex-1">
        <div className="relative min-w-0 flex-1">
          <MapView />
          <Toast />
        </div>
        <ControlPanel />
      </div>
    </div>
  );
}

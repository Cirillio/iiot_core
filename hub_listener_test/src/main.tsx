import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.tsx";

export interface Metric {
  Time: string;
  SensorId: number;
  RawValue: number;
  Value: number;
}

export interface ConnectionStatus {
  state: "Connected" | "Disconnected" | "Connecting";
  color: string;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

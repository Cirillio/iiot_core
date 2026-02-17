import { useState, useEffect, useMemo } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import type { ConnectionStatus, Metric } from "../main";

export interface MetricWithHistory extends Metric {
  history: number[];
}

export const useMetrics = (hubUrl: string) => {
  const [metrics, setMetrics] = useState<Record<number, MetricWithHistory>>({});
  const [globalHistory, setGlobalHistory] = useState<Metric[]>([]);
  const [status, setStatus] = useState<ConnectionStatus["state"]>("Disconnected");
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.on("ReceiveMetrics", (jsonString: string) => {
      try {
        const data: Metric = JSON.parse(jsonString);
        const now = new Date();
        setLastUpdate(now);
        
        setMetrics((prev) => {
          const existing = prev[data.SensorId];
          const newHistory = existing 
            ? [...existing.history, data.Value].slice(-40) 
            : [data.Value];
            
          return {
            ...prev,
            [data.SensorId]: { ...data, history: newHistory },
          };
        });

        setGlobalHistory(prev => [data, ...prev].slice(0, 50));
      } catch (e) {
        console.error("Error parsing metric:", e);
      }
    });

    const start = async () => {
      setStatus("Connecting");
      try {
        await connection.start();
        setStatus("Connected");
      } catch {
        setStatus("Disconnected");
        setTimeout(start, 5000);
      }
    };

    start();
    return () => { connection.stop(); };
  }, [hubUrl]);

  const stats = useMemo(() => {
    const values = Object.values(metrics);
    if (values.length === 0) return null;
    const avg = values.reduce((acc, m) => acc + m.Value, 0) / values.length;
    return { count: values.length, avg: parseFloat(avg.toFixed(2)) };
  }, [metrics]);

  return { metrics: Object.values(metrics), globalHistory, status, lastUpdate, stats };
};

import type { ConnectionStatus } from "../../main";

export const ConnectionBadge = ({ status, lastUpdate }: { status: ConnectionStatus["state"], lastUpdate: Date | null }) => {
  const styles = {
    Connected: "bg-emerald-50 text-emerald-700",
    Connecting: "bg-amber-50 text-amber-700",
    Disconnected: "bg-rose-50 text-rose-700",
  }[status];

  const labels = {
    Connected: "Подключено",
    Connecting: "Подключение",
    Disconnected: "Отключено",
  }[status];

  return (
    <div className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-2 transition-colors ${styles}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${status === "Connected" ? "bg-emerald-500 animate-pulse" : "bg-current"}`} />
      <span>{labels}</span>
      {lastUpdate && status === "Connected" && (
        <span className="opacity-40 ml-1 border-l border-current pl-2 font-medium">
          {lastUpdate.toLocaleTimeString()}
        </span>
      )}
    </div>
  );
};

export const StatsCard = ({ label, value, unit, color = "text-[#1d1d1f]" }: { label: string, value: string | number, unit?: string, color?: string }) => (
  <div className="bg-white p-5 rounded-2xl flex flex-col justify-between">
    <span className="text-[10px] font-bold text-[#86868b] uppercase tracking-wider">{label}</span>
    <div className="flex items-baseline gap-1 mt-1">
      <span className={`text-3xl font-bold tracking-tight ${color}`}>{value}</span>
      {unit && <span className="text-[10px] font-bold text-[#86868b] uppercase">{unit}</span>}
    </div>
  </div>
);

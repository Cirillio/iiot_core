import type { ConnectionStatus } from "../../main";

export const ConnectionBadge = ({ status, lastUpdate }: { status: ConnectionStatus["state"], lastUpdate: Date | null }) => {
  const styles = {
    Connected: "bg-emerald-50 text-emerald-700 border-emerald-100",
    Connecting: "bg-amber-50 text-amber-700 border-amber-100",
    Disconnected: "bg-rose-50 text-rose-700 border-rose-100",
  }[status];

  return (
    <div className={`px-4 py-2 rounded-2xl border text-sm font-semibold flex items-center gap-3 transition-all duration-500 ${styles}`}>
      <span className={`w-2.5 h-2.5 rounded-full ${status === "Connected" ? "bg-emerald-500 animate-pulse" : "bg-current"}`} />
      <span className="capitalize">{status}</span>
      {lastUpdate && status === "Connected" && (
        <span className="text-[10px] opacity-40 ml-1 border-l border-current pl-3">
          {lastUpdate.toLocaleTimeString()}
        </span>
      )}
    </div>
  );
};

export const StatsCard = ({ label, value, unit, color = "text-[#1d1d1f]" }: { label: string, value: string | number, unit?: string, color?: string }) => (
  <div className="bg-white p-6 rounded-[28px] border border-[#d2d2d7]/30 shadow-sm flex flex-col justify-between">
    <span className="text-[11px] font-bold text-[#86868b] uppercase tracking-widest">{label}</span>
    <div className="flex items-baseline gap-1 mt-2">
      <span className={`text-4xl font-semibold tracking-tight ${color}`}>{value}</span>
      {unit && <span className="text-xs font-bold text-[#86868b] uppercase">{unit}</span>}
    </div>
  </div>
);

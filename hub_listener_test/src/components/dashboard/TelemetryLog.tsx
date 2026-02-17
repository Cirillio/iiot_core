import type { Metric } from "../../main";

export const TelemetryLog = ({ history }: { history: Metric[] }) => (
  <div className="space-y-4 mt-12">
    <div className="flex justify-between items-center px-4">
      <h2 className="text-xs font-bold text-[#86868b] uppercase tracking-widest">Activity Journal</h2>
      <span className="text-[10px] text-emerald-600 font-bold bg-emerald-50 px-2 py-0.5 rounded-full">LIVE STREAM</span>
    </div>

    <div className="bg-white rounded-[32px] border border-[#d2d2d7]/30 shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b border-[#f5f5f7] bg-[#fbfbfd]">
              <th className="px-6 py-4 text-[10px] font-bold text-[#86868b] uppercase tracking-wider">Timestamp</th>
              <th className="px-6 py-4 text-[10px] font-bold text-[#86868b] uppercase tracking-wider">Node</th>
              <th className="px-6 py-4 text-[10px] font-bold text-[#86868b] uppercase tracking-wider text-right">Raw</th>
              <th className="px-6 py-4 text-[10px] font-bold text-[#86868b] uppercase tracking-wider text-right">Processed</th>
              <th className="px-6 py-4 text-[10px] font-bold text-[#86868b] uppercase tracking-wider text-center">Sync</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#f5f5f7]">
            {history.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-6 py-16 text-center text-[#86868b]">
                  <p className="text-sm font-medium">Waiting for data packets...</p>
                </td>
              </tr>
            ) : (
              history.map((entry, i) => (
                <tr key={`${entry.SensorId}-${entry.Time}-${i}`} className="hover:bg-[#fbfbfd]/50 transition-colors group">
                  <td className="px-6 py-4 text-sm font-medium tabular-nums text-[#424245]">
                    {new Date(entry.Time).toLocaleTimeString([], { hour12: false, fractionalSecondDigits: 2 })}
                  </td>
                  <td className="px-6 py-4 text-sm font-bold">
                    <span className="bg-[#f5f5f7] px-2 py-0.5 rounded-md text-[11px]">#{entry.SensorId}</span>
                  </td>
                  <td className="px-6 py-4 text-right text-sm font-mono text-[#86868b]">
                    {entry.RawValue}
                  </td>
                  <td className="px-6 py-4 text-right">
                    <span className="text-sm font-bold text-indigo-600 tabular-nums">
                      {entry.Value.toFixed(3)}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex justify-center">
                      <div className="w-1.5 h-1.5 rounded-full bg-emerald-400 group-hover:scale-125 transition-transform" />
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  </div>
);

import type { Metric } from "../../main";

export const TelemetryLog = ({ history }: { history: Metric[] }) => (
  <div className="flex-1 flex flex-col min-h-0 space-y-3">
    <div className="flex justify-between items-center px-4 shrink-0">
      <h2 className="text-[10px] font-bold text-[#86868b] uppercase tracking-widest">Журнал событий</h2>
      <span className="text-[9px] font-bold text-[#d2d2d7]">ПОСЛЕДНИЕ 50 ЗАПИСЕЙ</span>
    </div>

    <div className="flex-1 bg-white rounded-2xl overflow-hidden flex flex-col">
      <div className="overflow-y-auto flex-1 scrollbar-none hover:scrollbar-thin transition-all">
        <table className="w-full text-left border-collapse">
          <thead className="sticky top-0 z-20 bg-white/80 backdrop-blur-sm border-b border-[#f5f5f7]">
            <tr>
              <th className="pl-6 py-3 text-[9px] font-bold text-[#86868b] uppercase tracking-wider">Время</th>
              <th className="px-4 py-3 text-[9px] font-bold text-[#86868b] uppercase tracking-wider text-center">ID</th>
              <th className="pr-6 py-3 text-[9px] font-bold text-[#86868b] uppercase tracking-wider text-right">Значение</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#f5f5f7]">
            {history.length === 0 ? (
              <tr>
                <td colSpan={3} className="px-6 py-12 text-center text-[#d2d2d7]">
                  <p className="text-xs font-medium">Ожидание данных...</p>
                </td>
              </tr>
            ) : (
              history.map((entry, i) => (
                <tr key={`${entry.SensorId}-${entry.Time}-${i}`} className="hover:bg-[#fbfbfd] transition-colors group">
                  <td className="pl-6 py-2.5 text-[11px] font-medium tabular-nums text-[#424245]">
                    {(() => {
                      const d = new Date(entry.Time);
                      return `${d.toLocaleTimeString([], { hour12: false })}.${d.getMilliseconds().toString().padStart(3, '0')}`;
                    })()}
                  </td>
                  <td className="px-4 py-2.5 text-center text-[10px] font-bold text-[#86868b]">
                    #{entry.SensorId}
                  </td>
                  <td className="pr-6 py-2.5 text-right">
                    <span className={`text-[11px] font-bold tabular-nums ${entry.Value === 0 || entry.Value === 1 ? (entry.Value === 1 ? "text-emerald-600" : "text-rose-500") : "text-indigo-600"}`}>
                      {entry.Value === 0 || entry.Value === 1 ? (entry.Value === 1 ? "ВКЛ" : "ВЫКЛ") : entry.Value.toFixed(3)}
                    </span>
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

const labels = [
  ["pending", "En attente"],
  ["approved", "Approuvés"],
  ["rejected", "Rejetés"],
];

export default function DashboardWorkflowChart({ counts }) {
  const maxCount = Math.max(1, ...labels.map(([key]) => counts[key] ?? 0));

  return (
    <div className="w-full space-y-7 px-5" aria-label="Workflow counts by status">
      {labels.map(([key, label]) => {
        const count = counts[key] ?? 0;
        const percentage = (count / maxCount) * 100;

        return (
          <div key={key} className="grid grid-cols-[7rem_1fr_3rem] items-center gap-3">
            <span className="text-xs text-slate-600">{label}</span>
            <div
              className="h-3 overflow-hidden rounded-full bg-slate-100"
              role="progressbar"
              aria-label={label}
              aria-valuemin={0}
              aria-valuemax={maxCount}
              aria-valuenow={count}
            >
              <div
                className="h-full rounded-full bg-emerald-500 transition-[width]"
                style={{ width: `${percentage}%` }}
              />
            </div>
            <span className="text-right text-sm font-semibold tabular-nums">{count}</span>
          </div>
        );
      })}
    </div>
  );
}

/**
 * Small local replacement for toggle-selection. The published transitive
 * package is currently unreadable in the recovered dependency tree; keeping
 * this behavior local also makes clipboard support deterministic in builds.
 */
export default function toggleSelection(): () => void {
  const selection = typeof document !== "undefined" ? document.getSelection() : null;
  const activeElement = typeof document !== "undefined" ? document.activeElement : null;
  const range = selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null;

  return () => {
    if (!selection || !range) return;
    selection.removeAllRanges();
    selection.addRange(range);
    if (activeElement instanceof HTMLElement) activeElement.focus();
  };
}

/**
 * Half-court in the canonical frame (guide §9): x from half-court (0) to the baseline (1400),
 * y across the width (-750..750), hoop at (1242.5, 0). Drawn with the baseline at the top so the
 * official sees the attacking basket "up", whichever end the team is physically shooting at.
 */
const W = 1500, L = 1400, HOOP_X = 1242.5, ARC = 675, CORNER_Y = 660, CORNER_X = HOOP_X - Math.sqrt(ARC * ARC - CORNER_Y * CORNER_Y);

export function Court({ onTap, marker }: { onTap: (x: number, y: number) => void; marker?: { x: number; y: number } | null }) {
  // SVG: horizontal = y (court width), vertical = 1400 - x so the baseline is at the top.
  const toSvg = (x: number, y: number) => ({ sx: y + W / 2, sy: L - x });
  const hoop = toSvg(HOOP_X, 0);

  function handle(e: React.MouseEvent<SVGSVGElement>) {
    const rect = e.currentTarget.getBoundingClientRect();
    const sx = ((e.clientX - rect.left) / rect.width) * W;
    const sy = ((e.clientY - rect.top) / rect.height) * L;
    onTap(Math.round(L - sy), Math.round(sx - W / 2));
  }

  // The three-point line: corner segments up from the baseline, then the arc between them.
  const left = toSvg(CORNER_X, -CORNER_Y), right = toSvg(CORNER_X, CORNER_Y);
  const arc = `M ${toSvg(L, -CORNER_Y).sx} ${toSvg(L, -CORNER_Y).sy} L ${left.sx} ${left.sy} A ${ARC} ${ARC} 0 0 0 ${right.sx} ${right.sy} L ${toSvg(L, CORNER_Y).sx} ${toSvg(L, CORNER_Y).sy}`;
  const paint = { x: toSvg(0, -245).sx, y: toSvg(L, 0).sy, w: 490, h: L - 820 };
  const m = marker ? toSvg(marker.x, marker.y) : null;

  return (
    <svg viewBox={`0 0 ${W} ${L}`} className="court" onClick={handle}>
      <rect x={0} y={0} width={W} height={L} className="floor" />
      <rect x={paint.x} y={paint.y} width={paint.w} height={paint.h} className="line" fill="none" />
      <path d={arc} className="line" fill="none" />
      <circle cx={hoop.sx} cy={hoop.sy} r={125} className="line" fill="none" strokeDasharray="20 20" />
      <circle cx={hoop.sx} cy={hoop.sy} r={22.5} className="hoop" />
      <line x1={0} y1={L} x2={W} y2={L} className="line" />
      {m && <circle cx={m.sx} cy={m.sy} r={30} className="marker" />}
    </svg>
  );
}

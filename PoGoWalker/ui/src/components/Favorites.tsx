import { useStore } from "../store";

export default function Favorites() {
  const favorites = useStore((s) => s.favorites);
  const gotoFavorite = useStore((s) => s.gotoFavorite);

  if (favorites.length === 0) return null;

  return (
    <div>
      <span className="text-[10px] font-bold tracking-widest text-muted">
        FAVORITES
      </span>
      <ul className="mt-1 space-y-1">
        {favorites.map((f, i) => (
          <li key={i}>
            <button
              onClick={() => gotoFavorite(f)}
              className="flex w-full items-center justify-between rounded-md bg-panel2 px-3 py-2 text-left text-sm hover:bg-edge"
            >
              <span>{f.name}</span>
              <span className="font-mono text-xs text-muted">
                {f.lat.toFixed(3)}, {f.lng.toFixed(3)}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

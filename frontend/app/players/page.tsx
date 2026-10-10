"use client";

import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { API_URL, getErrorMessage } from "@/lib/api";
import { GameNav } from "@/components/GameNav";

type PlayerSummary = { id: string; username: string; level: number; description: string };
type SearchResult = { players: PlayerSummary[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export default function PlayersPage() {
  const router = useRouter();
  const [input, setInput] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<SearchResult | null>(null);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setError("");
    const parameters = new URLSearchParams({ query, page: String(page), pageSize: "20" });
    const response = await fetch(`${API_URL}/api/players?${parameters}`, { credentials: "include" });
    if (response.status === 401) { router.replace("/login"); return; }
    if (!response.ok) { setError(await getErrorMessage(response)); return; }
    setResult(await response.json() as SearchResult);
  }, [page, query, router]);

  useEffect(() => { void load(); }, [load]);

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setQuery(input.trim());
  }

  return (
    <main><section className="game-panel players-panel">
      <GameNav />
      <Link className="back-link" href="/profile">← Профиль</Link>
      <p className="eyebrow game-eyebrow">Городская сеть</p>
      <h1 className="game-title">Игроки</h1>
      <form className="player-search" onSubmit={search}>
        <input value={input} onChange={(event) => setInput(event.target.value)} maxLength={24} placeholder="Имя игрока" aria-label="Имя игрока" />
        <button className="primary-button" type="submit">Найти</button>
      </form>
      {error && <p className="form-error">{error}</p>}
      {result && <>
        <p className="field-hint player-count">Найдено: {result.totalCount}</p>
        <div className="player-results">
          {result.players.length === 0 && <p className="field-hint">Игроки не найдены.</p>}
          {result.players.map((player) => <Link className="player-result" href={`/players/${player.id}`} key={player.id}>
            <div><strong>{player.username}</strong><span>Уровень {player.level}</span></div>
            <p>{player.description || "Описание пока не заполнено."}</p>
          </Link>)}
        </div>
        <nav className="pagination" aria-label="Страницы">
          <button className="secondary-button" type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>← Назад</button>
          <span>Страница {result.page}{result.totalPages > 0 ? ` из ${result.totalPages}` : ""}</span>
          <button className="secondary-button" type="button" disabled={result.totalPages === 0 || page >= result.totalPages} onClick={() => setPage((value) => value + 1)}>Далее →</button>
        </nav>
      </>}
    </section></main>
  );
}

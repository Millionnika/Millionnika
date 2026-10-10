"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch as fetch, API_URL, getErrorMessage } from "@/lib/api";

type ListName = "friends" | "incoming" | "outgoing";
type FriendItem = { relationId: string; playerId: string; username: string; level: number; createdAtUtc: string };
type FriendList = { items: FriendItem[]; page: number; pageSize: number; totalCount: number; totalPages: number };
type Lists = Record<ListName, FriendList | null>;

const emptyLists: Lists = { friends: null, incoming: null, outgoing: null };

export default function FriendsPage() {
  const router = useRouter();
  const [pages, setPages] = useState<Record<ListName, number>>({ friends: 1, incoming: 1, outgoing: 1 });
  const [lists, setLists] = useState<Lists>(emptyLists);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busyId, setBusyId] = useState("");

  const load = useCallback(async () => {
    setError("");
    try {
      const names: ListName[] = ["friends", "incoming", "outgoing"];
      const responses = await Promise.all(names.map((name) => fetch(
        `${API_URL}/api/friends?list=${name}&page=${pages[name]}&pageSize=20`, { credentials: "include" })));
      if (responses.some((response) => response.status === 401)) { router.replace("/login"); return; }
      const failed = responses.find((response) => !response.ok);
      if (failed) { setError(await getErrorMessage(failed)); return; }
      const data = await Promise.all(responses.map((response) => response.json() as Promise<FriendList>));
      setLists({ friends: data[0], incoming: data[1], outgoing: data[2] });
    } catch { setError("Сервер игры недоступен."); }
  }, [pages, router]);

  useEffect(() => { void load(); }, [load]);

  async function act(path: string, method: "POST" | "DELETE", relationId: string) {
    setBusyId(relationId); setError(""); setMessage("");
    try {
      const response = await fetch(`${API_URL}${path}`, { method, credentials: "include" });
      if (response.status === 401) { router.replace("/login"); return; }
      const result = await response.json() as { message?: string };
      if (!response.ok) { setError(result.message ?? "Действие не выполнено."); return; }
      setMessage(result.message ?? "Готово.");
      await load();
    } catch { setError("Сервер игры недоступен."); }
    finally { setBusyId(""); }
  }

  function changePage(name: ListName, page: number) {
    setPages((current) => ({ ...current, [name]: page }));
  }

  return <main><section className="game-panel friends-panel">
    <Link className="back-link" href="/profile">← Профиль</Link>
    <p className="eyebrow game-eyebrow">Городская сеть</p>
    <h1 className="game-title">Друзья</h1>
    {message && <p className="action-result success">{message}</p>}
    {error && <p className="form-error">{error}</p>}
    <FriendSection title="Друзья" name="friends" data={lists.friends} busyId={busyId} onAction={(item) => act(`/api/friends/${item.relationId}`, "DELETE", item.relationId)} actionLabel="Удалить" onPage={changePage} />
    <FriendSection title="Входящие заявки" name="incoming" data={lists.incoming} busyId={busyId}
      onAction={(item) => act(`/api/friends/requests/${item.relationId}/accept`, "POST", item.relationId)} actionLabel="Принять"
      secondaryAction={(item) => act(`/api/friends/requests/${item.relationId}/reject`, "POST", item.relationId)} secondaryLabel="Отклонить" onPage={changePage} />
    <FriendSection title="Исходящие заявки" name="outgoing" data={lists.outgoing} busyId={busyId}
      onAction={(item) => act(`/api/friends/requests/${item.relationId}`, "DELETE", item.relationId)} actionLabel="Отменить" onPage={changePage} />
  </section></main>;
}

function FriendSection({ title, name, data, busyId, onAction, actionLabel, secondaryAction, secondaryLabel, onPage }: Readonly<{
  title: string; name: ListName; data: FriendList | null; busyId: string;
  onAction: (item: FriendItem) => void; actionLabel: string;
  secondaryAction?: (item: FriendItem) => void; secondaryLabel?: string;
  onPage: (name: ListName, page: number) => void;
}>) {
  return <section className="friend-section">
    <h2 className="section-title">{title}{data ? ` · ${data.totalCount}` : ""}</h2>
    {data && data.items.length === 0 && <p className="field-hint">Список пуст.</p>}
    <div className="friend-list">{data?.items.map((item) => <article className="friend-row" key={item.relationId}>
      <Link href={`/players/${item.playerId}`}><strong>{item.username}</strong><span>Уровень {item.level}</span></Link>
      <div className="friend-buttons">
        <button className="primary-button" type="button" disabled={busyId === item.relationId} onClick={() => onAction(item)}>{actionLabel}</button>
        {secondaryAction && <button className="secondary-button" type="button" disabled={busyId === item.relationId} onClick={() => secondaryAction(item)}>{secondaryLabel}</button>}
      </div>
    </article>)}</div>
    {data && data.totalPages > 1 && <nav className="pagination" aria-label={`Страницы: ${title}`}>
      <button className="secondary-button" type="button" disabled={data.page <= 1} onClick={() => onPage(name, data.page - 1)}>← Назад</button>
      <span>Страница {data.page} из {data.totalPages}</span>
      <button className="secondary-button" type="button" disabled={data.page >= data.totalPages} onClick={() => onPage(name, data.page + 1)}>Далее →</button>
    </nav>}
  </section>;
}
